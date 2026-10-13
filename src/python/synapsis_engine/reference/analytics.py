"""Pattern engine: every subject carries an expectation; sparks are forecast errors; forecasts are expectations.

Subjects live in the lattice (H3 cells, logical nodes, admin regions, entities). All models are causal:
the expectation for bucket t uses only buckets < t, so any `as_of` rollback reproduces exactly what the
engine would have said at that moment.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field

import h3
import numpy as np
import pandas as pd
from scipy.signal import lfilter
from scipy.stats import poisson

FREQ = {"D": "D", "W": "W-MON", "M": "MS", "Q": "QS"}


@dataclass
class Series:
    subjects: list            # subject keys (column order)
    buckets: pd.DatetimeIndex # bucket starts (row order)
    C: np.ndarray             # counts  [T x S]
    W: np.ndarray             # weights [T x S] (e.g. magnitude-weighted or case counts); == C when unweighted
    coords: dict = field(default_factory=dict)  # subject -> (lat, lon) when known
    spec: dict = field(default_factory=dict)


def build_series(store, spec: dict) -> Series:
    """spec: source, type_prefix, subject (column name or 'participant:<role>'), bucket, start, end, weight, min_events."""
    where = "source = ? AND type LIKE ?"
    params = [spec["source"], spec.get("type_prefix", "") + "%"]
    ev = store.events(where, params=params, as_of=spec.get("as_of"))
    if spec.get("filter"):
        ev = ev.query(spec["filter"])
    subj = spec["subject"]
    if subj.startswith("participant:"):
        role = subj.split(":")[1]
        parts = store.con.execute("SELECT event_id, entity_id FROM participants WHERE role = ?", [role]).df()
        ev = ev.merge(parts, left_on="id", right_on="event_id")
        ev["subject"] = ev["entity_id"]
    else:
        ev["subject"] = ev[subj]
    ev = ev[ev["subject"].notna()].copy()
    freq = FREQ[spec["bucket"]]
    t0 = ev["t_start"].dt.tz_convert(None)
    ev["bucket"] = {"D": t0.dt.floor("D"), "W": t0.dt.to_period("W-SUN").dt.start_time,
                    "M": t0.dt.to_period("M").dt.start_time, "Q": t0.dt.to_period("Q").dt.start_time}[spec["bucket"]]
    start, end = pd.Timestamp(spec["start"]), pd.Timestamp(spec["end"])
    ev = ev[(ev["bucket"] >= start) & (ev["bucket"] <= end)]
    counts = ev.groupby("subject").size()
    keep = counts[counts >= spec.get("min_events", 1)].index
    ev = ev[ev["subject"].isin(keep)]
    buckets = pd.date_range(start, end, freq=freq)
    subjects = sorted(keep)
    si = {s: i for i, s in enumerate(subjects)}
    bi = {b: i for i, b in enumerate(buckets)}
    C = np.zeros((len(buckets), len(subjects)), dtype=np.float64)
    Wt = np.zeros_like(C)
    w = spec.get("weight")
    for r in ev[["bucket", "subject", "measures"]].itertuples(index=False):
        t = bi.get(r.bucket)
        if t is None:
            continue
        C[t, si[r.subject]] += 1
        if w:
            Wt[t, si[r.subject]] += float(json.loads(r.measures).get(w, 0.0))
    if not w:
        Wt = C.copy()
    coords = {}
    if "lat" in ev:
        g = ev.dropna(subset=["lat"]).groupby("subject")[["lat", "lon"]].median()
        coords = {s: (r.lat, r.lon) for s, r in g.iterrows()}
    return Series(subjects, buckets, C, Wt, coords, spec)


def neighbor_matrix(series: Series, mode: str, k: int = 6) -> np.ndarray:
    """Row-normalized adjacency between subjects: H3 ring-1, or k nearest by great-circle distance."""
    S = len(series.subjects)
    N = np.zeros((S, S))
    idx = {s: i for i, s in enumerate(series.subjects)}
    if mode == "h3":
        for s, i in idx.items():
            for nb in h3.grid_disk(s, 1):
                j = idx.get(nb)
                if j is not None and j != i:
                    N[i, j] = 1.0
    elif mode == "knn" and series.coords:
        keys = [s for s in series.subjects if s in series.coords]
        lat = np.radians([series.coords[s][0] for s in keys]); lon = np.radians([series.coords[s][1] for s in keys])
        d = np.arccos(np.clip(np.sin(lat)[:, None] * np.sin(lat) + np.cos(lat)[:, None] * np.cos(lat) * np.cos(lon[:, None] - lon), -1, 1))
        for a, s in enumerate(keys):
            for b in np.argsort(d[a])[1:k + 1]:
                N[idx[s], idx[keys[b]]] = 1.0
    # Fixed weights (rule T-UNIVERSE): 1/6 per hexagon neighbour, 1/k per k-NN neighbour, whether or not the
    # neighbour has data yet — so adding a future subject never changes the weights of existing ones.
    return N / (6.0 if mode == "h3" else float(k))


# ------------------------------------------------------------------------------------- expectation models
def background(C: np.ndarray, alpha: float = 50.0) -> np.ndarray:
    """Causal, shrunk long-run rate per subject: mu[t] uses buckets < t only.
    The global prior divides by the number of subjects SEEN BEFORE t (rule T-UNIVERSE): the subject universe
    is itself knowledge, so it must not include subjects that only appear later."""
    T, S = C.shape
    cum = np.vstack([np.zeros((1, S)), np.cumsum(C, 0)[:-1]])            # counts strictly before t
    n = np.arange(T)[:, None].astype(float)                                # buckets strictly before t
    seen = (cum > 0).sum(1, keepdims=True)                                 # subjects known before t
    glob = cum.sum(1, keepdims=True) / np.maximum(n * np.maximum(seen, 1), 1)
    return (cum + alpha * glob) / (n + alpha)


def excitation(C: np.ndarray, N: np.ndarray | None, decay: float, gamma: float) -> np.ndarray:
    """Y[t] = sum_{k>=1} decay^(k-1) * (C[t-k] + gamma * neighbors(C[t-k])) — causal exponential memory."""
    X = C + (gamma * (C @ N.T) if (N is not None and gamma > 0) else 0.0)
    return lfilter([0.0, 1.0], [1.0, -decay], X, axis=0)


def expectations(C: np.ndarray, model: str, N=None, params=None) -> np.ndarray:
    mu = background(C)
    if model == "M0":                      # global rate, same for every subject
        cum = np.vstack([np.zeros((1, C.shape[1])), np.cumsum(C, 0)[:-1]])
        seen = np.maximum((cum > 0).sum(1, keepdims=True), 1)
        glob = cum.sum(1, keepdims=True) / np.maximum(np.arange(C.shape[0])[:, None] * seen, 1)
        return np.repeat(glob, C.shape[1], 1) + 1e-9
    if model == "M1":                      # subject routine (long-run)
        return mu + 1e-9
    if model == "M2":                      # routine blended with recency (EWMA)
        ew = excitation(C, None, 0.8, 0.0) * 0.2
        return 0.5 * mu + 0.5 * ew + 1e-9
    if model == "M3":                      # routine + self-excitation + lattice contagion (Hawkes-type)
        a, decay, gamma = params
        return mu + a * excitation(C, N, decay, gamma) + 1e-9
    raise ValueError(model)


def poisson_ll(C, lam, rows) -> float:
    """Mean Poisson log-likelihood per subject-bucket (constant term omitted: identical across models)."""
    c, l = C[rows], lam[rows]
    return float(np.mean(c * np.log(l) - l))


def fit_m3(C, N, rows, grid=None):
    """Grid-search MLE of (a, decay, gamma) on calibration rows (Poisson log-likelihood)."""
    grid = grid or dict(a=[0.02, 0.05, 0.1, 0.2, 0.4, 0.7], decay=[0.3, 0.5, 0.7, 0.85, 0.95], gamma=[0.0, 0.1, 0.3, 0.6])
    best = (-np.inf, None)
    mu = background(C)
    for decay in grid["decay"]:
        for gamma in grid["gamma"]:
            Y = excitation(C, N, decay, gamma)
            for a in grid["a"]:
                ll = poisson_ll(C, mu + a * Y + 1e-9, rows)
                if ll > best[0]:
                    best = (ll, (a, decay, gamma))
    return best[1], best[0]


# ------------------------------------------------------------------------------------- sparks
def surprise(C: np.ndarray, lam: np.ndarray) -> np.ndarray:
    """-log10 P(N >= c | lam): bits-like surprise of observing at least c events (0 when c == 0)."""
    s = -np.log10(np.clip(poisson.sf(C - 1, lam), 1e-300, 1.0))
    s[C == 0] = 0.0
    return s


def absence(C: np.ndarray, lam: np.ndarray) -> np.ndarray:
    """-log10 P(N = 0 | lam) where nothing happened: the dog that didn't bark."""
    a = lam / np.log(10)
    a[C > 0] = 0.0
    return a


