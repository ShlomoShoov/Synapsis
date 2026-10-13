"""M3 with a sparse neighbour matrix: the formulas of analytics.py (round-2 prototype), for thousands of cells."""
import numpy as np, pandas as pd, h3, hashlib
from scipy.signal import lfilter
from scipy import sparse
import analytics as A

def series(ev, start, end, freq):
    B = pd.date_range(start, end, freq=freq)
    subj = sorted(ev.cell.unique()); si = {s: i for i, s in enumerate(subj)}
    ti = B.searchsorted(ev.bucket.values)
    C = np.zeros((len(B), len(subj)))
    np.add.at(C, (ti, ev.cell.map(si).values), 1)
    rows, cols = [], []
    for s, i in si.items():
        for nb in h3.grid_disk(s, 1):
            j = si.get(nb)
            if j is not None and j != i: rows.append(i); cols.append(j)
    N = sparse.csr_matrix((np.full(len(rows), 1 / 6.0), (rows, cols)), shape=(len(subj), len(subj)))
    return B, subj, C, N

def excitation(C, N, decay, gamma):
    X = C + (gamma * (N @ C.T).T if gamma > 0 else 0.0)
    return lfilter([0.0, 1.0], [1.0, -decay], X, axis=0)

def fit(C, N, rows, gammas):
    mu = A.background(C); best = (-np.inf, None)
    for d in [0.3, 0.5, 0.7, 0.85, 0.95]:
        for g in gammas:
            Y = excitation(C, N, d, g)
            for a in [0.01, 0.02, 0.05, 0.1, 0.2, 0.4]:
                ll = A.poisson_ll(C, mu + a * Y + 1e-9, rows)
                if ll > best[0]: best = (ll, (a, d, g))
    return best[1]

def lam(C, N, p):
    return A.background(C) + p[0] * excitation(C, N, p[1], p[2]) + 1e-9

def compare(C, N, cal, test, top=0.01):
    p0 = fit(C, N, cal, [0.0]); pg = fit(C, N, cal, [0.0, 0.1, 0.3, 0.6])
    out = {}
    for name, L in (("M1 routine", A.expectations(C, "M1")), (f"M3 own {p0}", lam(C, N, p0)), (f"M3 +neighbours {pg}", lam(C, N, pg))):
        e = A.evaluate_forecast(C, L, test, top)
        out[name] = (round(A.poisson_ll(C, L, test), 5), e["auc"], round(float(e["share_of_events_in_top_k"]), 3), e["top_k_subjects"])
    return out, pg

def digest(x): return hashlib.sha256(np.ascontiguousarray(x).tobytes()).hexdigest()[:16]
