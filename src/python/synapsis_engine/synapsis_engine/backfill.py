"""Backfill: period by period, only what was known.

Every source has an issue schedule: the forecast for bucket t is issued at tau[t] = end of bucket t-1-L + lag, and
bucket b closes at close[b] = end of b + lag (= tau[b+1+L]). An event counts (in C_in) only if it was known when its
bucket closed; later arrivals are left out of every input, so a run on records truncated at any issue time gives
the same conclusions. The model card (shape, lag, spans, fitted parameters, spark threshold) is learned from the
calibration buckets and frozen when the last of them closes; conclusions are issued only from then on.
"""
import math

import numpy as np
import pandas as pd

from . import anomalies, config, findings, m3

FORECAST_TOP = 20
FORECAST_LAST = 52
NAT = np.iinfo(np.int64).min


class Series:
    """One occurrence series: counts per (bucket, subject) on a fixed axis, with the issue schedule."""

    def __init__(self, ev, bucket, start, end, L, lag_hours):
        self.bucket, self.L = bucket, L
        off = config.offset(bucket)
        self.B = B = pd.date_range(start, end, freq=config.FREQ[bucket])
        ti = B.get_indexer(ev.bucket)
        ev = ev[ti >= 0].copy()
        ev["ti"] = ti[ti >= 0]
        self.subjects = sorted(ev.subject.unique())
        si = {s: i for i, s in enumerate(self.subjects)}
        ev["si"] = ev.subject.map(si).astype(int)
        lag = pd.Timedelta(seconds=math.ceil(lag_hours * 3600))
        ext = pd.date_range(B[0] - L * off, periods=len(B) + L + 1, freq=config.FREQ[bucket])
        self.tau = (ext + lag).values                       # tau[t]: issue time of the forecast for row t
        self.close = self.tau[L + 1:L + 1 + len(B)]          # close[b]: when bucket b's events are counted
        ev["counted"] = ev.known_at.values <= self.close[ev.ti.values]
        T, S = len(B), len(self.subjects)
        self.C_full = np.zeros((T, S))
        np.add.at(self.C_full, (ev.ti.values, ev.si.values), 1)
        c = ev[ev.counted]
        self.C_in = np.zeros((T, S))
        np.add.at(self.C_in, (c.ti.values, c.si.values), 1)
        self.ev = ev.sort_values(["si", "ti", "id"]).reset_index(drop=True)
        self.evc = self.ev[self.ev.counted].reset_index(drop=True)
        self.dropped_late = int((~ev.counted).sum())
        self.max_in = np.maximum.accumulate(self._row_max(c))     # latest known_at counted in rows <= b
        self.row_full = self._row_max(ev)                          # latest known_at of all events of row b

    def _row_max(self, ev):
        m = np.full(len(self.B), NAT, dtype=np.int64)
        np.maximum.at(m, ev.ti.values, ev.known_at.values.astype("datetime64[ns]").astype(np.int64))
        return m

    def premises(self, s, last_row, first_row=None, n=50):
        """Ids of the counted events of subject s in rows [first_row, last_row], the latest n."""
        e = self.evc
        lo, hi = np.searchsorted(e.si.values, [s, s + 1])
        ti = e.ti.values[lo:hi]
        a = lo + (np.searchsorted(ti, first_row) if first_row is not None else 0)
        b = lo + np.searchsorted(ti, last_row, side="right")
        return list(e.id.values[max(a, b - n):b])


def ts(x):
    return None if x == NAT else pd.Timestamp(int(x))


def subject_label(ws, kind, subject, res):
    if kind == "cell":
        return f"H3 cell {subject} (r{res})"
    return (ws.name(subject) or subject) if kind == "participant" else subject


