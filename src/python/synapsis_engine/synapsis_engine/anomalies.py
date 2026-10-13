"""Anomalies: temporal surprise with an alarm budget (occurrences), own-past change (measurements), peer z and the
joint of a lens (declared measures, all subjects of a period)."""
import numpy as np

from . import m3

MIN_SURPRISE = 3.0     # a spark needs s >= 3 and n >= 3 besides the budget
MIN_COUNT = 3
OWN_PAST_Z = 4.0       # own-past spark: |z| >= 4 and within the budget
OWN_PAST_WINDOW = 52
OWN_PAST_MIN = 8       # fewer previous changes: no z (the report counts them)


def occurrence_threshold(C_in, lam, cal_rows, budget):
    """Surprise threshold that lets <= budget of the active subject-buckets of the calibration rows spark."""
    t, s = np.nonzero(C_in[cal_rows])
    t = np.asarray(cal_rows)[t]
    return m3.threshold_for_budget(m3.surprise(C_in[t, s], lam[t, s]), budget)


def occurrence_sparks(C_in, lam, first_row, thr):
    """(t, s, n, lam, surprise) of every subject-bucket from first_row on that sparks."""
    t, s = np.nonzero(C_in[first_row:])
    t = t + first_row
    n = C_in[t, s]
    sv = m3.surprise(n, lam[t, s])
    ok = (sv >= thr) & (sv >= MIN_SURPRISE) & (n >= MIN_COUNT)
    return t[ok], s[ok], n[ok], lam[t[ok], s[ok]], sv[ok]


def robust_z(x, ref):
    """(x - median) / (1.4826 * MAD) against ref; None when the MAD is 0."""
    m = np.median(ref)
    mad = np.median(np.abs(ref - m)) * 1.4826
    return None if mad == 0 else (x - m) / mad


def own_past(values):
    """Per position i: change = v[i] - v[i-1] and its robust z against the previous (up to 52) changes.
    Returns arrays (change, z) with NaN where there is no previous value, too few changes, or a zero MAD."""
    v = np.asarray(values, dtype=float)
    change = np.full(len(v), np.nan)
    z = np.full(len(v), np.nan)
    if len(v) < 2:
        return change, z
    change[1:] = np.diff(v)
    W = np.lib.stride_tricks.sliding_window_view(np.concatenate([np.full(OWN_PAST_WINDOW, np.nan), change]),
                                                 OWN_PAST_WINDOW)[:len(v)]          # W[i] = the changes before i
    count = (~np.isnan(W)).sum(1)
    ok = count >= OWN_PAST_MIN
    if not ok.any():
        return change, z
    med = np.nanmedian(W[ok], 1)
    mad = np.nanmedian(np.abs(W[ok] - med[:, None]), 1) * 1.4826
    zi = np.full(ok.sum(), np.nan)
    good = mad > 0
    zi[good] = (change[ok][good] - med[good]) / mad[good]
    z[ok] = zi
    return change, z


def peer_z(x, direction):
    """Robust z over all subjects of a period: direction * (x - median) / (1.4826 * MAD) (fdic_lenses.rz)."""
    x = np.asarray(x, dtype=float)
    m = np.median(x)
    mad = np.median(np.abs(x - m)) * 1.4826 or 1e-9
    return direction * (x - m) / mad


def joint(zs):
    """The joint score of a lens: the weakest of its z-scores."""
    return np.min(np.vstack(zs), axis=0)


def auc(scores, pos):
    """fdic_lenses.auc: the share of (failed, not failed) pairs ranked in the right order (stable on ties)."""
    order = sorted(range(len(scores)), key=lambda i: -scores[i])
    P = sum(pos)
    Nn = len(pos) - P
    if P == 0 or Nn == 0:
        return None
    seen_neg = tot = 0
    for i in order:
        if pos[i]:
            tot += Nn - seen_neg
        else:
            seen_neg += 1
    return tot / (P * Nn)
