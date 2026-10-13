"""F6 Polls x election results (US presidential, states). Round-2 'routine' applied to poll error: each state's
polling bias learned ONLY from previous elections corrects the current polls. Plus poll-level sparks."""
import re, io, os, time, json, urllib.request
import numpy as np, pandas as pd
from bs4 import BeautifulSoup
from synapsis_r3.core import validate_bulk, scores
STATES = ["Alabama","Alaska","Arizona","Arkansas","California","Colorado","Connecticut","Delaware","District of Columbia","Florida","Georgia","Hawaii","Idaho","Illinois","Indiana","Iowa","Kansas","Kentucky","Louisiana","Maine","Maryland","Massachusetts","Michigan","Minnesota","Mississippi","Missouri","Montana","Nebraska","Nevada","New Hampshire","New Jersey","New Mexico","New York","North Carolina","North Dakota","Ohio","Oklahoma","Oregon","Pennsylvania","Rhode Island","South Carolina","South Dakota","Tennessee","Texas","Utah","Vermont","Virginia","Washington","West Virginia","Wisconsin","Wyoming"]
DEM = {2016: "Clinton", 2020: "Biden", 2024: "Harris"}; EDAY = {2016: "2016-11-08", 2020: "2020-11-03", 2024: "2024-11-05"}
MON = {m: i for i, m in enumerate(["jan","feb","mar","apr","may","jun","jul","aug","sep","oct","nov","dec"], 1)}
UA = {"User-Agent": "synapsis-r3-research/0.1 (public data study)"}
def end_date(s, y):
    if not isinstance(s, str): return pd.NaT
    cur, last = None, None
    for mo, d1, d2 in re.findall(r"([A-Z][a-z]{2,8})\.?\s+(\d{1,2})|[–\-]\s*(\d{1,2})\b", s):
        if mo and mo[:3].lower() in MON: cur = MON[mo[:3].lower()]; last = (cur, int(d1))
        elif d2 and cur: last = (cur, int(d2))
    ys = re.findall(r"(20\d\d)", s); yr = int(ys[-1]) if ys else y
    try: return pd.Timestamp(yr, last[0], last[1]) if last else pd.NaT
    except Exception: return pd.NaT
pct = lambda s: pd.to_numeric(s.fillna("").astype(str).str.extract(r"(\d+(?:\.\d+)?)")[0], errors="coerce")
def parse_table(tab, y):
    try: df = pd.read_html(io.StringIO(str(tab)))[0]
    except Exception: return None
    cols = [" ".join(dict.fromkeys(map(str, c))) if isinstance(c, tuple) else str(c) for c in df.columns]; df.columns = cols
    if any("aggregation" in c.lower() or "updated" in c.lower() for c in cols): return None
    datec = [c for c in cols if "date" in c.lower()]
    if not datec: return None
    rc = [c for c in cols if "Trump" in c]; dc = [c for c in cols if DEM[y] in c]
    if rc and dc: R, D = df[rc[0]], df[dc[0]]
    elif "Democrat" in cols and "Republican" in cols:
        df = df[df["Democrat"].fillna("").astype(str).str.contains(DEM[y]) & df["Republican"].fillna("").astype(str).str.contains("Trump")]
        R, D = df[cols[cols.index("Republican") + 1]], df[cols[cols.index("Democrat") + 1]]
    else: return None
    out = pd.DataFrame({"end": df[datec[0]].fillna("").astype(str).map(lambda x: end_date(x, y)).values, "R": pct(R).values, "D": pct(D).values,
                        "src": df[cols[0]].fillna("").astype(str).str.replace(r"\[.*?\]", "", regex=True).str.strip().values})
    return out.dropna()
