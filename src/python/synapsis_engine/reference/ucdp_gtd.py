"""UCDP GED and GTD 1970-94, weekly, H3 r3, with the feeders' rules: a point gets an r3 cell only when its uncertainty
is below 59.8 km (UCDP where_prec 1-3, GTD specificity 1-3), and a GTD event without a known day fills no week.
UCDP is published about 4 weeks late (its feeder: known_at = event + 28 days): lag 4 is the honest number.
Run: python ucdp_gtd.py <ged261-csv.zip> <gtd_70to94_0617dist.xlsx>   (the feeders' cache holds both files)"""
import sys, zipfile
import numpy as np, pandas as pd, h3
import m3_sparse as m3s, analytics as A

def shift(X, L):
    Y = np.zeros_like(X); Y[L:] = X[:len(X) - L]; return Y

def run(name, ev, start, cal_end, test_end, lags):
    B, S, C, N = m3s.series(ev, start, ev.bucket.max(), "W-MON")
    cal = np.where(B < cal_end)[0]; test = np.where((B >= cal_end) & (B < test_end))[0]
    print(f"{name}: {C.shape[0]} weeks x {C.shape[1]} cells, {int(C.sum())} events; test {cal_end}..{test_end}")
    mu = A.background(C)
    for L in lags:
        best = (-np.inf, None)
        for d in [0.3, 0.5, 0.7, 0.85, 0.95]:
            for gam in [0.0, 0.1, 0.3, 0.6]:
                E = m3s.excitation(C, N, d, gam)
                for a in [0.01, 0.02, 0.05, 0.1, 0.2, 0.4]:
                    ll = A.poisson_ll(C, shift(mu, L) + a * d ** L * shift(E, L) + 1e-9, cal)
                    if ll > best[0]: best = (ll, (a, d, gam))
        a, d, gam = best[1]
        for label, lam in (("routine", shift(mu, L) + 1e-9), (f"M3 a={a} d={d} g={gam}", shift(mu, L) + a * d ** L * shift(m3s.excitation(C, N, d, gam), L) + 1e-9)):
            e = A.evaluate_forecast(C, lam, test, 0.01)
            print(f"  lag {L} | {label}: auc {e['auc']} top1% {float(e['share_of_events_in_top_k']):.3f} (k={e['top_k_subjects']})")
    lam = shift(mu, lags[-1]) + 1e-9
    print("  determinism:", m3s.digest(lam) == m3s.digest(shift(A.background(C), lags[-1]) + 1e-9))

z = zipfile.ZipFile(sys.argv[1]); f = [n for n in z.namelist() if n.endswith(".csv")][0]
g = pd.read_csv(z.open(f), usecols=["date_start", "latitude", "longitude", "where_prec"], low_memory=False)
g = g[g.where_prec <= 3].copy()
g["bucket"] = pd.to_datetime(g.date_start).dt.to_period("W-SUN").dt.start_time
g["cell"] = [h3.latlng_to_cell(a, b, 3) for a, b in zip(g.latitude, g.longitude)]
run("UCDP", g, "1989-01-02", "2010-01-01", "2025-01-01", [0, 4])

d = pd.read_excel(sys.argv[2], usecols=["iyear", "imonth", "iday", "latitude", "longitude", "specificity"])
d = d.dropna(subset=["latitude", "longitude"]); d = d[(d.specificity <= 3) & (d.imonth > 0) & (d.iday > 0)].copy()
d["bucket"] = pd.to_datetime(dict(year=d.iyear, month=d.imonth, day=d.iday), errors="coerce").dt.to_period("W-SUN").dt.start_time
d = d.dropna(subset=["bucket"])
d["cell"] = [h3.latlng_to_cell(a, b, 3) for a, b in zip(d.latitude, d.longitude)]
run("GTD", d, "1969-12-29", "1985-01-01", "1993-01-01", [0])
