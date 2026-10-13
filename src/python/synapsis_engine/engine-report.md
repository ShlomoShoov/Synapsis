# Engine report

Generated 2026-10-13 19:33 UTC by `python -m synapsis_engine verify`. Records: 4,733 (4,713 conclusions).

Summary: 1 pass, 2 pass, 3 pass, 4 pass, 5 pass, 6 pass

## 1. Same maths (tolerance 1e-12)

| Function | Max difference |
|---|---|
| background (routine) | 0.0e+00 |
| excitation with neighbours (dense N) | 0.0e+00 |
| excitation (m3_sparse) | 0.0e+00 |
| expectations M3 | 0.0e+00 |
| expectations M1 (routine) | 0.0e+00 |
| expectation with lag 4 (ucdp_gtd.py) | 0.0e+00 |
| poisson_ll | 0.0e+00 |
| fit_m3: log-likelihood | 0.0e+00 |
| fit_m3: parameters | 0.0e+00 |
| m3_sparse.fit: parameters | 0.0e+00 |
| surprise | 0.0e+00 |
| threshold_for_budget | 0.0e+00 |
| evaluate_forecast: AUC | 0.0e+00 |
| evaluate_forecast: top 1% share | 0.0e+00 |

## 2. Determinism

- Two backfills: sha256 `8894cbe07ac991bfb114c20226e707e418d771bfaee74df1f5c405a5f94013c9` and `8894cbe07ac991bfb114c20226e707e418d771bfaee74df1f5c405a5f94013c9`: same.
- POST: first {"received": 4733, "new": 0, "changed": 0, "unchanged": 4733, "rejected": 0}; second {"received": 4733, "new": 0, "changed": 0, "unchanged": 4733, "rejected": 0}.

## 3. No hindsight

- Every event counted for a conclusion has known_at <= the conclusion's known_at: 4,713 conclusions checked, 0 violations.
- Runs on records truncated at known_at <= the issue time (frozen model cards):

| Source | Issue time | Conclusions (full run) | Conclusions (truncated) | Same |
|---|---|---|---|---|
| src:cisa:kev | 2026-01-19 00:00:00 | 280 | 280 | yes |
| src:cisa:kev | 2026-04-20 00:00:00 | 540 | 540 | yes |
| src:cisa:kev | 2026-10-12 00:00:00 | 1,040 | 1,040 | yes |
| src:delphi:hhs-admissions | 2020-06-05 00:00:00 | 2 | 2 | yes |
| src:delphi:hhs-admissions | 2020-06-19 00:00:00 | 4 | 4 | yes |
| src:delphi:hhs-admissions | 2020-07-10 00:00:00 | 7 | 7 | yes |
| src:fdic:financials | 2023-02-15 00:00:00 | 100 | 100 | yes |
| src:fdic:financials | 2023-05-16 00:00:00 | 200 | 200 | yes |
| src:fdic:financials | 2023-08-15 00:00:00 | 300 | 300 | yes |
| src:hn:algolia | 2026-01-26 00:00:00 | 14 | 14 | yes |
| src:hn:algolia | 2026-04-27 00:00:00 | 27 | 27 | yes |
| src:hn:algolia | 2026-10-19 00:00:00 | 52 | 52 | yes |
| src:nist:nvd | 2025-09-29 00:00:00 | 280 | 280 | yes |
| src:nist:nvd | 2025-12-29 00:00:00 | 540 | 540 | yes |
| src:nist:nvd | 2026-06-22 00:00:00 | 1,040 | 1,040 | yes |
| src:ransomware-live:victims | 2023-07-31 00:00:00 | 280 | 280 | yes |
| src:ransomware-live:victims | 2023-10-30 00:00:00 | 540 | 540 | yes |
| src:ransomware-live:victims | 2024-04-22 00:00:00 | 1,041 | 1,041 | yes |
| src:reddit:arcticshift | 2026-01-26 00:00:00 | 14 | 14 | yes |
| src:reddit:arcticshift | 2026-04-27 00:00:00 | 27 | 27 | yes |
| src:reddit:arcticshift | 2026-10-19 00:00:00 | 52 | 52 | yes |
| src:tukaani:xz-git | 2008-06-23 00:00:00 | 2 | 2 | yes |
| src:tukaani:xz-git | 2008-09-01 00:00:00 | 4 | 4 | yes |
| src:usgs:comcat | 2016-11-22 00:54:07 | 280 | 280 | yes |
| src:usgs:comcat | 2016-12-05 00:54:07 | 540 | 540 | yes |
| src:usgs:comcat | 2016-12-31 00:54:07 | 1,060 | 1,060 | yes |

