"""FDIC BankFind: quarterly financials for every insured bank (one request per quarter) and the full failure list.

Quarters run from --since (default 2006-03-31) to the last quarter FDIC has published, oldest first; a quarter that
returns no rows is not published yet and ends the run. A bank entity is sent with its first report and again whenever
its name or state changes, so a renamed bank gets a new version known when that quarter was published. Failures are
re-read every run: cost estimates change, and the API turns a changed failure into a new version."""
from __future__ import annotations

from datetime import date, datetime, timedelta

from ..base import Checkpoint, Feeder, register
from ..geo import us_subdivision
from ..sil import admin, attributes, entity, event, logical, measures, participant, place, relation, slug, time_spec, utc

FIN = "https://api.fdic.gov/banks/financials"
FAILURES = "https://api.fdic.gov/banks/failures"
FIELDS = "CERT,NAME,STALP,REPDTE,ASSET,DEP,DEPUNINS,BRO,COREDEP,EQ,ROA,NTLNLSR,NCLNLSR,SC,SCAA,SCAF,SCHA,SCHF,LNLSNET,RBC1AAJ,INTEXPY,NIMY"
MEASURES = [("assets", "ASSET", "usd_k"), ("deposits", "DEP", "usd_k"), ("deposits_uninsured", "DEPUNINS", "usd_k"),
            ("deposits_brokered", "BRO", "usd_k"), ("deposits_core", "COREDEP", "usd_k"), ("equity", "EQ", "usd_k"),
            ("roa", "ROA", "percent"), ("net_chargeoffs_ratio", "NTLNLSR", "percent"),
            ("noncurrent_loans_ratio", "NCLNLSR", "percent"), ("securities", "SC", "usd_k"),
            ("afs_amortized_cost", "SCAA", "usd_k"), ("afs_fair_value", "SCAF", "usd_k"),
            ("htm_amortized_cost", "SCHA", "usd_k"), ("htm_fair_value", "SCHF", "usd_k"), ("net_loans", "LNLSNET", "usd_k"),
            ("leverage_ratio", "RBC1AAJ", "percent"), ("interest_expense_ratio", "INTEXPY", "percent"),
            ("net_interest_margin", "NIMY", "percent")]
LAG = timedelta(days=45)


