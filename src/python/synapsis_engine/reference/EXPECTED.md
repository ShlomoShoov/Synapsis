# Expected results (golden values)

Computed on 13 Oct 2026 from the public sources, outside our pipeline, with the scripts in this folder. The engine
must reach the same values from the records our pipeline stored. Same input, same output: the scripts are
deterministic (UCDP and GTD print `determinism: True`).

Formulas: `analytics.py` is the round-2 prototype, unchanged. Its routine is `background(C, alpha=50)`, so the
routine uses κ = 50 here, not the κ = 8 of later documents. The engine uses this file's formulas exactly.

## 1. Earthquakes: USGS M5.5+, 1965–2016, daily, H3 r2 (`usgs_tohoku.py`)
Calibration before 1990-01-01, test 1990–2016. 18,993 days × 1,257 cells, 24,692 events. "top1%" is the share of
the next day's events that fall in the 1% of cells the model ranked highest (13 cells).

| Model | AUC | top1% | Tōhoku cell on 11 Mar 2011 (data to 10 Mar) |
| --- | --- | --- | --- |
| routine (M1) | 0.842 | 0.151 | rank 25 of 1,257 |
| M3 own cell, (a, δ, γ) = (0.03, 0.7, 0) | 0.851 | 0.214 | rank 5 |
| M3 + map neighbours, (0.03, 0.7, 0.1) | 0.852 | 0.216 | **rank 3** |

Tolerance through the pipeline: cells ±10, AUC ±0.003, top1% ±0.005, Tōhoku rank ≤ 5 (3 expected).

## 2. Conflict: UCDP GED 26.1, weekly, H3 r3 (`ucdp_gtd.py`)
Feeder rules: where_prec 1–3 (uncertainty ≤ 50 km < 59.8). 1,931 weeks × 2,941 cells, 355,217 events.
Calibration before 2010, test 2010–2024. k = 29 cells (1%). **Lag 4** is the honest number: the feeder sets
known_at = event + 28 days, so a forecast for week t uses data up to t−5.

| Lag | routine AUC / top1% | M3 AUC / top1% | M3 parameters |
| --- | --- | --- | --- |
| 0 | 0.882 / 0.439 | 0.923 / 0.614 | a 0.2, δ 0.7, γ 0 |
| **4** | **0.878 / 0.432** | **0.919 / 0.589** | a 0.2, δ 0.85, γ 0 |

Tolerance: AUC ±0.005, top1% ±0.01 (the pipeline also holds the candidate files).

## 3. Terrorism: GTD 1970–1994, weekly, H3 r3 (`ucdp_gtd.py`)
Feeder rules: specificity 1–3, and only events with a known day. 1,305 weeks × 1,931 cells, 51,861 events.
Calibration before 1985, test 1985–1992 (GTD has no 1993).

| Model | AUC | top1% |
| --- | --- | --- |
| routine | 0.876 | 0.335 |
| M3, (0.1, 0.85, 0.1) | **0.896** | **0.411** |

Tolerance: AUC ±0.005, top1% ±0.01.

## 4. Banks: FDIC year-end reports (`fdic_svb.py`, `fdic_lenses.py`)
Banks with deposits > 0 and equity > 0. Robust z = (x − median) / (1.4826 · MAD) × direction; joint = the minimum.
Missing AFS/HTM amounts count as 0. Failed = an FDIC failure within 365 days after 14 Feb of the next year
(the pipeline's known_at is 15 Feb: same results except at the edge).

Lens A "run risk" (uninsured share +1, unrealized loss to equity +1), 2022 year-end, 4,683 banks:
**Silicon Valley Bank rank 5**, joint z 3.60, uninsured 0.864, loss/equity 1.144 (failed 10 Mar 2023).
Signature rank 324. 2023 year-end, 4,571 banks: Republic Bank (Republic First) rank 16.

Lens B "credit stress" (noncurrent_loans_ratio +1, roa −1), and lens A, every year-end:

| Year-end | Banks | Failed in 12 months | Lens A: in top 50 / AUC | Lens B: in top 50 / AUC |
| --- | --- | --- | --- | --- |
| 2006 | 8,676 | 3 | 0 / 0.367 | 2 / 0.671 |
| 2007 | 8,531 | 46 | 0 / 0.552 | 7 / 0.707 |
| 2008 | 8,297 | 141 | 2 / 0.516 | **26 / 0.951** |
| 2009 | 7,996 | 149 | 2 / 0.517 | **27 / 0.957** |
| 2010 | 7,646 | 79 | 0 / 0.419 | 12 / 0.952 |
| 2011 | 7,350 | 42 | 0 / 0.364 | 15 / 0.984 |
| 2012 | 7,080 | 25 | 0 / 0.359 | 10 / 0.980 |
| 2013 | 6,808 | 17 | 0 / 0.342 | 8 / 0.953 |
| 2014 | 6,508 | 5 | 0 / 0.390 | 3 / 0.916 |
| 2015 | 6,182 | 7 | 0 / 0.341 | 7 / 1.000 |
| 2016 | 5,909 | 5 | 0 / 0.175 | 2 / 0.822 |
| 2018 | 5,405 | 4 | 0 / 0.334 | 3 / 0.822 |
| 2019 | 5,176 | 4 | 0 / 0.134 | 4 / 0.998 |
| 2022 | 4,683 | 5 | **1 / 0.900** | 0 / 0.710 |
| 2023 | 4,571 | 3 | 1 / 0.366 | 0 / 0.608 |

2017, 2020 and 2021 had no failures in the following 12 months.
Tolerance: SVB rank 5 (±1 if the bank count differs), AUC ±0.01, failures in the top 50 ±1.

## 5. Elections (`case_f6_elections.py`, round 3, not re-run here)
"routine_bias+prior_blend": mean absolute error **2.56** points (2020) and **2.24** (2024); winners 48 of 50 (2020)
and **37 of 37** (2024). Polls alone: 5.60 and 3.31; 34 of 37. 2024 Brier: corrected **0.0041**, Polymarket 0.0391,
polls alone 0.0457. Tolerance: error ±0.05 points; winners exact.

## Not proven (do not claim)
Map neighbours add little (UCDP γ = 0). Sudan 2023 top-20 capture: routine and M3 equal (82.6%). KEV vendors: M3
not better than the routine (round 2). Similar patterns, Omicron, CL0P: not re-run.