## 4. Placebo (bucket order shuffled, seed 7)

| Series | M3 refit | M3 top1% | routine top1% | M3 - routine <= 0.01 |
|---|---|---|---|---|
| src:cisa:kev|logical | (0.02, 0.3, 0.0) | 0.220 | 0.228 | yes |
| src:delphi:hhs-admissions|logical | (0.02, 0.3, 0.0) | 1.000 | 1.000 | yes |
| src:hn:algolia|logical | (0.02, 0.3, 0.0) | 1.000 | 1.000 | yes |
| src:nist:nvd|participant | (0.02, 0.3, 0.0) | 0.000 | 0.000 | yes |
| src:ransomware-live:victims|logical | (0.02, 0.3, 0.0) | 0.235 | 0.235 | yes |
| src:reddit:arcticshift|logical | (0.02, 0.3, 0.0) | 0.000 | 0.000 | yes |
| src:usgs:comcat|cell | (0.002, 0.7, 0.0) | 0.106 | 0.106 | yes |

## 5. Pipeline: events per source, MongoDB (first states) = DuckDB

| Source | MongoDB | DuckDB |
|---|---|---|
| src:cisa:kev | 1,739 | 1,739 |
| src:delphi:hhs-admissions | 200 | 200 |
| src:fdic:failures | 68 | 68 |
| src:fdic:financials | 14,227 | 14,227 |
| src:first:epss | 100 | 100 |
| src:gdelt:events | 200 | 200 |
| src:hn:algolia | 186 | 186 |
| src:jhu:csse-covid | 200 | 200 |
| src:nist:nvd | 133 | 133 |
| src:polymarket:prices | 200 | 200 |
| src:ransomware-live:victims | 176 | 176 |
| src:reddit:arcticshift | 200 | 200 |
| src:start:gtd | 169 | 169 |
| src:tukaani:xz-git | 198 | 198 |
| src:ucdp:ged | 138 | 138 |
| src:usgs:comcat | 200 | 200 |
| src:wikipedia:polls | 109 | 109 |

## 6. Golden values (reference/EXPECTED.md)