def quarter_ends(start: date, end: date):
    q = date(start.year, 3 * ((start.month - 1) // 3) + 3, 1)
    while q <= end:
        nxt = date(q.year + (q.month == 12), q.month % 12 + 1, 1)
        yield nxt - timedelta(days=1)
        q = date(q.year + (q.month + 3 > 12), (q.month + 2) % 12 + 1, 1)


def bank_id(cert) -> str:
    return f"ent:fdic:cert-{int(cert)}"


def failure_keys(r: dict) -> tuple[str, str, str | None]:
    """(bank entity id, record key, certificate) for one row of the failure list. 488 failures, nearly all from the
    1930s, have no FDIC certificate number: they are keyed by the failure list's own id, and their bank entity is the
    institution as that row names it."""
    cert = r.get("CERT")
    if cert not in (None, ""):
        return bank_id(cert), str(int(cert)), str(int(cert))
    fid = int(r["ID"])
    return f"ent:fdic:failure-{fid}", f"id{fid}", None


@register
class FdicFeeder(Feeder):
    name = "fdic"
    package = "fdic"
    title = "FDIC bank financials and failures"
    min_interval = 0.5

    def records(self):
        ctx = self.ctx
        cursor = dict(ctx.cursor)
        if ctx.options.get("failures", "yes") != "no":
            yield from self._failures(ctx.source("src:fdic:failures"))
            cursor["failures_loaded_at"] = ctx.started_at.isoformat()
            yield Checkpoint(dict(cursor))
        if ctx.options.get("financials", "yes") == "no":
            return
        src = ctx.source("src:fdic:financials")
        last = ctx.cursor.get("last_quarter")
        start = ctx.since or (date.fromisoformat(last) + timedelta(days=1) if last else date(2006, 1, 1))
        sent: dict[str, tuple] = {}     # bank id -> (name, state) last sent in this run
        end = ctx.until or (ctx.started_at.date() - LAG)
        for qend in quarter_ends(start, end):
            rows = self._quarter(qend)
            if not rows:
                ctx.log.info("quarter %s not published yet", qend)
                break
            qstart = date(qend.year, qend.month - 2, 1)
            known = utc(qend) + timedelta(days=1) + LAG
            unkeyed = [r for r in rows if r.get("CERT") in (None, "")]
            if unkeyed:     # a report without a certificate cannot be tied to a bank; say so instead of guessing
                ctx.log.warning("quarter %s: %d reports without a certificate number were not sent", qend, len(unkeyed))
            for r in sorted((r for r in rows if r.get("CERT") not in (None, "")), key=lambda x: int(x["CERT"])):
                bid = bank_id(r["CERT"])
                state = r.get("STALP") if isinstance(r.get("STALP"), str) else None
                described = (r.get("NAME") or bid, state)
                if sent.get(bid) != described:      # first report, or a new name or state (the quarter's batch is
                    sent[bid] = described           # flushed before the next quarter, so versions arrive in order)
                    yield entity(bid, "Bank", described[0], src, known, "proxy_publication_lag",
                                 attributes=attributes(state=state))
                yield event(
                    f"evt:fdic:{int(r['CERT'])}-{qend:%Y%m%d}", "FIN.BANK.QUARTER_REPORT", src, time_spec(qstart, "quarter"),
                    place(logical("finance.bank", "US", str(int(r["CERT"]))), admin("US", us_subdivision(state))),
                    known, "proxy_publication_lag", participants=[participant(bid, "subject")],
                    measures=measures(*[(m, r.get(f), u) for m, f, u in MEASURES]),
                    evidence={"url": f"https://banks.data.fdic.gov/bankfind-suite/bankfind/details/{int(r['CERT'])}", "ref": f"REPDTE {qend:%Y%m%d}"})
            cursor["last_quarter"] = qend.isoformat()
            yield Checkpoint(dict(cursor))

    def _quarter(self, qend: date) -> list[dict]:
        rows, offset = [], 0
        while True:
            body = self.ctx.http.json(FIN, params={"filters": f"REPDTE:{qend:%Y%m%d}", "fields": FIELDS, "limit": 10000,
                                                   "offset": offset, "sort_by": "CERT", "sort_order": "ASC"})
            page = [x["data"] for x in body.get("data", [])]
            rows += page
            offset += len(page)
            if not page or offset >= body.get("meta", {}).get("total", 0):
                return rows

    def _failures(self, src: dict):
        ctx = self.ctx
        body = ctx.http.json(FAILURES, params={"limit": 10000, "sort_by": "FAILDATE", "sort_order": "ASC"})
        for x in body.get("data", []):
            r = x["data"]
            try:
                failed = datetime.strptime(r["FAILDATE"], "%m/%d/%Y").date()
            except (KeyError, ValueError, TypeError):
                continue
            if (ctx.since and failed < ctx.since) or (ctx.until and failed > ctx.until):
                continue
            known = utc(failed) + timedelta(days=1)
            bid, key, cert = failure_keys(r)
            state = r.get("PSTALP") or (r.get("CITYST", "")[-2:] if r.get("CITYST") else None)
            if ctx.once(f"{bid}|failures"):
                yield entity(bid, "Bank", r.get("NAME") or bid, src, known, "published",
                             attributes=attributes(state=state if state and len(state) == 2 else None,
                                                   charter_class=r.get("CHCLASS1") or None))
            kind = "FIN.BANK.ASSISTANCE" if r.get("RESTYPE") == "ASSISTANCE" else "FIN.BANK.FAILURE"
            cost_asof = (r.get("COSTMOSTRECENTASOF") or "")[:10]
            eid = f"evt:fdic:failure-{key}-{failed:%Y%m%d}"
            yield event(
                eid, kind, src, time_spec(failed, "day"),
                place(logical("finance.bank", "US", cert) if cert else None,
                      admin("US", us_subdivision(state), name=(r.get("CITY") or "").title() or None)),
                known, "published", participants=[participant(bid, "subject")],
                measures=measures(("assets_at_failure", r.get("QBFASSET"), "usd_k"), ("deposits_at_failure", r.get("QBFDEP"), "usd_k"),
                                  ("estimated_cost", r.get("COST"), "usd_k")),
                attributes=attributes(resolution=r.get("RESTYPE1") or None, insurance_fund=r.get("SAVR") or None,
                                      charter_class=r.get("CHCLASS1") or None,
                                      cost_as_of=cost_asof if len(cost_asof) == 10 else None,
                                      acquirer=(r.get("BIDNAME") or "")[:300] or None))
            if r.get("BIDNAME"):
                aid = f"ent:fdic-bidder:{slug(r['BIDNAME'], 90)}-{(r.get('BIDSTATE') or 'xx').lower()}"
                if ctx.once(aid):
                    yield entity(aid, "Organization", r["BIDNAME"], src, known, "published")
                yield relation(f"rel:fdic:acquired-{key}-{failed:%Y%m%d}", "ACQUIRED", src, aid, bid, known, "published",
                               time=time_spec(failed, "day"))
