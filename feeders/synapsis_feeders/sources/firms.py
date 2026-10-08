"""NASA FIRMS active fires (VIIRS S-NPP) for configured areas, five days per request (the API's maximum).

Standard processing (SP) is used up to its last available date and near-real-time (NRT) after it; the availability
endpoint says where the switch is. Options: --set areas=SDN:21.8,8.6,38.6,22.2;UKR:22.1,44.3,40.2,52.4 (west,south,
east,north), keep_low=yes to keep low-confidence detections. Needs FIRMS_MAP_KEY; the key never appears in logs."""
from __future__ import annotations

import io
from datetime import date, timedelta

import pandas as pd

from ..base import Checkpoint, Feeder, register
from ..sil import attributes, event, finite, measures, point, time_spec, utc

API = "https://firms.modaps.eosdis.nasa.gov/api"
AREAS = "SDN:21.8,8.6,38.6,22.2;UKR:22.1,44.3,40.2,52.4"
FIRST = date(2021, 1, 1)


@register
class FirmsFeeder(Feeder):
    name = "firms"
    package = "firms"
    keys = ("FIRMS_MAP_KEY",)
    title = "NASA FIRMS VIIRS active fires"
    min_interval = 0.5

    def records(self):
        ctx = self.ctx
        key = ctx.key("FIRMS_MAP_KEY")
        src = ctx.source("src:nasa:firms")
        avail = pd.read_csv(io.StringIO(ctx.http.get(f"{API}/data_availability/csv/{key}/all").text)).set_index("data_id")
        sp_end = date.fromisoformat(avail.loc["VIIRS_SNPP_SP", "max_date"])
        nrt_end = date.fromisoformat(avail.loc["VIIRS_SNPP_NRT", "max_date"])
        keep_low = ctx.options.get("keep_low") == "yes"
        cursor = dict(ctx.cursor)
        for spec in ctx.options.get("areas", AREAS).split(";"):
            area, bbox = spec.split(":")
            done = cursor.get(area)
            d = ctx.since or (date.fromisoformat(done) + timedelta(days=1) if done else FIRST)
            end = min(ctx.until or nrt_end, nrt_end)
            while d <= end:
                product = "VIIRS_SNPP_SP" if d <= sp_end else "VIIRS_SNPP_NRT"
                days = max(1, min(5, (min(end, sp_end if product.endswith("SP") else end) - d).days + 1))
                text = ctx.http.get(f"{API}/area/csv/{key}/{product}/{bbox}/{days}/{d.isoformat()}").text
                if not text.startswith("latitude"):
                    raise RuntimeError(f"FIRMS answered for {area} {d}: {text[:120]!r}")
                df = pd.read_csv(io.StringIO(text), dtype={"acq_time": str, "confidence": str})
                if not keep_low:
                    df = df[df.confidence != "l"]
                for r in df.itertuples(index=False):
                    hhmm = str(r.acq_time).zfill(4)
                    t = utc(f"{r.acq_date}T{hhmm[:2]}:{hhmm[2:]}:00Z")
                    sat = str(r.satellite)
                    yield event(f"evt:firms:viirs-{sat.lower()}/{t:%Y%m%dT%H%M}/{r.latitude:.5f}_{r.longitude:.5f}", "ENV.FIRE.DETECTION",
                                src, time_spec(t, "minute"), point(r.longitude, r.latitude, 0.4), t + timedelta(hours=3),
                                "proxy_publication_lag",
                                measures=measures(("frp", r.frp, "MW"), ("brightness_ti4", r.bright_ti4, "K"), ("brightness_ti5", r.bright_ti5, "K")),
                                attributes=attributes(confidence=r.confidence if r.confidence in ("l", "n", "h") else None, satellite=sat[:10],
                                                      daynight=r.daynight if r.daynight in ("D", "N") else None, product=product,
                                                      version=str(r.version)[:20]))
                d += timedelta(days=days)
                cursor[area] = (d - timedelta(days=1)).isoformat()
                yield Checkpoint(dict(cursor))