def polls_from(soup, y, state=None):
    rows, cur = [], state
    for el in soup.select("h2, h3, h4, table.wikitable"):
        if el.name in ("h2", "h3", "h4") and state is None:
            t = re.sub(r"\[.*?\]", "", el.get_text(" ", strip=True)).strip()
            if el.name == "h2": cur = t if t in STATES else None
            elif "district" in t.lower(): cur = None if cur and t != cur else cur
        elif el.name == "table" and cur:
            p = parse_table(el, y)
            if p is not None and len(p): rows.append(p.assign(state=cur))
    return pd.concat(rows) if rows else pd.DataFrame()
P = []
for y in (2016, 2024):
    P.append(polls_from(BeautifulSoup(open(f"data/elections/polls_{y}.html", "rb").read(), "lxml"), y).assign(year=y))
for st in STATES:
    f = f"data/elections/2020_{st.replace(' ', '_')}.html"
    if not os.path.exists(f):
        name = ("the_" if st == "District of Columbia" else "") + st.replace(" ", "_")
        try: open(f, "wb").write(urllib.request.urlopen(urllib.request.Request(f"https://en.wikipedia.org/wiki/2020_United_States_presidential_election_in_{name}", headers=UA), timeout=90).read()); time.sleep(0.7)
        except Exception as e: print("miss", st, e); continue
    P.append(polls_from(BeautifulSoup(open(f, "rb").read(), "lxml"), 2020, state=st).assign(year=2020))
P = pd.concat(P, ignore_index=True)
P["eday"] = pd.to_datetime(P.year.map(EDAY)); P = P[(P.end < P.eday) & (P.end >= P.eday - pd.Timedelta(days=30)) & P.R.between(1, 99) & P.D.between(1, 99)]
P = P.drop_duplicates(["year", "state", "src", "end"]); P["margin"] = P.R - P.D
def results(y):
    s = BeautifulSoup(open(f"data/elections/results_{y}.html", "rb").read(), "lxml")
    for t in s.select("table.wikitable"):
        if "Wyoming" in t.get_text() and "Alabama" in t.get_text():
            df = pd.read_html(io.StringIO(str(t)))[0]
            cols = [" ".join(dict.fromkeys(map(str, c))) if isinstance(c, tuple) else str(c) for c in df.columns]; df.columns = cols
            rc = [c for c in cols if "Trump" in c and "%" in c]; dc = [c for c in cols if DEM[y] in c and "%" in c]
            if not (rc and dc): continue
            nm = df[cols[0]].fillna("").astype(str).str.replace(r"\[.*?\]|†|\*", "", regex=True).str.strip()
            st = nm.map(lambda x: next((S for S in STATES if x == S or (x.startswith(S) and "at-l" in x.lower())), None))
            r = pd.DataFrame({"state": st, "R": pct(df[rc[0]]), "D": pct(df[dc[0]])}).dropna().drop_duplicates("state")
            if len(r) >= 45: return r.assign(year=y, margin=lambda d: d.R - d.D)
Rs = pd.concat([results(y) for y in (2016, 2020, 2024)])
# ---- SIL ingestion (signed measure 'margin' allowed by the registry)
def sil(df, kind, ns, src, lagd, tcol):
    t = pd.to_datetime(df[tcol]).dt.normalize()
    S = pd.DataFrame({"id": f"evt:{ns}:" + pd.Series(range(len(df))).fillna("").astype(str).values, "type": kind, "t_start": t.values, "precision": "day",
                      "known_at": (t + pd.Timedelta(days=lagd)).values, "known_at_basis": "published", "source": src, "reliability": 80,
                      "reliability_basis": "B2", "lspace": "admin", "lpath": ("US/" + df.state.str.replace("/", "-")).values, "measure": "margin", "mvalue": df.margin.values})
    a, r = validate_bulk(S, ns); return [len(S), len(a), r.code.value_counts().to_dict() if len(r) else {}]
