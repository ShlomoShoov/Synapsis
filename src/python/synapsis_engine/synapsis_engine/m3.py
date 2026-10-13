"""Routine, expectation (M3) and fit: exactly the formulas of reference/analytics.py, with the sparse neighbours of
reference/m3_sparse.py and the lag of reference/ucdp_gtd.py. All causal: row t uses buckets < t - L only."""
import h3
import numpy as np
from scipy import sparse
from scipy.signal import lfilter
from scipy.stats import poisson


def background(C: np.ndarray, alpha: float = 50.0) -> np.ndarray:
    """Routine: causal, shrunk long-run rate per subject (analytics.background)."""
    T, S = C.shape
    cum = np.vstack([np.zeros((1, S)), np.cumsum(C, 0)[:-1]])
    n = np.arange(T)[:, None].astype(float)
    seen = (cum > 0).sum(1, keepdims=True)
    glob = cum.sum(1, keepdims=True) / np.maximum(n * np.maximum(seen, 1), 1)
    return (cum + alpha * glob) / (n + alpha)


def neighbours(cells):
    """H3 ring 1, weight 1/6 per neighbour whether or not it has data (m3_sparse.series)."""
    si = {s: i for i, s in enumerate(cells)}
    rows, cols = [], []
    for s, i in si.items():
        for nb in sorted(h3.grid_disk(s, 1)):
            j = si.get(nb)
            if j is not None and j != i:
                rows.append(i)
                cols.append(j)
    N = sparse.csr_matrix((np.full(len(rows), 1 / 6.0), (rows, cols)), shape=(len(cells), len(cells)))
    N.sort_indices()
    return N


def excitation(C, N, decay, gamma):
    """Y[t] = sum_{k>=1} decay^(k-1) (C[t-k] + gamma * neighbours(C[t-k]))."""
    X = C + (gamma * (N @ C.T).T if (N is not None and gamma > 0) else 0.0)
    return lfilter([0.0, 1.0], [1.0, -decay], X, axis=0)


def shift(X, L):
    Y = np.zeros_like(X)
    Y[L:] = X[:len(X) - L]
    return Y


def poisson_ll(C, lam, rows) -> float:
    c, l = C[rows], lam[rows]
    return float(np.mean(c * np.log(l) - l))


def expectation(C, N, p, L=0, mu=None):
    """lambda[t] = mu[t-L] + a * decay^L * E[t-L] + 1e-9 (M3; L = 0 gives analytics.expectations 'M3')."""
    a, d, g = p
    mu = background(C) if mu is None else mu
    return shift(mu, L) + a * d ** L * shift(excitation(C, N, d, g), L) + 1e-9


def routine(C, L=0, mu=None):
    return shift(background(C) if mu is None else mu, L) + 1e-9


def fit(C, N, rows, grid, L=0):
    """Grid MLE of (a, decay, gamma) on the calibration rows; same loop order and strict '>' as the reference."""
    best = (-np.inf, None)
    mu = shift(background(C), L)[rows]
    c = C[rows]
    for d in grid["decay"]:
        for g in grid["gamma"]:
            if g > 0 and N is None:
                continue
            Y = shift(excitation(C, N, d, g), L)[rows]
            for a in grid["a"]:
                lam = mu + a * d ** L * Y + 1e-9
                ll = float(np.mean(c * np.log(lam) - lam))
                if ll > best[0]:
                    best = (ll, (a, d, g))
    return best[1], best[0]


def surprise(c, lam):
    """-log10 P(N >= c | lam), 0 when c == 0 (analytics.surprise), on any array shape."""
    c = np.asarray(c, dtype=float)
    s = -np.log10(np.clip(poisson.sf(c - 1, lam), 1e-300, 1.0))
    s[c == 0] = 0.0
    return s


def threshold_for_budget(values, budget):
    """Threshold so that at most `budget` of the active (> 0) values reach it (analytics.threshold_for_budget)."""
    v = np.asarray(values, dtype=float)
    v = v[v > 0]
    return float(np.quantile(v, 1 - budget)) if len(v) else np.inf


def band(lam):
    """Poisson 10th and 90th percentiles."""
    return poisson.ppf(0.1, lam), poisson.ppf(0.9, lam)


def evaluate_forecast(C, lam, rows, top_frac=0.01):
    """analytics.evaluate_forecast, plus the unrounded values and the captured events per bucket."""
    S = C.shape[1]
    k = max(1, int(round(top_frac * S)))
    captured = total = 0
    aucs, per = [], {}
    for t in rows:
        y = C[t] > 0
        if not y.any():
            continue
        order = np.argsort(-lam[t], kind="stable")
        got = C[t, order[:k]].sum()
        per[int(t)] = (float(got), float(C[t].sum()))
        captured += got
        total += C[t].sum()
        if y.all():
            continue
        ranks = np.empty(S)
        ranks[order] = np.arange(S)
        pos, neg = ranks[y], ranks[~y]
        aucs.append(np.mean(pos[:, None] < neg[None, :]) + 0.5 * np.mean(pos[:, None] == neg[None, :]))
    share = captured / max(total, 1)
    auc = float(np.mean(aucs)) if aucs else None
    return {"top_k_subjects": k, "share_of_events_in_top_k": round(share, 3), "auc": round(auc, 3) if aucs else None,
            "buckets": len(rows), "share": float(share), "auc_raw": auc, "per_bucket": per}