def occurrence_card(series, decl, source, kind, res, pin, start, end):
    """Learn the frozen model card from the calibration buckets."""
    B, T = series.B, len(series.B)
    t_data = T if (pin and pin.get("end")) else T - 1                     # the last row is the future bucket
    t_cal = int(B.searchsorted(pd.Timestamp(pin["calibrate_before"]))) if pin else int(config.CALIBRATION_SHARE * t_data)
    t_end = int(B.searchsorted(pd.Timestamp(pin["test_before"]))) if pin and pin.get("test_before") else t_data
    card = {"source": source, "kind": kind, "resolution": res, "bucket": series.bucket, "L": series.L,
            "start": str(pd.Timestamp(start).date()), "end": str(pd.Timestamp(end).date()), "t_cal": t_cal,
            "t_end": t_end, "subjects": len(series.subjects), "events": int(series.C_full.sum()),
            "late": series.dropped_late}
    if t_cal >= t_data:
        card["skip"] = f"no bucket after the calibration span (the data ends {B[t_data - 1].date()})"
        return card
    card["calibrate_before"] = str(B[t_cal].date())
    if t_cal < config.MIN_CALIBRATION_BUCKETS:
        card["skip"] = f"only {t_cal} calibration buckets (< {config.MIN_CALIBRATION_BUCKETS})"
        return card
    if series.C_in[:t_cal].sum() == 0:
        card["skip"] = "no event in the calibration buckets"
        return card
    N = m3.neighbours(series.subjects) if kind == "cell" else None
    grid = dict(pin["grid"] if pin else decl["default_grid"])
    if N is None:
        grid["gamma"] = [0.0]
    cal = np.arange(t_cal)
    card["params"], card["ll"] = m3.fit(series.C_in, N, cal, grid, series.L)
    if pin and pin.get("own_grid"):
        card["params_own"], _ = m3.fit(series.C_in, N, cal, pin["own_grid"], series.L)
    lam = m3.expectation(series.C_in, N, card["params"], series.L)
    card["threshold"] = anomalies.occurrence_threshold(series.C_in, lam, cal, decl["budget"])
    card["freeze"] = pd.Timestamp(series.close[t_cal - 1])
    rows = set(range(max(T - FORECAST_LAST, 0), T))
    demo = [pd.Timestamp(d) for d in decl["demo_dates"].get(source, [])]
    card["demo_rows"] = [int(r) for r in B.get_indexer(config.bucket_start(pd.Series(demo, dtype="datetime64[ns]"),
                                                                              series.bucket)) if r >= 0]
    card["forecast_rows"] = sorted(r for r in rows | set(card["demo_rows"]) if r >= t_cal + series.L)
    return card


