"""USGS M5.5+ 1965-2016, daily, H3 r2: routine vs M3 (own cell, + map neighbours), and the Tohoku rollback.
Reads quakes.csv in the working folder, downloading it from the USGS event service when missing (52 requests).
Run: python usgs_tohoku.py   (needs numpy, pandas, scipy, h3)"""
import os, urllib.request, urllib.parse
import numpy as np, pandas as pd, h3
import analytics as A

if not os.path.exists("quakes.csv"):
    with open("quakes.csv", "w") as out:
        for y in range(1965, 2017):
            q = urllib.parse.urlencode({"format": "csv", "starttime": f"{y}-01-01", "endtime": f"{y + 1}-01-01", "minmagnitude": 5.5, "orderby": "time-asc"})
            lines = urllib.request.urlopen("https://earthquake.usgs.gov/fdsnws/event/1/query?" + q, timeout=120).read().decode().splitlines()
            out.write(("" if y > 1965 else lines[0] + "\n") + "\n".join(lines[1:]) + "\n")
ev = pd.read_csv("quakes.csv")
ev["t"] = pd.to_datetime(ev["time"], utc=True).dt.tz_convert(None).dt.floor("D")
ev["cell"] = [h3.latlng_to_cell(a, b, 2) for a, b in zip(ev.latitude, ev.longitude)]
B = pd.date_range("1965-01-01", "2016-12-31", freq="D"); S = sorted(ev.cell.unique()); si = {s: i for i, s in enumerate(S)}
C = np.zeros((len(B), len(S)))
for t, c in zip(((ev.t - B[0]).dt.days).values, ev.cell): C[t, si[c]] += 1
N = A.neighbor_matrix(A.Series(S, B, C, C), "h3")
cal = np.where(B < "1990-01-01")[0]; test = np.where(B >= "1990-01-01")[0]
s = si[h3.latlng_to_cell(38.297, 142.373, 2)]; t = B.get_loc(pd.Timestamp("2011-03-11"))
g = dict(a=[0.002, 0.005, 0.01, 0.02, 0.03], decay=[0.7, 0.8, 0.85, 0.9, 0.95])
pn, _ = A.fit_m3(C, N, cal, {**g, "gamma": [0.0]})
pg, _ = A.fit_m3(C, N, cal, {**g, "gamma": [0.0, 0.05, 0.1, 0.2, 0.4]})
print(f"{C.shape[0]} days x {C.shape[1]} cells, {int(C.sum())} events")
for name, lam in (("routine (M1)", A.expectations(C, "M1", N)), (f"M3 own cell {pn}", A.expectations(C, "M3", N, pn)),
                  (f"M3 + neighbours {pg}", A.expectations(C, "M3", N, pg))):
    e = A.evaluate_forecast(C, lam, test, 0.01)
    print(f"{name}: auc {e['auc']} top1% {float(e['share_of_events_in_top_k']):.3f} | Tohoku cell rank {int((lam[t] > lam[t, s]).sum()) + 1} of {len(S)}")