def threshold_for_budget(S: np.ndarray, rows, budget: float) -> float:
    """Spark threshold so that at most `budget` of active subject-buckets spark in the calibration rows."""
    vals = S[rows][S[rows] > 0]
    return float(np.quantile(vals, 1 - budget)) if len(vals) else np.inf


def sparks_table(series: Series, S: np.ndarray, lam: np.ndarray, thr: float) -> pd.DataFrame:
    t, s = np.nonzero(S >= thr)
    return pd.DataFrame({"bucket": series.buckets[t], "subject": [series.subjects[i] for i in s],
                         "observed": series.C[t, s], "expected": lam[t, s].round(3), "surprise": S[t, s].round(2),
                         "t": t, "s": s})


def link_sparks(sp: pd.DataFrame, N: np.ndarray, max_lag: int) -> pd.DataFrame:
    """Connect sparks that are close in time (<= max_lag buckets) and space (same or neighboring subject).
    Returns sparks with a constellation id (union-find), i.e. the connections between anomalies."""
    sp = sp.sort_values("t").reset_index(drop=True)
    parent = list(range(len(sp)))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    by_t = {}
    for i, r in sp.iterrows():
        by_t.setdefault(r.t, []).append(i)
    adj = (N > 0)
    for i, r in sp.iterrows():
        for dt in range(0, max_lag + 1):
            for j in by_t.get(r.t - dt, []):
                if j == i:
                    continue
                q = sp.loc[j]
                if q.s == r.s or adj[r.s, q.s] or adj[q.s, r.s]:
                    parent[find(i)] = find(j)
    sp["constellation"] = [find(i) for i in range(len(sp))]
    return sp


