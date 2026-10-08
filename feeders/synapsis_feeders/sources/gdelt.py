"""GDELT 1.0 daily event exports (data.gdeltproject.org/events/YYYYMMDD.export.CSV.zip), filtered to the configured
countries and quad classes, located at city level.

Options (--set): countries=SU:SD,UP:UA,OD:SS (GDELT's FIPS code : ISO code; default Sudan, Ukraine, South Sudan),
quads=4, geotypes=3,4 (city level), sample=weekly to read one day per week as round 3 did (default: every day)."""
from __future__ import annotations

import io
import zipfile
from datetime import date, timedelta

import pandas as pd

from ..base import Checkpoint, Feeder, register
from ..sil import admin, attributes, event, finite, measures, place, point, time_spec, utc

URL = "https://data.gdeltproject.org/events/{d:%Y%m%d}.export.CSV.zip"
COLS = {0: "id", 1: "sqldate", 5: "a1code", 6: "a1name", 15: "a2code", 16: "a2name", 25: "root_event", 26: "code",
        28: "root", 29: "quad", 30: "goldstein", 31: "mentions", 32: "sources", 33: "articles", 34: "tone",
        49: "geotype", 50: "fullname", 51: "cc", 53: "lat", 54: "lon", 56: "dateadded", 57: "url"}
TYPES = {4: "SEC.NEWS.CONFLICT", 3: "SEC.NEWS.VERBAL_CONFLICT", 2: "SEC.NEWS.MATERIAL_COOP", 1: "SEC.NEWS.VERBAL_COOP"}
UNCERTAINTY_KM = {3: 25.0, 4: 25.0, 2: 150.0, 5: 150.0}
FIRST_DAY = date(2021, 1, 6)


@register
class GdeltFeeder(Feeder):
    name = "gdelt"
    package = "gdelt"
    title = "GDELT 1.0 news-coded events"
    min_interval = 0.5

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:gdelt:events")
        countries = dict(pair.split(":") for pair in ctx.options.get("countries", "SU:SD,UP:UA,OD:SS").split(","))
        quads = {int(q) for q in ctx.options.get("quads", "4").split(",")}
        geotypes = {int(g) for g in ctx.options.get("geotypes", "3,4").split(",")}
        step = 7 if ctx.options.get("sample") == "weekly" else 1
        last = ctx.cursor.get("last_day")
        day = ctx.since or (date.fromisoformat(last) + timedelta(days=step) if last else FIRST_DAY)
        end = ctx.until or (ctx.started_at.date() - timedelta(days=1))
        while day <= end:
            raw = ctx.http.bytes(URL.format(d=day), ok_missing=True)
            if raw is None:
                ctx.log.warning("no GDELT file for %s", day)
            else:
                z = zipfile.ZipFile(io.BytesIO(raw))
                df = pd.read_csv(z.open(z.namelist()[0]), sep="\t", header=None, usecols=list(COLS), dtype=str,
                                 on_bad_lines="skip", quoting=3)
                df.columns = [COLS[c] for c in sorted(COLS)]
                df = df[df.cc.isin(countries.keys()) & df.quad.isin({str(q) for q in quads}) &
                        df.geotype.isin({str(g) for g in geotypes})]
                for r in df.itertuples(index=False):
                    rec = self._map(r, src, countries)
                    if rec:
                        yield rec
            yield Checkpoint({"last_day": day.isoformat()})
            day += timedelta(days=step)

    @staticmethod
    def _map(r, src, countries):
        try:
            occurred = utc(f"{r.sqldate[:4]}-{r.sqldate[4:6]}-{r.sqldate[6:8]}")
            added = utc(f"{r.dateadded[:4]}-{r.dateadded[4:6]}-{r.dateadded[6:8]}")
        except (TypeError, ValueError):
            return None
        geotype, lon, lat = int(r.geotype), finite(r.lon), finite(r.lat)
        pl = place(point(lon, lat, UNCERTAINTY_KM[geotype]) if lon is not None and lat is not None and geotype in UNCERTAINTY_KM else None,
                   admin(countries[r.cc], name=r.fullname))
        quad = int(r.quad)
        return event(
            f"evt:gdelt:{r.id}", TYPES[quad], src, time_spec(occurred, "day"), pl, added + timedelta(days=1), "proxy_publication_lag",
            measures=measures(("articles", r.articles, "count"), ("mentions", r.mentions, "count"),
                              ("news_sources", r.sources, "count"), ("goldstein", r.goldstein, "index"), ("tone", r.tone, "index")),
            attributes=attributes(cameo_code=r.code if isinstance(r.code, str) else None,
                                  cameo_root=r.root if isinstance(r.root, str) else None, quad_class=quad,
                                  is_root_event=r.root_event == "1",
                                  actor1_code=r.a1code if isinstance(r.a1code, str) else None,
                                  actor1_name=r.a1name if isinstance(r.a1name, str) else None,
                                  actor2_code=r.a2code if isinstance(r.a2code, str) else None,
                                  actor2_name=r.a2name if isinstance(r.a2name, str) else None,
                                  geo_type=geotype, place_name=r.fullname if isinstance(r.fullname, str) else None, fips_country=r.cc),
            evidence={"url": r.url[:2000]} if isinstance(r.url, str) and r.url.startswith("http") else None)
