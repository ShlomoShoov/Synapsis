"""Johns Hopkins CSSE global confirmed-case time series (archived March 2023): one file of cumulative counts.

Daily new cases = today's cumulative minus yesterday's. Negative differences are corrections; they are sent as they
are and rejected by the API, so the rejection log is the list of corrections (round 2 counted 362)."""
from __future__ import annotations

import io

import pandas as pd

from ..base import Checkpoint, Feeder, register
from ..geo import country_iso2
from ..sil import admin, event, logical, measures, place, slug, time_spec, utc
from datetime import timedelta

URL = ("https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/"
       "csse_covid_19_time_series/time_series_covid19_confirmed_global.csv")


@register
class JhuCovidFeeder(Feeder):
    name = "jhu-covid"
    package = "jhu-covid"
    requires = ("health",)
    title = "Johns Hopkins CSSE COVID-19 confirmed cases (global)"

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:jhu:csse-covid")
        df = pd.read_csv(io.BytesIO(ctx.http.bytes(URL, cache_name="time_series_covid19_confirmed_global.csv")))
        dates = [c for c in df.columns[4:]]
        days = pd.to_datetime(dates, format="%m/%d/%y")
        unmapped = set()
        for _, row in df.iterrows():
            country, province = str(row["Country/Region"]).strip(), row["Province/State"]
            province = str(province).strip() if isinstance(province, str) and province.strip() else None
            iso2 = country_iso2(country)
            if iso2 is None:
                unmapped.add(country)
            region_key = f"{slug(country)}/{slug(province) if province else 'all'}"
            cum = pd.to_numeric(row[dates], errors="coerce").to_numpy()
            prev = 0.0
            for day, value in zip(days, cum):
                if pd.isna(value):
                    continue
                new = float(value) - prev
                prev = float(value)
                d = day.date()
                if (ctx.since and d < ctx.since) or (ctx.until and d > ctx.until):
                    continue
                yield event(
                    f"evt:jhu-covid:{region_key}-{d:%Y%m%d}", "HLT.SIGNAL.DAILY", src, time_spec(d, "day"),
                    place(logical("health.region", country, province or "(all)"),
                          admin(iso2, name=province) if iso2 else None),
                    utc(d) + timedelta(days=1), "proxy_publication_lag", measures=measures(("cases", new, "count")))
        if unmapped:
            ctx.log.info("no ISO country for %s (kept with their JHU names only)", sorted(unmapped))
        yield Checkpoint({"file": URL, "regions": int(len(df)), "days": len(dates)})
