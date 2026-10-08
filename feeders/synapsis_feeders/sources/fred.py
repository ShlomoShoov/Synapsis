"""FRED series observations (needs FRED_API_KEY). Option --set series=FEDFUNDS,DGS2,DGS10,T10Y2Y (default)."""
from __future__ import annotations

from datetime import date, timedelta

from ..base import Checkpoint, Feeder, register
from ..sil import attributes, event, logical, measures, time_spec, utc

API = "https://api.stlouisfed.org/fred"
PRECISION = {"Daily": "day", "Weekly": "day", "Monthly": "month", "Quarterly": "quarter", "Annual": "year"}  # FRED weeks end on any weekday


@register
class FredFeeder(Feeder):
    name = "fred"
    package = "fred"
    keys = ("FRED_API_KEY",)
    title = "FRED interest-rate series"
    min_interval = 0.6

    def records(self):
        ctx = self.ctx
        key = ctx.key("FRED_API_KEY")
        src = ctx.source("src:fred:series")
        cursor = dict(ctx.cursor)
        for sid in ctx.options.get("series", "FEDFUNDS,DGS2,DGS10,T10Y2Y").split(","):
            meta = ctx.http.json(f"{API}/series", params={"series_id": sid, "api_key": key, "file_type": "json"})["seriess"][0]
            frequency = (meta.get("frequency") or "Daily").split(",")[0].strip()
            precision = PRECISION.get(frequency, "day")
            start = ctx.since or (date.fromisoformat(cursor[sid]) + timedelta(days=1) if sid in cursor else date(2000, 1, 1))
            obs = ctx.http.json(f"{API}/series/observations", params={"series_id": sid, "api_key": key, "file_type": "json",
                                                                      "observation_start": start.isoformat()})["observations"]
            last = None
            for o in obs:
                if o["value"] in (".", ""):
                    continue                      # FRED writes "." for a missing value (M-NULL)
                d = date.fromisoformat(o["date"])
                if ctx.until and d > ctx.until:
                    break
                period_end = {"day": d + timedelta(days=6) if frequency == "Weekly" else d, "month": date(d.year + (d.month == 12), d.month % 12 + 1, 1) - timedelta(days=1),
                              "quarter": date(d.year + (d.month >= 10), (d.month + 2) % 12 + 1, 1) - timedelta(days=1),
                              "year": date(d.year, 12, 31)}[precision]
                known = utc(period_end) + timedelta(days=1 + src["publication_lag_days"])
                if known > ctx.started_at:
                    continue
                yield event(f"evt:fred:{sid}-{d:%Y%m%d}", "FIN.SERIES.OBSERVATION", src,
                            time_spec(d, precision, d + timedelta(days=7) if frequency == "Weekly" else None),
                            logical("finance.series", "FRED", sid), known, "proxy_publication_lag",
                            measures=measures(("rate", o["value"], "percent")),
                            attributes=attributes(series_title=meta.get("title", "")[:300] or None))
                last = d
            if last:
                cursor[sid] = last.isoformat()
                yield Checkpoint(dict(cursor))
