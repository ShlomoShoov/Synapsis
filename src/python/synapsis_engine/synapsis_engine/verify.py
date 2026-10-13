"""python -m synapsis_engine verify: the seven proofs, written to engine-report.md."""
import collections
import datetime
import json
import sys

import h3
import numpy as np
import pandas as pd

from . import backfill, config, findings, m3
from .__main__ import compute, digest, load_workspace, records_of, volume
from .io import api_client, mongo_reader

TOL = 1e-12
GOLDEN_MIN_EVENTS = {"src:usgs:comcat": 20000, "src:ucdp:ged": 300000, "src:start:gtd": 45000}


def same_maths():
    """The engine's functions against reference/analytics.py (and m3_sparse.py, ucdp_gtd.py) on identical arrays."""
    sys.path.insert(0, str(config.ROOT / "reference"))
    import analytics as A
    import m3_sparse as MS
    rng = np.random.default_rng(1)
    cells = sorted(h3.grid_disk(h3.latlng_to_cell(38.297, 142.373, 2), 2))
    T, S = 300, len(cells)
    C = rng.poisson(0.4, (T, S)).astype(float)
    Nd = A.neighbor_matrix(A.Series(cells, pd.date_range("2000-01-01", periods=T), C, C), "h3")
    Ns = m3.neighbours(cells)
    cal, test = np.arange(200), np.arange(200, T)
    p = (0.1, 0.7, 0.3)
    lam = m3.expectation(C, Ns, p)
    out = []

    def chk(name, x, y):
        out.append((name, float(np.max(np.abs(np.asarray(x, dtype=float) - np.asarray(y, dtype=float))))))

    chk("background (routine)", m3.background(C), A.background(C))
    chk("excitation with neighbours (dense N)", m3.excitation(C, Ns, 0.7, 0.3), A.excitation(C, Nd, 0.7, 0.3))
    chk("excitation (m3_sparse)", m3.excitation(C, Ns, 0.7, 0.3), MS.excitation(C, Ns, 0.7, 0.3))
    chk("expectations M3", lam, A.expectations(C, "M3", Nd, p))
    chk("expectations M1 (routine)", m3.routine(C), A.expectations(C, "M1"))
    ref_lag = (m3.shift(A.background(C), 4) + 0.1 * 0.7 ** 4 * m3.shift(MS.excitation(C, Ns, 0.7, 0.3), 4) + 1e-9)
    chk("expectation with lag 4 (ucdp_gtd.py)", m3.expectation(C, Ns, p, 4), ref_lag)
    chk("poisson_ll", m3.poisson_ll(C, lam, cal), A.poisson_ll(C, lam, cal))
    grid = dict(a=[0.02, 0.1, 0.4], decay=[0.5, 0.85], gamma=[0.0, 0.3])
    (pa, lla), (pe, lle) = A.fit_m3(C, Nd, cal, grid), m3.fit(C, Ns, cal, grid)
    chk("fit_m3: log-likelihood", lle, lla)
    out.append(("fit_m3: parameters", 0.0 if tuple(pa) == tuple(pe) else float("inf")))
    ms_grid = dict(a=[0.01, 0.02, 0.05, 0.1, 0.2, 0.4], decay=[0.3, 0.5, 0.7, 0.85, 0.95], gamma=[0.0, 0.1])
    out.append(("m3_sparse.fit: parameters",
                0.0 if tuple(MS.fit(C, Ns, cal, [0.0, 0.1])) == tuple(m3.fit(C, Ns, cal, ms_grid)[0]) else float("inf")))
    Sx = A.surprise(C, lam)
    chk("surprise", m3.surprise(C, lam), Sx)
    chk("threshold_for_budget", m3.threshold_for_budget(Sx[cal].ravel(), 0.005), A.threshold_for_budget(Sx, cal, 0.005))
    er, ee = A.evaluate_forecast(C, lam, test), m3.evaluate_forecast(C, lam, test)
    chk("evaluate_forecast: AUC", ee["auc"], er["auc"])
    chk("evaluate_forecast: top 1% share", ee["share_of_events_in_top_k"], er["share_of_events_in_top_k"])
    return out


