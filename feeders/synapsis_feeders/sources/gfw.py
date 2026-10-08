"""Global Fishing Watch Events API v3: loitering and port visits for chosen vessels (needs GFW_TOKEN).

Vessels: --set imos=IMO1234567,... or, by default, round 3's design drawn from today's OpenSanctions maritime list:
70 shadow-fleet ships and 70 ships with port-state detentions only (random sample, seed 7). Window: --since/--until,
default 2023-01-01 to 2025-12-31."""
from __future__ import annotations

import io
import math
import random
from datetime import date, timedelta

import pandas as pd

from ..base import Checkpoint, Feeder, register
from ..sil import admin, attributes, entity, event, measures, participant, place, point, time_spec, utc
from .opensanctions import INDEX, iso2

BASE = "https://gateway.api.globalfishingwatch.org/v3"
DATASETS = {"loitering": "public-global-loitering-events:latest", "port_visits": "public-global-port-visits-events:latest"}


def bbox_km(box) -> float:
    """Half the diagonal of an event's bounding box in km: the honest uncertainty of its mean position."""
    if not box or len(box) != 4:
        return 10.0
    lon0, lat0, lon1, lat1 = box
    dy = (lat1 - lat0) * 111.0
    dx = (lon1 - lon0) * 111.0 * math.cos(math.radians((lat0 + lat1) / 2))
    return max(1.0, round(math.hypot(dx, dy) / 2, 2))


@register
class GfwFeeder(Feeder):
    name = "gfw"
    package = "gfw"
    keys = ("GFW_TOKEN",)
    title = "Global Fishing Watch vessel events (loitering, port visits)"
    min_interval = 0.3

    def headers(self):
        return {"Authorization": f"Bearer {self.ctx.key('GFW_TOKEN')}", "Accept": "application/json"}

    def choose(self) -> list[str]:
        ctx = self.ctx
        if ctx.options.get("imos"):
            return [i.strip() for i in ctx.options["imos"].split(",") if i.strip()]
        idx = ctx.http.json(INDEX)
        url = next(r["url"] for r in idx["resources"] if r["name"] == "maritime.csv")
        df = pd.read_csv(io.BytesIO(ctx.http.bytes(url)), dtype=str).fillna("")
        v = df[(df.type == "VESSEL") & df.imo.str.match(r"^IMO\d{7}$")]
        shadow = sorted(set(v[v.risk.str.contains("mare.shadow")].imo))
        control = sorted(set(v[v.risk == "mare.detained"].imo) - set(shadow))
        n = int(ctx.options.get("sample", "70"))
        rng = random.Random(7)
        return rng.sample(shadow, min(n, len(shadow))) + rng.sample(control, min(n, len(control)))

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:gfw:events")
        start, end = ctx.since or date(2023, 1, 1), ctx.until or date(2025, 12, 31)
        done = set(ctx.cursor.get("imos_done", [])) if ctx.cursor.get("window") == f"{start}/{end}" else set()
        for imo in self.choose():
            if imo in done:
                continue
            found = ctx.http.json(f"{BASE}/vessels/search", headers=self.headers(),
                                  params={"query": imo[3:], "datasets[0]": "public-global-vessel-identity:latest", "limit": 1})
            entries = (found or {}).get("entries") or []
            if not entries:
                ctx.log.info("%s not found in GFW", imo)
                done.add(imo)
                continue
            infos = entries[0].get("selfReportedInfo") or []
            ids = [x["id"] for x in infos if x.get("id")]
            first = infos[0] if infos else {}
            vid = f"ent:imo:{imo}"
            first_tx = min((x.get("transmissionDateFrom") for x in infos if x.get("transmissionDateFrom")), default=None)
            yield entity(vid, "Vessel", first.get("shipname") or imo, src, utc(first_tx) if first_tx else ctx.started_at, "observed",
                         aliases=sorted({x.get("shipname") for x in infos if x.get("shipname")}),
                         attributes=attributes(flag=first.get("flag") if (first.get("flag") or "").isalpha() and len(first.get("flag") or "") in (2, 3) else None,
                                               mmsi=first.get("ssvid") if str(first.get("ssvid") or "").isdigit() and len(str(first.get("ssvid"))) == 9 else None))
            for kind, dataset in DATASETS.items():
                offset = 0
                while offset is not None:
                    params = {"datasets[0]": dataset, "start-date": start.isoformat(), "end-date": end.isoformat(),
                              "limit": 100, "offset": offset}
                    params.update({f"vessels[{i}]": v for i, v in enumerate(ids)})
                    page = ctx.http.json(f"{BASE}/events", headers=self.headers(), params=params) or {}
                    for e in page.get("entries", []):
                        rec = self._event(kind, e, vid, src)
                        if rec:
                            yield rec
                    offset = page.get("nextOffset")
            done.add(imo)
            yield Checkpoint({"window": f"{start}/{end}", "imos_done": sorted(done)})

    @staticmethod
    def _event(kind: str, e: dict, vid: str, src: dict):
        t0, t1 = utc(e["start"]), utc(e.get("end") or e["start"])
        pos = e.get("position") or {}
        if "lat" not in pos or "lon" not in pos:
            return None
        known = t1 + timedelta(days=src["publication_lag_days"])
        gfw_vessel = (e.get("vessel") or {}).get("id")
        if kind == "loitering":
            lo = e.get("loitering") or {}
            return event(f"evt:gfw:{e['id']}", "MAR.VESSEL.LOITERING", src, time_spec(t0, "second", t1),
                         point(pos["lon"], pos["lat"], bbox_km(e.get("boundingBox"))), known, "proxy_publication_lag",
                         participants=[participant(vid, "subject")],
                         measures=measures(("duration_hours", lo.get("totalTimeHours"), "hours"), ("distance_km", lo.get("totalDistanceKm"), "km"),
                                           ("average_speed_knots", lo.get("averageSpeedKnots"), "knots"),
                                           ("distance_from_shore_km", lo.get("averageDistanceFromShoreKm"), "km")),
                         attributes=attributes(gfw_vessel_id=gfw_vessel, eez=[str(x) for x in (e.get("regions") or {}).get("eez", [])][:10]))
        pv = e.get("port_visit") or {}
        anchorage = pv.get("intermediateAnchorage") or pv.get("startAnchorage") or {}
        flag = iso2(anchorage.get("flag") or "")
        return event(f"evt:gfw:{e['id']}", "MAR.VESSEL.PORT_VISIT", src, time_spec(t0, "second", t1),
                     place(point(pos["lon"], pos["lat"], 5.0), admin(flag) if flag else None), known, "proxy_publication_lag",
                     participants=[participant(vid, "subject")],
                     measures=measures(("duration_hours", pv.get("durationHrs"), "hours")),
                     attributes=attributes(gfw_vessel_id=gfw_vessel, anchorage=(anchorage.get("name") or "")[:300] or None,
                                           port_flag=(anchorage.get("flag") or "")[:10] or None,
                                           confidence=int(pv["confidence"]) if str(pv.get("confidence", "")).isdigit() else None))