| Source | Value | Expected | Engine | Result |
|---|---|---|---|---|
| src:usgs:comcat | routine AUC | 0.842 | pending | needs the full data (200 events now) |
| src:usgs:comcat | routine top1% | 0.151 | pending | needs the full data (200 events now) |
| src:usgs:comcat | M3 + neighbours AUC | 0.852 | pending | needs the full data (200 events now) |
| src:usgs:comcat | M3 + neighbours top1% | 0.216 | pending | needs the full data (200 events now) |
| src:ucdp:ged | lag L | 4 | pending | needs the full data (50 events now) |
| src:ucdp:ged | routine AUC | 0.878 | pending | needs the full data (50 events now) |
| src:ucdp:ged | routine top1% | 0.432 | pending | needs the full data (50 events now) |
| src:ucdp:ged | M3 AUC | 0.919 | pending | needs the full data (50 events now) |
| src:ucdp:ged | M3 top1% | 0.589 | pending | needs the full data (50 events now) |
| src:start:gtd | routine AUC | 0.876 | pending | needs the full data (160 events now) |
| src:start:gtd | routine top1% | 0.335 | pending | needs the full data (160 events now) |
| src:start:gtd | M3 AUC | 0.896 | pending | needs the full data (160 events now) |
| src:start:gtd | M3 top1% | 0.411 | pending | needs the full data (160 events now) |
| src:fdic:financials | SVB, lens A, 2022 year-end: rank | 5 | 5 | ok |
| src:fdic:financials | SVB, lens A, 2022 year-end: of | 4683 | 4683 | ok |
| src:fdic:financials | SVB, lens A, 2022 year-end: z | 3.6 | 3.599 | ok |
| src:fdic:financials | SVB, lens A, 2022 year-end: uninsured | 0.864 | 0.864 | ok |
| src:fdic:financials | SVB, lens A, 2022 year-end: unreal_loss_to_equity | 1.144 | 1.144 | ok |
| src:fdic:financials | Signature, lens A, 2022 year-end: rank | 324 | 324 | ok |
| src:fdic:financials | Republic First, lens A, 2023 year-end: rank | 16 | pending | no report of that quarter yet |
| src:fdic:financials | lens A 2006: in top 50 / AUC | 0 / 0.367 | pending | no year-end report yet |
| src:fdic:financials | lens B 2006: in top 50 / AUC | 2 / 0.671 | pending | no year-end report yet |
| src:fdic:financials | lens A 2007: in top 50 / AUC | 0 / 0.552 | pending | no year-end report yet |
| src:fdic:financials | lens B 2007: in top 50 / AUC | 7 / 0.707 | pending | no year-end report yet |
| src:fdic:financials | lens A 2008: in top 50 / AUC | 2 / 0.516 | pending | no year-end report yet |
| src:fdic:financials | lens B 2008: in top 50 / AUC | 26 / 0.951 | pending | no year-end report yet |
| src:fdic:financials | lens A 2009: in top 50 / AUC | 2 / 0.517 | pending | no year-end report yet |
| src:fdic:financials | lens B 2009: in top 50 / AUC | 27 / 0.957 | pending | no year-end report yet |
| src:fdic:financials | lens A 2010: in top 50 / AUC | 0 / 0.419 | pending | no year-end report yet |
| src:fdic:financials | lens B 2010: in top 50 / AUC | 12 / 0.952 | pending | no year-end report yet |
| src:fdic:financials | lens A 2011: in top 50 / AUC | 0 / 0.364 | pending | no year-end report yet |
| src:fdic:financials | lens B 2011: in top 50 / AUC | 15 / 0.984 | pending | no year-end report yet |
| src:fdic:financials | lens A 2012: in top 50 / AUC | 0 / 0.359 | pending | no year-end report yet |
| src:fdic:financials | lens B 2012: in top 50 / AUC | 10 / 0.98 | pending | no year-end report yet |
| src:fdic:financials | lens A 2013: in top 50 / AUC | 0 / 0.342 | pending | no year-end report yet |
| src:fdic:financials | lens B 2013: in top 50 / AUC | 8 / 0.953 | pending | no year-end report yet |
| src:fdic:financials | lens A 2014: in top 50 / AUC | 0 / 0.39 | pending | no year-end report yet |
| src:fdic:financials | lens B 2014: in top 50 / AUC | 3 / 0.916 | pending | no year-end report yet |
| src:fdic:financials | lens A 2015: in top 50 / AUC | 0 / 0.341 | pending | no year-end report yet |
| src:fdic:financials | lens B 2015: in top 50 / AUC | 7 / 1.0 | pending | no year-end report yet |
| src:fdic:financials | lens A 2016: in top 50 / AUC | 0 / 0.175 | pending | no year-end report yet |
| src:fdic:financials | lens B 2016: in top 50 / AUC | 2 / 0.822 | pending | no year-end report yet |
| src:fdic:financials | lens A 2018: in top 50 / AUC | 0 / 0.334 | pending | no year-end report yet |
| src:fdic:financials | lens B 2018: in top 50 / AUC | 3 / 0.822 | pending | no year-end report yet |
| src:fdic:financials | lens A 2019: in top 50 / AUC | 0 / 0.134 | pending | no year-end report yet |
| src:fdic:financials | lens B 2019: in top 50 / AUC | 4 / 0.998 | pending | no year-end report yet |
| src:fdic:financials | lens A 2022: in top 50 / AUC | 1 / 0.9 | pending | pending: failures recorded only to 2023-03-12 00:00:00 (< 2024-02-15) |
| src:fdic:financials | lens B 2022: in top 50 / AUC | 0 / 0.71 | pending | pending: failures recorded only to 2023-03-12 00:00:00 (< 2024-02-15) |
| src:fdic:financials | lens A 2023: in top 50 / AUC | 1 / 0.366 | pending | no year-end report yet |
| src:fdic:financials | lens B 2023: in top 50 / AUC | 0 / 0.608 | pending | no year-end report yet |

