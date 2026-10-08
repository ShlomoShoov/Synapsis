"""EPSS daily score files (epss_scores-YYYY-MM-DD.csv.gz), the first of each month by default.

A file holds every scored CVE (about 300,000 in 2026). Each CVE becomes an event; the CVE entity is sent the first
time this feeder sees it. The ids already sent are kept in the feeder's memo, which --reset clears together with the
cursor, so a reloaded database always receives the entities again."""
from __future__ import annotations

import gzip
import io
from datetime import date, timedelta

import pandas as pd

from ..base import Checkpoint, Feeder, register
from ..sil import attributes, entity, event, measures, participant, time_spec, utc

URLS = ["https://epss.empiricalsecurity.com/epss_scores-{d}.csv.gz", "https://epss.cyentia.com/epss_scores-{d}.csv.gz"]
FIRST = date(2022, 2, 1)


def dates(start: date, end: date, monthly: bool):
    d = date(start.year, start.month, 1) if monthly and start.day != 1 else start
    if monthly and d < start:
        d = date(d.year + (d.month == 12), d.month % 12 + 1, 1)
    while d <= end:
        yield d
        d = date(d.year + (d.month == 12), d.month % 12 + 1, 1) if monthly else d + timedelta(days=1)


@register
class EpssFeeder(Feeder):
    name = "epss"
    package = "epss"
    title = "EPSS exploit prediction scores"
    min_interval = 0.5

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:first:epss")
        monthly = ctx.options.get("every", "month") != "day"
        last = ctx.cursor.get("last_date")
        start = ctx.since or (date.fromisoformat(last) + timedelta(days=1) if last else FIRST)
        end = ctx.until or (ctx.started_at.date() - timedelta(days=1))
        known = set(ctx.load_memo("cves_sent", []))
        for d in dates(start, end, monthly):
            raw = None
            for u in URLS:
                raw = ctx.http.bytes(u.format(d=d.isoformat()), ok_missing=True)
                if raw:
                    break
            if not raw:
                ctx.log.warning("no EPSS file for %s", d)
                continue
            text = gzip.decompress(raw).decode("utf-8")
            header = text.split("\n", 1)[0]
            meta = dict(kv.split(":", 1) for kv in header.lstrip("#").split(",") if ":" in kv)
            scored = utc(meta.get("score_date", f"{d}T00:00:00Z"))
            df = pd.read_csv(io.StringIO(text), comment="#")
            new_ids = []
            for r in df.itertuples(index=False):
                cve = str(r.cve).strip().upper()
                eid = f"ent:cve:{cve}"
                if cve not in known:
                    yield entity(eid, "Vulnerability", cve, src, scored, "published")
                    new_ids.append(cve)
                yield event(f"evt:epss:{cve}-{d:%Y%m%d}", "CYB.VULN.EPSS_SCORE", src, time_spec(d, "day"), {"unplaced": True},
                            scored, "published", participants=[participant(eid, "subject")],
                            measures=measures(("epss", r.epss, "probability"), ("epss_percentile", r.percentile, "probability")),
                            attributes=attributes(model_version=meta.get("model_version")))
            yield Checkpoint({"last_date": d.isoformat()})
            known.update(new_ids)                       # the batches holding these entities are acknowledged
            ctx.save_memo("cves_sent", sorted(known))