def occurrence_conclusions(ws, series, card, as_of=None):
    """Sparks, forecasts and (in the full run) scores of one occurrence series, from its frozen card."""
    out = []
    src, kind, res, L = card["source"], card["kind"], card["resolution"], card["L"]
    a, d, g = card["params"]
    N = m3.neighbours(series.subjects) if kind == "cell" else None
    C_in, B = series.C_in, series.B
    mu = m3.background(C_in)
    lam = m3.shift(mu, L) + a * d ** L * m3.shift(m3.excitation(C_in, N, d, g), L) + 1e-9
    params = {"model": "M3", "a": a, "decay": d, "gamma": g, "L": L, "lag_hours": card.get("lag_hours"),
              "alpha": 50, "bucket": card["bucket"], "threshold": round(card["threshold"], 6),
              "budget": card["budget"], "calibrate_before": card["calibrate_before"]}
    limit = None if as_of is None else np.datetime64(pd.Timestamp(as_of), "ns")
    label = lambda s: subject_label(ws, kind, series.subjects[s], res)

    # sparks: issued when their bucket closes
    for t, s, n, l, sv in zip(*anomalies.occurrence_sparks(C_in, lam, card["t_cal"], card["threshold"])):
        if limit is not None and series.close[t] > limit:
            continue
        about, skey, extra = findings.subject_about(kind, series.subjects[s], res)
        out.append(findings.conclusion(
            "spark", src, skey, findings.day(B[t]), series.close[t], about,
            {"observed": n, "expected": l, "s": sv},
            f"{label(s)}: {findings.fmt(n)} events in the {card['bucket']} of {findings.day(B[t])}, "
            f"expected {findings.fmt(l)} (surprise {sv:.1f})",
            series.premises(s, t, t), "engine.occurrence_spark", params, B[t], card["bucket"], extra,
            ts(series.max_in[t])))

    # forecasts: the top 20 subjects of each selected bucket, among the subjects seen before
    rows = [t for t in card["forecast_rows"] if t < len(B) and (limit is None or series.tau[t] <= limit)]
    if rows:
        E_self = m3.shift(m3.excitation(C_in, None, d, 0.0), L)[rows]
        E_nb = (m3.shift(m3.excitation((N @ C_in.T).T, None, d, 0.0), L)[rows]
                if (N is not None and g > 0) else np.zeros_like(E_self))
        mu_r = m3.shift(mu, L)[rows]
        for i, t in enumerate(rows):
            seen = np.nonzero(C_in[:max(t - L, 0)].sum(0) > 0)[0]
            if not len(seen):
                continue
            order = seen[np.argsort(-lam[t, seen], kind="stable")]
            lo, hi = m3.band(lam[t, order[:FORECAST_TOP]])
            for rank, s in enumerate(order[:FORECAST_TOP], 1):
                about, skey, extra = findings.subject_about(kind, series.subjects[s], res)
                val = {"expected": lam[t, s], "routine": mu_r[i, s], "self": a * d ** L * E_self[i, s],
                       "neighbours": a * d ** L * g * E_nb[i, s], "lower80": lo[rank - 1], "upper80": hi[rank - 1],
                       "rank": rank, "of": len(seen)}
                pre = series.premises(s, t - 1 - L)
                if not pre:
                    continue
                out.append(findings.conclusion(
                    "forecast", src, skey, findings.day(B[t]), series.tau[t], about, val,
                    f"{label(s)}: {findings.fmt(val['expected'])} events expected in the {card['bucket']} of "
                    f"{findings.day(B[t])} (routine {findings.fmt(val['routine'])}, own recent activity "
                    f"{findings.fmt(val['self'])}, neighbours {findings.fmt(val['neighbours'])}; 80% band "
                    f"{findings.fmt(val['lower80'])}-{findings.fmt(val['upper80'])}), rank {rank} of {len(seen):,}",
                    pre, "engine.m3_forecast", params, B[t], card["bucket"], extra,
                    ts(series.max_in[t - 1 - L]) if t - 1 - L >= 0 else None))

    if as_of is None:
        out += occurrence_scores(series, card, lam, m3.shift(mu, L) + 1e-9, params)
    return out