def constellations(sp: pd.DataFrame) -> pd.DataFrame:
    g = sp.groupby("constellation")
    return pd.DataFrame({"first": g.bucket.min(), "last": g.bucket.max(), "sparks": g.size(),
                         "subjects": g.subject.nunique(), "score": g.surprise.sum().round(1)}).sort_values("score", ascending=False)


# ------------------------------------------------------------------------------------- forecasting evaluation
def evaluate_forecast(C: np.ndarray, lam: np.ndarray, rows, top_frac: float = 0.01) -> dict:
    """For each bucket t in rows, rank subjects by expectation lam[t] (computed from the past only) and measure
    how much of what actually happened in t falls in the top-ranked subjects."""
    S = C.shape[1]
    k = max(1, int(round(top_frac * S)))
    captured = total = 0
    aucs = []
    for t in rows:
        y = C[t] > 0
        if not y.any():
            continue
        order = np.argsort(-lam[t], kind="stable")
        captured += C[t, order[:k]].sum(); total += C[t].sum()
        if y.all():
            continue
        ranks = np.empty(S); ranks[order] = np.arange(S)
        pos, neg = ranks[y], ranks[~y]
        aucs.append(np.mean(pos[:, None] < neg[None, :]) + 0.5 * np.mean(pos[:, None] == neg[None, :]))
    return {"top_k_subjects": k, "share_of_events_in_top_k": round(captured / max(total, 1), 3),
            "auc": round(float(np.mean(aucs)), 3) if aucs else None, "buckets": len(rows)}


# ------------------------------------------------------------------------------------- analogs (close patterns)
def window_features(C: np.ndarray, t: int, s: int, W: int) -> np.ndarray:
    w = C[max(0, t - W):t, s]
    w = np.pad(w, (W - len(w), 0))
    level = np.log1p(w.sum())
    shape = w / w.sum() if w.sum() > 0 else w
    trend = np.log1p(w[-W // 3:].sum()) - np.log1p(w[:W // 3].sum())
    return np.concatenate([shape, [level, trend]])


def analog_forecast(C, t_query, s_query, W, H, k=25, min_gap=0, candidates=None):
    """Find the k historical (subject, time) windows most similar to the query window, using only windows
    whose outcome (next H buckets) was already known at t_query. Return what happened next to them."""
    from numpy.linalg import norm
    q = window_features(C, t_query, s_query, W)
    feats, outs, keys = [], [], []
    T, S = C.shape
    for t in range(W, t_query - H + 1, max(1, H // 2)):
        for s in (candidates if candidates is not None else range(S)):
            if C[t - W:t, s].sum() == 0:
                continue
            feats.append(window_features(C, t, s, W)); outs.append(C[t:t + H, s].sum()); keys.append((t, s))
    if not feats:
        return None
    F = np.array(feats)
    sim = (F @ q) / (norm(F, axis=1) * norm(q) + 1e-12)
    top = np.argsort(-sim)[:k]
    return {"analogs": [keys[i] for i in top], "similarity": sim[top], "next_counts": np.array(outs)[top]}