## 7. Coverage

| Source | Bucket | Lag (h) / L | Type | Events | Shape and rule | Conclusions |
|---|---|---|---|---|---|---|
| src:cisa:kev | week | 0.0 / 0 | CYB.VULN.KNOWN_EXPLOITED | 1,739 | occurrence (no measures (0% of events)); subject: logical prefix at depth 2 | forecast 1,040, forecast_score 101 |
| src:delphi:hhs-admissions | week | 96.0 / 0 | HLT.SIGNAL.DAILY | 200 | occurrence (only 19% of (subject, period) pairs hold one record (< 80%)); subject: logical prefix at depth 2 | forecast 7, forecast_score 7 |
| src:fdic:failures | week | 0.0 / 0 | FIN.BANK.FAILURE | 68 | measurement: 100% of 68 (subject, period) pairs hold one record (>= 80%) | none |
| src:fdic:financials | quarter | 1080.0 / 0 | FIN.BANK.QUARTER_REPORT | 14,227 | measurement: 100% of 14227 (subject, period) pairs hold one record (>= 80%) | joint_anomaly 300 |
| src:first:epss | week | 0.0 / 0 | CYB.VULN.EPSS_SCORE | 100 | measurement: 100% of 100 (subject, period) pairs hold one record (>= 80%) | none |
| src:gdelt:events | week | 124.6 / 0 | SEC.NEWS.CONFLICT | 200 | occurrence (only 38% of (subject, period) pairs hold one record (< 80%)); subject: H3 r3 cell (>= 50% of events have one) | none |
| src:hn:algolia | week | 0.0 / 0 | TXT.POST | 93 | occurrence (no measures (0% of events)); subject: logical prefix at depth 2 | forecast 52, forecast_score 2 |
|  |  |  | TXT.POST.ENGAGEMENT | 93 | occurrence (only 0% of (subject, period) pairs hold one record (< 80%)); subject: logical prefix at depth 2 |  |
| src:jhu:csse-covid | week | 0.0 / 0 | HLT.SIGNAL.DAILY | 200 | measurement: 100% of 200 (subject, period) pairs hold one record (>= 80%) | none |
| src:nist:nvd | week | 0.0 / 0 | CYB.VULN.NVD_ASSESSMENT | 66 | occurrence (no measures (0% of events)); subject: first participant entity | forecast 1,040, forecast_score 3 |
|  |  |  | CYB.VULN.PUBLISHED | 67 | occurrence (no measures (0% of events)); subject: first participant entity |  |
| src:polymarket:prices | week | 0.0 / 0 | MKT.PREDICTION.PRICE | 200 | measurement: 100% of 200 (subject, period) pairs hold one record (>= 80%) | none |
| src:ransomware-live:victims | week | 0.0 / 0 | CYB.RANSOM.VICTIM_POSTED | 176 | occurrence (no measures (0% of events)); subject: logical prefix at depth 2 | forecast 1,040, forecast_score 2, spark 1 |
| src:reddit:arcticshift | week | 0.0 / 0 | TXT.POST | 100 | occurrence (no measures (0% of events)); subject: logical prefix at depth 2 | forecast 52, forecast_score 2 |
|  |  |  | TXT.POST.ENGAGEMENT | 100 | occurrence (only 0% of (subject, period) pairs hold one record (< 80%)); subject: logical prefix at depth 2 |  |
| src:start:gtd | week | 0.0 / 0 | SEC.ATTACK.ARMED_ASSAULT | 8 | pinned in engine.yaml: H3 r3, week | none |
|  |  |  | SEC.ATTACK.ASSASSINATION | 6 | pinned in engine.yaml: H3 r3, week |  |
|  |  |  | SEC.ATTACK.BOMBING | 89 | pinned in engine.yaml: H3 r3, week |  |
|  |  |  | SEC.ATTACK.FACILITY | 49 | pinned in engine.yaml: H3 r3, week |  |
|  |  |  | SEC.ATTACK.HIJACKING | 3 | pinned in engine.yaml: H3 r3, week |  |
|  |  |  | SEC.ATTACK.KIDNAPPING | 11 | pinned in engine.yaml: H3 r3, week |  |
|  |  |  | SEC.ATTACK.UNKNOWN | 3 | pinned in engine.yaml: H3 r3, week |  |
| src:tukaani:xz-git | week | 0.0 / 0 | CODE.COMMIT | 198 | measurement: 100% of 198 (subject, period) pairs hold one record (>= 80%) | spark 4 |
| src:ucdp:ged | week | 672.0 / 4 (pinned; measured p99 9384 h) | SEC.CONFLICT.NON_STATE | 45 | pinned in engine.yaml: H3 r3, week | none |
|  |  |  | SEC.CONFLICT.ONE_SIDED | 21 | pinned in engine.yaml: H3 r3, week |  |
|  |  |  | SEC.CONFLICT.STATE_BASED | 72 | pinned in engine.yaml: H3 r3, week |  |
| src:usgs:comcat | day | 0.9 / 0 | GEO.EARTHQUAKE | 200 | pinned in engine.yaml: H3 r2, day | forecast 1,060 |
| src:wikipedia:polls | week | 24.0 / 0 | POL.POLL.STATE | 109 | occurrence (only 66% of (subject, period) pairs hold one record (< 80%)); subject: first participant entity | none |