def occurrence_scores(series, card, lam, routine, params):
    """Per bucket of the test span and per source: the share of events in the top 1% (M3 and routine), AUC."""
    src, kind, B = card["source"], card["kind"], series.B
    test = np.arange(card["t_cal"], card["t_end"])
    if not len(test):
        return []
    em, er = m3.evaluate_forecast(series.C_full, lam, test), m3.evaluate_forecast(series.C_full, routine, test)
    card["scores"] = {"auc_model": em["auc_raw"], "auc_routine": er["auc_raw"], "top1_model": em["share"],
                      "top1_routine": er["share"], "k": em["top_k_subjects"], "test_buckets": len(test)}
    about, logical = findings.logical_about("engine.score", findings.source_key(src), kind)
    out, last = [], None
    e = series.ev.sort_values(["ti", "id"])
    for t, (got, total) in em["per_bucket"].items():
        known = max(series.close[t].astype("datetime64[ns]").astype(np.int64), series.row_full[t])
        bucket_ids = list(e.id.values[np.searchsorted(e.ti.values, t):np.searchsorted(e.ti.values, t, side="right")])
        val = {"events": total, "captured_model": got, "captured_routine": er["per_bucket"][t][0],
               "k": em["top_k_subjects"]}
        cm = max(series.max_in[t - 1 - card["L"]] if t - 1 - card["L"] >= 0 else NAT, series.row_full[t])
        out.append(findings.conclusion(
            "forecast_score", src, kind, findings.day(B[t]), ts(known), about, val,
            f"{src}: the {em['top_k_subjects']} highest-ranked subjects held {findings.fmt(got)} of "
            f"{findings.fmt(total)} events in the {card['bucket']} of {findings.day(B[t])} (routine's: "
            f"{findings.fmt(val['captured_routine'])})", bucket_ids[:50], "engine.forecast_score", params, B[t],
            card["bucket"], logical, ts(cm)))
        last = (known, bucket_ids, cm) if last is None or known >= last[0] else last
    if last is None:
        return out
    s = card["scores"]
    known = max(r[1]["known_at"] for r in out)
    out.append(findings.conclusion(
        "forecast_score", src, kind, "test", known, about,
        {k: s[k] for k in ("auc_model", "auc_routine", "top1_model", "top1_routine")} | {"k": s["k"]},
        f"{src}: M3 AUC {findings.fmt(s['auc_model'])} and top-1% share {findings.fmt(s['top1_model'])}; routine "
        f"AUC {findings.fmt(s['auc_routine'])} and {findings.fmt(s['top1_routine'])}, over {len(test):,} {card['bucket']}s from "
        f"{findings.day(B[test[0]])} to {findings.day(B[test[-1]])}",
        last[1][:50], "engine.forecast_score", params, B[test[0]], card["bucket"], logical,
        max(r[1]["counted_max"] for r in out if r[1]["counted_max"] is not None)))
    return out


def series_for(ws, card, as_of=None, ev=None):
    if ev is None:
        ev = ws.occurrences(card["source"], card["types"], card["bucket"], card["kind"], card["resolution"], as_of)
    return Series(ev, card["bucket"], card["start"], card["end"], card["L"], card["lag_hours"])


def occurrence_groups(shape):
    groups = {}
    for typ, t in shape["types"].items():
        if t["shape"] == "occurrence":
            groups.setdefault((t["subject"], t.get("resolution")), []).append(typ)
    return groups


def run(ws, decl, shapes, only=None, cards=None, as_of=None):
    """All operators over all sources. With cards (frozen) and as_of: the run on records known at as_of."""
    out, cards_out, notes = [], {}, {}
    for source, shape in shapes.items():
        if only and source != only:
            continue
        pin = decl["pins"].get(source)
        notes[source] = []
        for (kind, res), types in sorted(occurrence_groups(shape).items(), key=lambda x: str(x[0])):
            ck = f"{source}|{kind}"
            if kind is None:
                notes[source].append(f"{', '.join(types)}: no subject (no cell, logical path or participant)")
                continue
            card = (cards or {}).get(ck)
            if cards is not None and card is None:
                continue
            ev = ws.occurrences(source, types, shape["bucket"], kind, res, as_of)
            if as_of is not None and ev.empty:
                continue
            if card is None:
                if ev.empty:
                    notes[source].append(f"{', '.join(types)}: no event fills a {shape['bucket']} bucket")
                    continue
                start = pd.Timestamp(pin["start"]) if pin else ev.bucket.min()
                end = (pd.Timestamp(pin["end"]) if pin and pin.get("end")
                       else ev.bucket.max() + config.offset(shape["bucket"]))
                series = Series(ev, shape["bucket"], start, end, shape["L"], shape["lag_hours"])
                card = occurrence_card(series, decl, source, kind, res, pin, start, end)
                card.update(types=types, lag_hours=shape["lag_hours"], budget=decl["budget"])
            else:
                series = series_for(ws, card, as_of, ev)
            cards_out[ck] = card
            if card.get("skip"):
                notes[source].append(f"{', '.join(types)}: {card['skip']}")
                continue
            got = occurrence_conclusions(ws, series, card, as_of)
            out += got
    return out, cards_out, notes
