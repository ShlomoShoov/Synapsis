"""FDIC: every year-end 2006-2023, all banks ranked by two lenses (joint = the weaker of two robust z-scores), checked against
the failures of the next 12 months; plus the SVB profile. Public FDIC API, no key. Run: python fdic_lenses.py"""
import json, urllib.request, urllib.parse, statistics as st, datetime as dt
UA = {"User-Agent": "synapsis-check"}
def get(url, params):
    return json.load(urllib.request.urlopen(urllib.request.Request(url + "?" + urllib.parse.urlencode(params), headers=UA), timeout=180))
fails = [d["data"] for d in get("https://api.fdic.gov/banks/failures", {"limit": 10000, "fields": "CERT,NAME,FAILDATE"})["data"]]
fail_at = {}
for f in fails:
    try: fail_at[int(f["CERT"])] = dt.datetime.strptime(f["FAILDATE"], "%m/%d/%Y")
    except (TypeError, ValueError, KeyError): pass
F = "CERT,NAME,DEP,DEPUNINS,EQ,SCAA,SCAF,SCHA,SCHF,NCLNLSR,ROA,ASSET"
def num(r, k):
    v = r.get(k); return None if v is None else float(v)
def rz(xs):
    m = st.median(xs); mad = st.median([abs(x - m) for x in xs]) * 1.4826 or 1e-9
    return lambda x: (x - m) / mad
def auc(scores, pos):
    order = sorted(range(len(scores)), key=lambda i: -scores[i]); P = sum(pos); Nn = len(pos) - P
    if P == 0 or Nn == 0: return None
    seen_neg = 0; tot = 0
    for i in order:
        if pos[i]: tot += Nn - seen_neg
        else: seen_neg += 1
    return tot / (P * Nn)
print("year  banks fail12m | lens A (uninsured x unrealized loss): in top50  auc | lens B (bad loans x low ROA): in top50  auc")
for y in range(2006, 2024):
    rows = [d["data"] for d in get("https://api.fdic.gov/banks/financials", {"filters": f"REPDTE:{y}1231", "fields": F, "limit": 10000})["data"]]
    known = dt.datetime(y + 1, 2, 14)            # year-end reports are public about six weeks later
    banks = []
    for r in rows:
        dep, eq = num(r, "DEP"), num(r, "EQ")
        if not dep or not eq or dep <= 0 or eq <= 0: continue
        c = int(r["CERT"]); fa = fail_at.get(c)
        failed = fa is not None and known <= fa < known + dt.timedelta(days=365)
        u = num(r, "DEPUNINS"); u = None if u is None else u / dep
        loss = ((num(r, "SCAA") or 0) - (num(r, "SCAF") or 0) + (num(r, "SCHA") or 0) - (num(r, "SCHF") or 0)) / eq
        banks.append(dict(cert=c, name=r["NAME"], u=u, loss=loss, ncl=num(r, "NCLNLSR"), roa=num(r, "ROA"), failed=failed))
    out = f"{y}  {len(banks):5d} {sum(b['failed'] for b in banks):5d}   |"
    for lens, keys, signs in (("A", ("u", "loss"), (1, 1)), ("B", ("ncl", "roa"), (1, -1))):
        bs = [b for b in banks if all(b[k] is not None for k in keys)]
        if len(bs) < 100: out += f" lens {lens}: no data          |"; continue
        zs = [rz([s * b[k] for b in bs]) for k, s in zip(keys, signs)]
        sc = [min(z(s * b[k]) for z, k, s in zip(zs, keys, signs)) for b in bs]
        pos = [b["failed"] for b in bs]
        top = sorted(range(len(bs)), key=lambda i: -sc[i])[:50]
        a = auc(sc, pos)
        out += f" {sum(pos[i] for i in top):3d} of {sum(pos):3d}  auc {a if a is None else round(a, 3)} |"
    print(out, flush=True)