R = {"case": "F6_elections", "ingest": {"polls": sil(P, "POL.POLL.STATE", "wikipolls", "src:wikipedia:polls", 2, "end"),
                                         "results": sil(Rs.assign(d=pd.to_datetime(Rs.year.map(EDAY))), "POL.ELECTION.RESULT", "wikiresults", "src:wikipedia:results", 1, "d")},
     "polls_final30d": P.groupby("year").size().to_dict(), "states_with_polls": P.groupby("year").state.nunique().to_dict()}
# ---- forecasts: polls-only vs polls + national bias vs polls + state routine bias (shrunk) vs + prior blend
avg = P.groupby(["year", "state"]).agg(poll=("margin", "mean"), n=("margin", "size")).reset_index()
D = avg.merge(Rs[["year", "state", "margin"]], on=["year", "state"]); D["err"] = D.margin - D.poll
prev = Rs.set_index(["year", "state"]).margin
out = {}
for y, hist in ((2020, [2016]), (2024, [2016, 2020])):
    H = D[D.year.isin(hist)]; nat = H.groupby("year").err.mean().mean()
    sb = H.groupby("state").err.agg(["sum", "size"]); kappa = 2.0
    bias = ((sb["sum"] + kappa * nat) / (sb["size"] + kappa)).reindex(D[D.year == y].state).fillna(nat).values
    T = D[D.year == y].copy(); T["prior"] = T.state.map(lambda s: prev.get((y - 4, s), np.nan)) + (T.poll.mean() - pd.Series([prev.get((y - 4, s), np.nan) for s in T.state]).mean())
    f = {"polls_only": T.poll.values, "polls+national_bias": T.poll.values + nat, "polls+state_routine_bias": T.poll.values + bias}
    w = T.n / (T.n + 3.0); f["routine_bias+prior_blend"] = w * (T.poll + bias) + (1 - w) * T.prior.fillna(T.poll + bias)
    res = {}
    for k, v in f.items():
        e = T.margin.values - v; sw = np.abs(T.margin.values) < 8
        res[k] = {"mae": round(float(np.mean(np.abs(e))), 2), "mae_swing_states": round(float(np.mean(np.abs(e[sw]))), 2),
                  "winner_correct": int(np.sum(np.sign(v) == np.sign(T.margin.values))), "of": len(T), "mean_signed_error": round(float(np.mean(e)), 2)}
    out[y] = {"national_bias_used": round(float(nat), 2), "results": res,
              "bias_persistence_corr": round(float(np.corrcoef(*[D[(D.year == a)].set_index("state").err.reindex(T.state).values for a in (hist[-1], y)])[0, 1]), 3) if True else None}
R["forecast"] = out
# ---- poll-level sparks in 2024: surprise of each poll vs the state's other final-month polls (leave-one-out)
P24 = P[P.year == 2024].copy(); sp = []
for st, g in P24.groupby("state"):
    if len(g) < 5: continue
    for i, r in g.iterrows():
        o = g.drop(i); z = (r.margin - o.margin.mean()) / max(o.margin.std(), 2.0); sp.append((abs(z), z, st, r.src[:40], str(r.end.date()), r.margin, round(o.margin.mean(), 1)))
sp.sort(reverse=True); res24 = Rs[Rs.year == 2024].set_index("state").margin
R["sparks_2024_top"] = [{"state": s, "poll": p, "end": e, "poll_margin_R_minus_D": m, "others_mean": om, "z": round(z, 2), "result": float(res24.get(s, np.nan))} for _, z, s, p, e, m, om in sp[:6]]
json.dump(R, open("results/f6_elections.json", "w"), indent=1, default=str)
print(json.dumps({k: R[k] for k in ("ingest", "polls_final30d", "states_with_polls")}, default=str))
for y in out: print(y, "nat_bias", out[y]["national_bias_used"], "persist_corr", out[y]["bias_persistence_corr"]); [print("  ", k, v) for k, v in out[y]["results"].items()]
for s in R["sparks_2024_top"]: print("  spark", s)