def no_hindsight(out):
    bad = [(r["id"], str(m["counted_max"]), r["known_at"]) for r, m in out
           if m["counted_max"] is not None and m["counted_max"] > m["known_at"]]
    return sum(m["counted_max"] is not None for _, m in out), bad


def canon(r):
    return json.dumps(r, sort_keys=True, separators=(",", ":"))


def truncated(ws, decl, shapes, cards, out):
    """For 3 issue dates per source: the run on records with known_at <= issue gives the same conclusions."""
    res = []
    for source in sorted({m["source"] for _, m in out}):
        full = [(r, m) for r, m in out if m["source"] == source and r["type"] != "forecast_score"]
        times = sorted({m["known_at"] for _, m in full})
        if not times:
            continue
        picks = sorted({times[len(times) // 4], times[len(times) // 2], times[-1]})
        src_cards = {k: c for k, c in cards.items() if k.startswith(source + "|")}
        for tau in picks:
            got, _, _ = backfill.run(ws, decl, {source: shapes[source]}, source, src_cards, tau)
            want = {canon(r) for r, m in full if m["known_at"] <= tau}
            have = {canon(r) for r, _ in got}
            res.append((source, tau, len(want), len(have), want == have))
    return res


def placebo(ws, decl, cards):
    """Bucket order shuffled (seed 7): M3 must not beat the routine by more than 1 point of top 1%."""
    res = []
    for ck, card in sorted(cards.items()):
        if card.get("skip") or "params" not in card or card["t_end"] <= card["t_cal"]:
            continue
        s = backfill.series_for(ws, card)
        perm = np.random.default_rng(7).permutation(len(s.B))
        Cin, Cout = s.C_in[perm], s.C_full[perm]
        N = m3.neighbours(s.subjects) if card["kind"] == "cell" else None
        pin = decl["pins"].get(card["source"])
        grid = dict(pin["grid"] if pin else decl["default_grid"])
        if N is None:
            grid["gamma"] = [0.0]
        p, _ = m3.fit(Cin, N, np.arange(card["t_cal"]), grid, card["L"])
        test = np.arange(card["t_cal"], card["t_end"])
        em = m3.evaluate_forecast(Cout, m3.expectation(Cin, N, p, card["L"]), test)
        er = m3.evaluate_forecast(Cout, m3.routine(Cin, card["L"]), test)
        res.append((ck, p, em["share"], er["share"], em["share"] - er["share"] <= 0.01))
    return res


def golden_occurrences(ws, cards):
    """reference/EXPECTED.md sections 1-3, from the engine's own series (pending until the full data is loaded)."""
    rows = []
    exp = {"src:usgs:comcat": [("routine AUC", "auc_routine", 0.842, 0.003), ("routine top1%", "top1_routine", 0.151, 0.005),
                               ("M3 + neighbours AUC", "auc_model", 0.852, 0.003), ("M3 + neighbours top1%", "top1_model", 0.216, 0.005)],
           "src:ucdp:ged": [("lag L", "L", 4, 0), ("routine AUC", "auc_routine", 0.878, 0.005), ("routine top1%", "top1_routine", 0.432, 0.01),
                            ("M3 AUC", "auc_model", 0.919, 0.005), ("M3 top1%", "top1_model", 0.589, 0.01)],
           "src:start:gtd": [("routine AUC", "auc_routine", 0.876, 0.005), ("routine top1%", "top1_routine", 0.335, 0.01),
                             ("M3 AUC", "auc_model", 0.896, 0.005), ("M3 top1%", "top1_model", 0.411, 0.01)]}
    for source, checks in exp.items():
        card = cards.get(source + "|cell")
        n = card["events"] if card else 0
        if not card or n < GOLDEN_MIN_EVENTS[source] or "scores" not in card:
            rows += [(source, name, want, "pending", f"needs the full data ({n:,} events now)") for name, _, want, _ in checks]
            continue
        got = dict(card["scores"], L=card["L"])
        for name, k, want, tol in checks:
            rows.append((source, name, want, round(got[k], 3), "ok" if abs(got[k] - want) <= tol else "FAIL"))
        if source == "src:usgs:comcat":
            rows += usgs_extra(ws, card)
    return rows


LENS_GOLDEN = [("A|2022-10-01|ent:fdic:cert-24735", "SVB, lens A, 2022 year-end", {"rank": 5, "of": 4683, "z": 3.60, "uninsured": 0.864, "unreal_loss_to_equity": 1.144}),
               ("A|2022-10-01|ent:fdic:cert-57053", "Signature, lens A, 2022 year-end", {"rank": 324}),
               ("A|2023-10-01|ent:fdic:cert-27332", "Republic First, lens A, 2023 year-end", {"rank": 16})]
LENS_YEARS = {2006: (0, 0.367, 2, 0.671), 2007: (0, 0.552, 7, 0.707), 2008: (2, 0.516, 26, 0.951), 2009: (2, 0.517, 27, 0.957),
              2010: (0, 0.419, 12, 0.952), 2011: (0, 0.364, 15, 0.984), 2012: (0, 0.359, 10, 0.980), 2013: (0, 0.342, 8, 0.953),
              2014: (0, 0.390, 3, 0.916), 2015: (0, 0.341, 7, 1.000), 2016: (0, 0.175, 2, 0.822), 2018: (0, 0.334, 3, 0.822),
              2019: (0, 0.134, 4, 0.998), 2022: (1, 0.900, 0, 0.710), 2023: (1, 0.366, 0, 0.608)}


def golden_lenses(cards):
    """reference/EXPECTED.md section 4."""
    card = next((c for k, c in cards.items() if k.endswith("|lenses")), {})
    ranks, scores, rows = card.get("ranks", {}), card.get("lens_scores", {}), []
    for k, name, want in LENS_GOLDEN:
        got = ranks.get(k)
        if got is None:
            rows += [("src:fdic:financials", f"{name}: {m}", v, "pending", "no report of that quarter yet") for m, v in want.items()]
            continue
        same_count = got["of"] == want.get("of", got["of"])
        for m, v in want.items():
            g = got[m]
            tol = (0 if same_count else 1) if m == "rank" else (0 if m == "of" else 0.005 if m != "z" else 0.01)
            if m == "rank" and "of" not in want:
                tol = 0 if not same_count else 0
            rows.append(("src:fdic:financials", f"{name}: {m}", v, round(g, 3) if isinstance(g, float) else g,
                         "ok" if abs(g - v) <= tol else ("FAIL" if m == "rank" and "of" in want or m != "rank" else "differs")))
    for y, (a_top, a_auc, b_top, b_auc) in LENS_YEARS.items():
        for lens, top, auc in (("A", a_top, a_auc), ("B", b_top, b_auc)):
            got = scores.get(f"{lens}|{y}")
            if not isinstance(got, dict):
                rows.append(("src:fdic:financials", f"lens {lens} {y}: in top 50 / AUC", f"{top} / {auc}", "pending", got or "no year-end report yet"))
                continue
            ok = abs(got["failed_in_top50"] - top) <= 1 and got["auc"] is not None and abs(got["auc"] - auc) <= 0.01
            rows.append(("src:fdic:financials", f"lens {lens} {y}: in top 50 / AUC", f"{top} / {auc}",
                         f"{got['failed_in_top50']} / {findings.fmt(got['auc'])}", "ok" if ok else "FAIL"))
    return rows


def usgs_extra(ws, card):
    s = backfill.series_for(ws, card)
    N = m3.neighbours(s.subjects)
    test = np.arange(card["t_cal"], card["t_end"])
    rows = [("src:usgs:comcat", "cells", 1257, len(s.subjects), "ok" if abs(len(s.subjects) - 1257) <= 10 else "FAIL")]
    cell = h3.latlng_to_cell(38.297, 142.373, 2)
    t = int(s.B.get_loc(pd.Timestamp("2011-03-11")))
    for name, p, best in (("M3 own cell", card["params_own"], 5), ("M3 + neighbours", card["params"], 3)):
        lam = m3.expectation(s.C_in, N, p, card["L"])
        e = m3.evaluate_forecast(s.C_full, lam, test)
        if name == "M3 own cell":
            rows += [("src:usgs:comcat", f"{name} AUC", 0.851, round(e["auc_raw"], 3), "ok" if abs(e["auc_raw"] - 0.851) <= 0.003 else "FAIL"),
                     ("src:usgs:comcat", f"{name} top1%", 0.214, round(e["share"], 3), "ok" if abs(e["share"] - 0.214) <= 0.005 else "FAIL")]
        rank = int((lam[t] > lam[t, s.subjects.index(cell)]).sum()) + 1 if cell in s.subjects else None
        rows.append(("src:usgs:comcat", f"Tohoku rank on 2011-03-11, {name} {tuple(p)}", best, rank,
                     "ok" if rank is not None and rank <= 5 else "FAIL"))
    return rows


def coverage(shapes, cards, notes, out):
    vol = collections.Counter((m["source"], r["type"]) for r, m in out)
    lines = ["| Source | Bucket | Lag (h) / L | Type | Events | Shape and rule | Conclusions |", "|---|---|---|---|---|---|---|"]
    for source, sh in shapes.items():
        con = ", ".join(f"{t} {n:,}" for (s, t), n in sorted(vol.items()) if s == source) or "none"
        first = True
        for typ, t in sh["types"].items():
            lag = f"{sh['lag_hours']:.1f} / {sh['L']}" if sh["lag_hours"] is not None else "-"
            if sh.get("lag_measured") is not None:
                lag += f" (pinned; measured p99 {sh['lag_measured']:.0f} h)"
            lines.append(f"| {source if first else ''} | {sh['bucket'] if first else ''} | {lag if first else ''} | {typ} | "
                         f"{t['n']:,} | {t['rule']} | {con if first else ''} |")
            first = False
    lines += ["", "Operators per series (model cards):", ""]
    for ck, c in sorted(cards.items()):
        if c["kind"] == "measurement":
            lines.append(f"- `{ck}`: own past, {c['values']:,} values" + (f": not covered: {c['skip']}" if c.get("skip") else
                         f", {c['with_z']:,} with a z (>= {8} previous changes, MAD > 0), calibration {c['t_cal']} "
                         f"buckets (frozen {c['freeze']}), |z| threshold {max(4, c['threshold']):.2f}"))
        elif c["kind"] == "lenses":
            sc = "; ".join(f"{k}: " + (v if isinstance(v, str) else f"{v['failed_in_top50']} of {v['failed']} failures in the top 50, AUC {v['auc']}")
                           for k, v in sorted(c.get("lens_scores", {}).items()))
            lines.append(f"- `{ck}`: lenses A and B on {c.get('start')}..{c.get('end')}" + (f": {c['skip']}" if c.get("skip") else "")
                         + (f"; year-end scores: {sc}" if sc else ""))
        elif c.get("skip"):
            lines.append(f"- `{ck}`: not covered: {c['skip']} ({c['events']:,} events, {c['subjects']:,} subjects)")
        else:
            sc = c.get("scores", {})
            lines.append(f"- `{ck}`: {c['events']:,} events ({c['late']:,} arrived after their bucket closed and are left "
                         f"out of the inputs), {c['subjects']:,} subjects, {c['bucket']}s {c['start']}..{c['end']}, "
                         f"calibration {c['t_cal']} buckets (frozen {c['freeze']}), M3 (a, decay, gamma) = "
                         f"{tuple(c['params'])}, spark threshold {c['threshold']:.3f}"
                         + (f"; test: M3 AUC {sc['auc_model']}, top1% {sc['top1_model']:.3f} vs routine AUC "
                            f"{sc['auc_routine']}, top1% {sc['top1_routine']:.3f}" if sc else ""))
    for source, ns in notes.items():
        for n in ns:
            lines.append(f"- `{source}`: {n}")
    return lines


def main():
    decl = config.load()
    ws = load_workspace()
    shapes, out, cards, notes = compute(ws, decl)
    recs = records_of(out)
    L = [f"# Engine report", "", f"Generated {datetime.datetime.now(datetime.UTC):%Y-%m-%d %H:%M} UTC by "
         f"`python -m synapsis_engine verify`. Records: {len(recs):,} ({len(out):,} conclusions).", ""]
    ok = {}

    m = same_maths()
    ok[1] = all(d <= TOL for _, d in m)
    L += ["## 1. Same maths (tolerance 1e-12)", "", "| Function | Max difference |", "|---|---|"]
    L += [f"| {n} | {d:.1e} |" for n, d in m] + [""]

    _, out2, _, _ = compute(ws, decl)
    d1, d2 = digest(recs), digest(records_of(out2))
    first = api_client.post(recs)
    second = api_client.post(recs)
    ok[2] = d1 == d2 and second["unchanged"] == len(recs) and second["rejected"] == 0
    L += ["## 2. Determinism", "", f"- Two backfills: sha256 `{d1}` and `{d2}`: {'same' if d1 == d2 else 'DIFFERENT'}.",
          f"- POST: first {json.dumps({k: first[k] for k in ('received', 'new', 'changed', 'unchanged', 'rejected')})}; "
          f"second {json.dumps({k: second[k] for k in ('received', 'new', 'changed', 'unchanged', 'rejected')})}.", ""]

    n_checked, bad = no_hindsight(out)
    tr = truncated(ws, decl, shapes, cards, out)
    ok[3] = not bad and all(r[4] for r in tr)
    L += ["## 3. No hindsight", "", f"- Every event counted for a conclusion has known_at <= the conclusion's known_at: "
          f"{n_checked:,} conclusions checked, {len(bad)} violations." + (f" First: {bad[:3]}" if bad else ""),
          "- Runs on records truncated at known_at <= the issue time (frozen model cards):", "",
          "| Source | Issue time | Conclusions (full run) | Conclusions (truncated) | Same |", "|---|---|---|---|---|"]
    L += [f"| {s} | {t} | {a:,} | {b:,} | {'yes' if e else 'NO'} |" for s, t, a, b, e in tr] + [""]

    pl = placebo(ws, decl, cards)
    ok[4] = all(r[4] for r in pl)
    L += ["## 4. Placebo (bucket order shuffled, seed 7)", "", "| Series | M3 refit | M3 top1% | routine top1% | M3 - routine <= 0.01 |",
          "|---|---|---|---|---|"]
    L += [f"| {k} | {tuple(p)} | {a:.3f} | {b:.3f} | {'yes' if e else 'NO'} |" for k, p, a, b, e in pl] + [""]

    mongo, duck = mongo_reader.first_state_counts(), ws.counts()
    ok[5] = mongo == duck
    L += ["## 5. Pipeline: events per source, MongoDB (first states) = DuckDB", "", "| Source | MongoDB | DuckDB |", "|---|---|---|"]
    L += [f"| {s} | {mongo.get(s, 0):,} | {duck.get(s, 0):,} |" for s in sorted(set(mongo) | set(duck))] + [""]

    g = golden_occurrences(ws, cards) + golden_lenses(cards)
    ok[6] = all(r[4] != "FAIL" for r in g)
    L += ["## 6. Golden values (reference/EXPECTED.md)", "", "| Source | Value | Expected | Engine | Result |", "|---|---|---|---|---|"]
    L += [f"| {s} | {n} | {w} | {v} | {e} |" for s, n, w, v, e in g] + [""]

    L += ["## 7. Coverage", ""] + coverage(shapes, cards, notes, out) + [""]
    L += ["## Conclusions per source and type", "", "| Source | Type | Count |", "|---|---|---|"]
    L += [f"| {s} | {t} | {n:,} |" for (s, t), n in volume(out)] + [f"| (map cells) | MapCell | {len(recs) - len(out):,} |", ""]
    L.insert(4, "Summary: " + ", ".join(f"{k} {'pass' if v else 'FAIL'}" for k, v in ok.items()) + "\n")
    text = "\n".join(L)
    (config.data_dir() / "engine-report.md").write_text(text)
    print(text)
    return 0 if all(ok.values()) else 1
