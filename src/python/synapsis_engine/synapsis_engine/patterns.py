"""Similar patterns: the past windows of the same source that looked most like a subject's last 8 buckets, and how
they continued. Window vector: v = log(1 + count), shape = v - mean(v), then 0.5 * mean(v). Only windows with at
least one event are candidates (an empty window has no shape)."""
import math

import numpy as np

W = 8                 # window length (buckets)
H = 8                 # continuation length (buckets)
K = 10                # nearest windows
MAX_QUERIES = 5000    # evaluation: at most this many queries per source, every n-th


def features(V):
    level = V.mean(1)
    return np.hstack([V - level[:, None], 0.5 * level[:, None]]), level


class Windows:
    """Every active window [u - W, u) of every subject whose continuation [u, u + H) lies on the axis, by (u, s)."""

    def __init__(self, C):
        T, S = C.shape
        cs = np.vstack([np.zeros((1, S)), np.cumsum(C, 0)])
        u = np.arange(W, T - H + 1)
        iu, self.s = np.nonzero(cs[u] - cs[u - W] > 0) if len(u) else (np.array([], int), np.array([], int))
        self.u = u[iu]
        V = np.log1p(np.stack([C[self.u - W + k, self.s] for k in range(W)], 1)) if len(self.u) else np.zeros((0, W))
        self.vec, self.level = features(V)
        self.norm = (self.vec ** 2).sum(1)
        self.fut = np.stack([C[self.u + h, self.s] for h in range(H)], 1).astype(np.float32) if len(self.u) else np.zeros((0, H), np.float32)

    def query(self, C, t, s, L):
        """The K nearest windows for subject s at issue row t (window [t-L-W, t-L)): candidates whose continuation
        ended by t-1-L, the subject's own overlapping windows excluded, ties broken by (distance, subject, start)."""
        end = t - L
        if end - W < 0:
            return None
        V = np.log1p(C[end - W:end, s])[None, :]
        if V.sum() == 0:
            return None
        q, lq = features(V)
        q, lq = q[0], lq[0]
        n = int(np.searchsorted(self.u, end - H, side="right"))
        ok = ~((self.s[:n] == s) & (self.u[:n] > end - W - H))
        idx = np.flatnonzero(ok)
        if not len(idx):
            return None
        d2 = self.norm[idx] - 2 * (self.vec[idx] @ q) + q @ q
        if len(idx) > K:
            kth = np.partition(d2, K - 1)[K - 1]
            idx = idx[d2 <= kth + 1e-9]
        dist = np.sqrt(((self.vec[idx] - q) ** 2).sum(1))
        top = np.lexsort((self.u[idx], self.s[idx], dist))[:K]
        idx, dist = idx[top], dist[top]
        scale = np.exp(lq - self.level[idx])
        F = self.fut[idx].astype(float) * scale[:, None]
        totals = F.sum(1)
        return {"idx": idx, "dist": dist, "totals": totals, "raw": self.fut[idx].sum(1), "F": F,
                "expected_total": float(np.median(totals)), "lower80": float(np.percentile(totals, 10)),
                "upper80": float(np.percentile(totals, 90))}


def m3_forward(mu, E, N, p, t, L):
    """M3 run forward over the next H buckets with its own expectations in place of the unknown counts."""
    a, d, g = p
    m, e = mu[t - L], E[t - L].copy()
    total = np.zeros_like(m)
    for _ in range(H):
        lam = m + a * e + 1e-9
        total += lam
        e = d * e + lam + (g * (N @ lam) if (N is not None and g > 0) else 0.0)
    return total


def evaluate(win, C_in, C_full, mu, E, N, p, L, rows):
    """Mean absolute error of the next-H total: patterns, routine (H * mu) and M3 run forward."""
    lo, hi = rows
    T = C_in.shape[0]
    q = [(t, s) for t in range(max(lo, L + W), hi) if t - L + H <= hi and t - L + H <= T
         for s in np.flatnonzero(C_in[t - L - W:t - L].sum(0) > 0)]
    if not q:
        return None
    step = max(1, math.ceil(len(q) / MAX_QUERIES))
    q = q[::step]
    err = {"patterns": [], "routine": [], "m3": []}
    fwd = {}
    for t, s in q:
        r = win.query(C_in, t, s, L)
        if r is None:
            continue
        actual = C_full[t - L:t - L + H, s].sum()
        if t not in fwd:
            fwd = {t: m3_forward(mu, E, N, p, t, L)}
        err["patterns"].append(abs(r["expected_total"] - actual))
        err["routine"].append(abs(H * mu[t - L, s] - actual))
        err["m3"].append(abs(fwd[t][s] - actual))
    if not err["patterns"]:
        return None
    out = {k: float(np.mean(v)) for k, v in err.items()}
    return {"queries": len(err["patterns"]), "every": step, "mae_patterns": out["patterns"], "mae_routine": out["routine"],
            "mae_m3": out["m3"], "beats_routine": out["patterns"] < out["routine"], "beats_m3": out["patterns"] < out["m3"]}
