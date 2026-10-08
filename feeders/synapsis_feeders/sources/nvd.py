"""NVD CVE API 2.0: CVEs by publication date (backfill) and then by last modification (incremental runs).

Without an API key NVD allows 5 requests per 30 seconds, hence the 6.5 s pacing; set NVD_API_KEY to go faster. The
API limits a date-filtered query to 120 days, so the feeder walks forward in 120-day windows."""
from __future__ import annotations

from datetime import date, datetime, timedelta, timezone

from ..base import Checkpoint, Feeder, register
from ..sil import attributes, entity, event, measures, participant, text, time_spec, utc

API = "https://services.nvd.nist.gov/rest/json/cves/2.0"
FIRST = date(1999, 1, 1)
WINDOW = timedelta(days=119)


def _nvd_time(s: str) -> datetime:
    return utc(s[:19])


@register
class NvdFeeder(Feeder):
    name = "nvd"
    package = "nvd"
    title = "NIST National Vulnerability Database (CVE API 2.0)"
    min_interval = 6.5

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:nist:nvd")
        key = ctx.settings.key("NVD_API_KEY")
        if key:
            ctx.http.min_interval = 0.7
        headers = {"apiKey": key} if key else None
        incremental = ctx.cursor.get("backfill_done") and ctx.since is None
        field = "lastMod" if incremental else "pub"
        start_s = ctx.cursor.get("last_modified") if incremental else (ctx.cursor.get("last_published") if not ctx.since else None)
        start = utc(start_s) if start_s else utc(ctx.since or FIRST)
        end = utc(ctx.until) if ctx.until else ctx.started_at
        while start < end:
            stop = min(end, start + WINDOW)
            index, total = 0, None
            while total is None or index < total:
                body = ctx.http.json(API, headers=headers, params={
                    "resultsPerPage": 2000, "startIndex": index,
                    f"{field}StartDate": start.strftime("%Y-%m-%dT%H:%M:%S.000"), f"{field}EndDate": stop.strftime("%Y-%m-%dT%H:%M:%S.000")})
                total = body.get("totalResults", 0)
                for v in body.get("vulnerabilities", []):
                    yield from self._cve(v["cve"], src)
                index += body.get("resultsPerPage", 2000) or 2000
            cursor = dict(ctx.cursor)
            cursor["last_modified" if incremental else "last_published"] = stop.isoformat()
            if not incremental and stop >= end and ctx.since is None and ctx.until is None:
                cursor["backfill_done"] = True
                cursor.setdefault("last_modified", ctx.started_at.isoformat())
            ctx.cursor = cursor
            yield Checkpoint(cursor)
            start = stop

    def _cve(self, c: dict, src: dict):
        cid = c["id"]
        published, modified = _nvd_time(c["published"]), _nvd_time(c.get("lastModified") or c["published"])
        eid = f"ent:cve:{cid}"
        if self.ctx.once(eid):
            yield entity(eid, "Vulnerability", cid, src, published, "published")
        desc = next((d["value"] for d in c.get("descriptions", []) if d.get("lang") == "en"), None)
        yield event(f"evt:nvd:{cid}", "CYB.VULN.PUBLISHED", src, time_spec(published, "second"), {"unplaced": True}, published,
                    "published", participants=[participant(eid, "subject")], text=text(None, desc),
                    evidence={"url": f"https://nvd.nist.gov/vuln/detail/{cid}"})
        m = c.get("metrics", {})
        v31 = next((x for x in m.get("cvssMetricV31", []) if x.get("type") == "Primary"), (m.get("cvssMetricV31") or [None])[0])
        v40 = (m.get("cvssMetricV40") or [None])[0]
        cwes = sorted({d["value"] for w in c.get("weaknesses", []) for d in w.get("description", []) if d.get("lang") == "en"})
        aff = ((c.get("affected") or [{}])[0].get("affectedData") or [{}])[0] if c.get("affected") else {}
        yield event(f"evt:nvd:{cid}-assessment", "CYB.VULN.NVD_ASSESSMENT", src, time_spec(modified, "second"), {"unplaced": True},
                    modified, "published", participants=[participant(eid, "subject")],
                    measures=measures(("cvss_v31", v31["cvssData"]["baseScore"] if v31 else None, "score"),
                                      ("cvss_v40", v40["cvssData"]["baseScore"] if v40 else None, "score")),
                    attributes=attributes(status=c.get("vulnStatus"), cvss_v31_vector=v31["cvssData"]["vectorString"] if v31 else None,
                                          cvss_v40_vector=v40["cvssData"]["vectorString"] if v40 else None, cwes=cwes[:30],
                                          affected_vendor=(aff.get("vendor") or "")[:300] or None,
                                          affected_product=(aff.get("product") or "")[:300] or None))
