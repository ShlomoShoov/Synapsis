"""Polymarket: hourly prices of every outcome in chosen events (Gamma API for events, CLOB API for price history).

Options: --set slugs=a,b,... (default: the 2024 state presidential-winner markets), window_days=4 (hours before the
event closes, as round 3 read them), fidelity=60 (minutes)."""
from __future__ import annotations

import json
import re
from datetime import datetime, timedelta, timezone

from ..base import Checkpoint, Feeder, register
from ..geo import STATES_FOR_MARKETS, us_subdivision
from ..sil import admin, attributes, event, logical, measures, place, slug, time_spec, utc

GAMMA = "https://gamma-api.polymarket.com/events"
CLOB = "https://clob.polymarket.com/prices-history"
CLOSE_2024 = datetime(2024, 11, 5, 10, tzinfo=timezone.utc)


@register
class PolymarketFeeder(Feeder):
    name = "polymarket"
    package = "polymarket"
    title = "Polymarket outcome prices"
    min_interval = 0.25

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:polymarket:prices")
        slugs = ctx.options.get("slugs")
        targets = slugs.split(",") if slugs else [f"{slug(s)}-presidential-election-winner" for s in STATES_FOR_MARKETS]
        days = float(ctx.options.get("window_days", "4"))
        fidelity = int(ctx.options.get("fidelity", "60"))
        done = set(ctx.cursor.get("slugs_done", []))
        for event_slug in targets:
            if event_slug in done and ctx.since is None:
                continue
            found = ctx.http.json(GAMMA, params={"slug": event_slug}) or []
            if not found:
                ctx.log.info("no Polymarket event %s", event_slug)
                continue
            ev = found[0]
            state = next((s for s in STATES_FOR_MARKETS if event_slug.startswith(slug(s) + "-")), None)
            sub = us_subdivision(state) if state else None
            # the 2024 state markets are read up to the morning of election day, as in round 3
            closed = CLOSE_2024 if not slugs else utc(ev.get("endDate") or ctx.started_at.isoformat())
            for m in ev.get("markets", []):
                tokens = json.loads(m.get("clobTokenIds") or "[]")
                if not tokens:
                    continue
                outcome = (m.get("groupItemTitle") or m.get("question") or "").strip()[:200]
                end_ts = int(min(closed, ctx.started_at).timestamp())
                hist = (ctx.http.json(CLOB, params={"market": tokens[0], "startTs": int(end_ts - days * 86400), "endTs": end_ts,
                                                    "fidelity": fidelity}) or {}).get("history", [])
                for h in hist:
                    t = datetime.fromtimestamp(int(h["t"]), timezone.utc)
                    yield event(f"evt:polymarket:{event_slug}/{slug(outcome, 60)}/{t:%Y%m%dT%H%M}", "MKT.PREDICTION.PRICE", src,
                                time_spec(t, "minute"), place(logical("market.polymarket", event_slug, outcome or "yes"),
                                                              admin("US", sub) if sub else None),
                                t, "observed", measures=measures(("probability", h.get("p"), "probability")),
                                attributes=attributes(question=(m.get("question") or "")[:500] or None, outcome=outcome or None,
                                                      market_id=str(m.get("id")) if m.get("id") else None))
            done.add(event_slug)
            yield Checkpoint({"slugs_done": sorted(done)})