Operators per series (model cards):

- `src:cisa:kev|logical`: 1,739 events (0 arrived after their bucket closed and are left out of the inputs), 725 subjects, weeks 2021-11-01..2026-10-12, calibration 154 buckets (frozen 2024-10-14 00:00:00), M3 (a, decay, gamma) = (0.02, 0.3, 0.0), spark threshold 86.560; test: M3 AUC 0.4734707573438679, top1% 0.157 vs routine AUC 0.45837977202254143, top1% 0.157
- `src:delphi:hhs-admissions|logical`: 200 events (0 arrived after their bucket closed and are left out of the inputs), 1 subjects, weeks 2020-03-23..2020-07-06, calibration 9 buckets (frozen 2020-05-29 00:00:00), M3 (a, decay, gamma) = (0.2, 0.7, 0.0), spark threshold 45.239; test: M3 AUC None, top1% 1.000 vs routine AUC None, top1% 1.000
- `src:fdic:failures|measurement|FIN.BANK.FAILURE`: own past, 128 values, 0 with a z (>= 8 previous changes, MAD > 0), calibration 2783 buckets (frozen 1987-08-17 00:00:00), |z| threshold inf
- `src:fdic:financials|lenses`: lenses A and B on 2022-10-01..2023-07-01; year-end scores: A|2022: pending: failures recorded only to 2023-03-12 00:00:00 (< 2024-02-15); B|2022: pending: failures recorded only to 2023-03-12 00:00:00 (< 2024-02-15)
- `src:fdic:financials|measurement|FIN.BANK.QUARTER_REPORT`: own past, 0 values: not covered: only 1 calibration buckets (< 8)
- `src:first:epss|measurement|CYB.VULN.EPSS_SCORE`: own past, 0 values: not covered: only 0 calibration buckets (< 8)
- `src:gdelt:events|cell`: not covered: only 3 calibration buckets (< 8) (200 events, 21 subjects)
- `src:hn:algolia|logical`: 186 events (0 arrived after their bucket closed and are left out of the inputs), 1 subjects, weeks 2021-11-01..2026-10-19, calibration 155 buckets (frozen 2024-10-21 00:00:00), M3 (a, decay, gamma) = (0.02, 0.3, 0.0), spark threshold 294.000; test: M3 AUC None, top1% 1.000 vs routine AUC None, top1% 1.000
- `src:jhu:csse-covid|measurement|HLT.SIGNAL.DAILY`: own past, 200 values, 126 with a z (>= 8 previous changes, MAD > 0), calibration 17 buckets (frozen 2020-05-18 00:00:00), |z| threshold 19.80
- `src:nist:nvd|participant`: 133 events (0 arrived after their bucket closed and are left out of the inputs), 67 subjects, weeks 1998-12-28..2026-06-22, calibration 860 buckets (frozen 2015-06-22 00:00:00), M3 (a, decay, gamma) = (0.02, 0.3, 0.0), spark threshold 9.000; test: M3 AUC 0.5146825396825397, top1% 0.015 vs routine AUC 0.5146825396825397, top1% 0.015
- `src:polymarket:prices|measurement|MKT.PREDICTION.PRICE`: own past, 0 values: not covered: only 1 calibration buckets (< 8)
- `src:ransomware-live:victims|logical`: 176 events (0 arrived after their bucket closed and are left out of the inputs), 24 subjects, weeks 2019-12-30..2024-04-22, calibration 135 buckets (frozen 2022-08-01 00:00:00), M3 (a, decay, gamma) = (0.1, 0.5, 0.0), spark threshold 9.000; test: M3 AUC 0.0, top1% 0.000 vs routine AUC 0.0, top1% 0.000
- `src:reddit:arcticshift|logical`: 200 events (0 arrived after their bucket closed and are left out of the inputs), 1 subjects, weeks 2020-06-29..2026-10-19, calibration 197 buckets (frozen 2024-04-08 00:00:00), M3 (a, decay, gamma) = (0.02, 0.3, 0.0), spark threshold 300.000; test: M3 AUC None, top1% 1.000 vs routine AUC None, top1% 1.000
- `src:start:gtd|cell`: not covered: no bucket after the calibration span (the data ends 1970-06-29) (160 events, 58 subjects)
- `src:tukaani:xz-git|measurement|CODE.COMMIT`: own past, 198 values, 188 with a z (>= 8 previous changes, MAD > 0), calibration 24 buckets (frozen 2008-05-19 00:00:00), |z| threshold 10.24
- `src:ucdp:ged|cell`: not covered: no bucket after the calibration span (the data ends 1989-01-09) (50 events, 23 subjects)
- `src:usgs:comcat|cell`: 200 events (2 arrived after their bucket closed and are left out of the inputs), 86 subjects, days 1965-01-01..2016-12-31, calibration 9131 buckets (frozen 1990-01-01 00:54:07), M3 (a, decay, gamma) = (0.03, 0.7, 0.4), spark threshold 21.157; test: M3 AUC None, top1% 0.000 vs routine AUC None, top1% 0.000
- `src:wikipedia:polls|participant`: not covered: only 2 calibration buckets (< 8) (109 events, 54 subjects)
- `src:fdic:financials`: FIN.BANK.QUARTER_REPORT (own past): only 1 calibration buckets (< 8)
- `src:first:epss`: CYB.VULN.EPSS_SCORE (own past): only 0 calibration buckets (< 8)
- `src:gdelt:events`: SEC.NEWS.CONFLICT: only 3 calibration buckets (< 8)
- `src:polymarket:prices`: MKT.PREDICTION.PRICE (own past): only 1 calibration buckets (< 8)
- `src:start:gtd`: SEC.ATTACK.ARMED_ASSAULT, SEC.ATTACK.ASSASSINATION, SEC.ATTACK.BOMBING, SEC.ATTACK.FACILITY, SEC.ATTACK.HIJACKING, SEC.ATTACK.KIDNAPPING, SEC.ATTACK.UNKNOWN: no bucket after the calibration span (the data ends 1970-06-29)
- `src:ucdp:ged`: SEC.CONFLICT.NON_STATE, SEC.CONFLICT.ONE_SIDED, SEC.CONFLICT.STATE_BASED: no bucket after the calibration span (the data ends 1989-01-09)
- `src:wikipedia:polls`: POL.POLL.STATE: only 2 calibration buckets (< 8)

## Conclusions per source and type

| Source | Type | Count |
|---|---|---|
| src:cisa:kev | forecast | 1,040 |
| src:cisa:kev | forecast_score | 101 |
| src:delphi:hhs-admissions | forecast | 7 |
| src:delphi:hhs-admissions | forecast_score | 7 |
| src:fdic:financials | joint_anomaly | 300 |
| src:hn:algolia | forecast | 52 |
| src:hn:algolia | forecast_score | 2 |
| src:nist:nvd | forecast | 1,040 |
| src:nist:nvd | forecast_score | 3 |
| src:ransomware-live:victims | forecast | 1,040 |
| src:ransomware-live:victims | forecast_score | 2 |
| src:ransomware-live:victims | spark | 1 |
| src:reddit:arcticshift | forecast | 52 |
| src:reddit:arcticshift | forecast_score | 2 |
| src:tukaani:xz-git | spark | 4 |
| src:usgs:comcat | forecast | 1,060 |
| (map cells) | MapCell | 20 |
