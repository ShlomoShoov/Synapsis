"""USGS ComCat earthquakes through the FDSN event service: one year per request for the backfill, then everything
updated since the last run (new events and revisions).

Option --set minmagnitude=5.5 (default; round 2's set). ComCat returns at most 20,000 rows per request; a year of
M5.5+ is well below that, and lower thresholds are split into months automatically."""
from __future__ import annotations

import io
from datetime import date, datetime, timedelta, timezone

import pandas as pd

from ..base import Checkpoint, Feeder, register
from ..sil import attributes, event, finite, measures, point, time_spec, utc

API = "https://earthquake.usgs.gov/fdsnws/event/1/query"
FIRST = date(1965, 1, 1)


@register
class UsgsFeeder(Feeder):
    name = "usgs"
    package = "usgs"
    title = "USGS ComCat earthquakes"
    min_interval = 1.0

    def query(self, **params) -> pd.DataFrame:
        r = self.ctx.http.get(API, params={"format": "csv", "eventtype": "earthquake", "orderby": "time-asc",
                                           "minmagnitude": self.ctx.options.get("minmagnitude", "5.5"), **params})
        return pd.read_csv(io.StringIO(r.text)) if r.text.strip() else pd.DataFrame()

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:usgs:comcat")
        if ctx.cursor.get("backfill_done") and ctx.since is None:
            since = ctx.cursor["updated_after"]
            df = self.query(updatedafter=since)
            yield from self._rows(df, src)
            yield Checkpoint(dict(ctx.cursor, updated_after=ctx.started_at.strftime("%Y-%m-%dT%H:%M:%S")))
            return
        start = ctx.since or date.fromisoformat(ctx.cursor.get("next_year", FIRST.isoformat()))
        end = ctx.until or ctx.started_at.date()
        year = start
        while year <= end:
            nxt = min(date(year.year + 1, 1, 1), end + timedelta(days=1))
            df = self.query(starttime=year.isoformat(), endtime=nxt.isoformat())
            if len(df) >= 20000:
                raise RuntimeError("more than 20,000 events in a year; raise minmagnitude or split the range")
            yield from self._rows(df, src)
            cursor = dict(ctx.cursor, next_year=nxt.isoformat())
            if nxt > ctx.started_at.date() and ctx.since is None and ctx.until is None:
                cursor.update(backfill_done=True, updated_after=ctx.started_at.strftime("%Y-%m-%dT%H:%M:%S"))
            ctx.cursor = cursor
            yield Checkpoint(cursor)
            year = nxt

    @staticmethod
    def _rows(df: pd.DataFrame, src: dict):
        for r in df.itertuples(index=False):
            lon, lat = finite(r.longitude), finite(r.latitude)
            if lon is None or lat is None:
                continue
            t = utc(str(r.time))
            unc = finite(getattr(r, "horizontalError", None))
            yield event(f"evt:usgs:{r.id}", "GEO.EARTHQUAKE", src, time_spec(t, "second"),
                        point(lon, lat, unc if unc and unc > 0 else 25.0), t + timedelta(hours=1), "proxy_publication_lag",
                        measures=measures(("magnitude", r.mag, "magnitude"), ("depth_km", r.depth, "km")),
                        attributes=attributes(magnitude_type=str(r.magType)[:10] if isinstance(r.magType, str) else None,
                                              place_name=str(r.place)[:300] if isinstance(r.place, str) else None,
                                              network=str(r.net)[:10] if isinstance(r.net, str) else None,
                                              review_status=str(r.status)[:20] if isinstance(r.status, str) else None),
                        evidence={"url": f"https://earthquake.usgs.gov/earthquakes/eventpage/{r.id}"})
