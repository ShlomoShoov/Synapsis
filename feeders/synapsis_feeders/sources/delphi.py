"""Delphi Epidata covidcast: five daily COVID-19 signals for every US state and territory.

One request per signal per six months; the cursor keeps the last day loaded for each signal. The signals were
archived in 2023, so the default range is February 2020 to March 2023 and the feeder runs once."""
from __future__ import annotations

from datetime import date, timedelta

from ..base import Checkpoint, Feeder, register
from ..geo import us_subdivision
from ..sil import admin, attributes, event, logical, measures, place, time_spec, utc

API = "https://api.delphi.cmu.edu/epidata/covidcast/"
SIGNALS = [  # (data_source, signal, measure, unit, source id)
    ("hhs", "confirmed_admissions_covid_1d", "admissions", "count", "src:delphi:hhs-admissions"),
    ("jhu-csse", "confirmed_incidence_num", "cases", "count", "src:delphi:jhu-cases"),
    ("doctor-visits", "smoothed_adj_cli", "cli_share", "percent", "src:delphi:doctor-visits"),
    ("google-symptoms", "s05_smoothed_search", "symptom_search", "index", "src:delphi:google-symptoms"),
    ("fb-survey", "smoothed_wcli", "wcli_share", "percent", "src:delphi:fb-survey"),
]
SHORT = {"hhs": "adm", "jhu-csse": "cases", "doctor-visits": "cli", "google-symptoms": "smell", "fb-survey": "survey"}
DEFAULT_START, DEFAULT_END = date(2020, 2, 1), date(2023, 3, 1)


def _d(v) -> date:
    s = str(v)
    return date(int(s[:4]), int(s[4:6]), int(s[6:8]))


@register
class DelphiFeeder(Feeder):
    name = "delphi"
    package = "delphi"
    requires = ("health",)
    title = "Delphi Epidata COVID-19 signals (US states)"
    min_interval = 1.0

    def records(self):
        ctx = self.ctx
        cursor = dict(ctx.cursor)
        for data_source, signal, measure, unit, source_id in SIGNALS:
            src = ctx.source(source_id)
            lag = timedelta(days=src["publication_lag_days"])
            done = cursor.get(signal)
            start = ctx.since or (date.fromisoformat(done) + timedelta(days=1) if done else DEFAULT_START)
            end = ctx.until or DEFAULT_END
            while start <= end:
                stop = min(end, date(start.year + (start.month > 6), (start.month + 5) % 12 + 1, 1) - timedelta(days=1))
                body = ctx.http.json(API, params={"data_source": data_source, "signals": signal, "time_type": "day",
                                                  "geo_type": "state", "geo_values": "*",
                                                  "time_values": f"{start:%Y%m%d}-{stop:%Y%m%d}"})
                if body.get("result") not in (1, -2):          # -2 = no results in this window
                    raise RuntimeError(f"Delphi answered {body.get('result')}: {body.get('message')}")
                for r in sorted(body.get("epidata") or [], key=lambda x: (x["time_value"], x["geo_value"])):
                    sub = us_subdivision(r["geo_value"])
                    m = measures((measure, r.get("value"), unit))
                    if not sub or not m:
                        continue                              # M-NULL: a missing value is not sent
                    day = _d(r["time_value"])
                    yield event(
                        f"evt:delphi:{SHORT[data_source]}-{r['geo_value']}-{day:%Y%m%d}", "HLT.SIGNAL.DAILY", src,
                        time_spec(day, "day"), place(admin("US", sub), logical("health.signal", data_source, signal)),
                        utc(day) + lag, "proxy_publication_lag", measures=m,
                        attributes=attributes(issue=_d(r["issue"]).isoformat() if r.get("issue") else None,
                                              lag_days=int(r["lag"]) if r.get("lag") is not None else None))
                cursor[signal] = stop.isoformat()
                yield Checkpoint(dict(cursor))
                start = stop + timedelta(days=1)
