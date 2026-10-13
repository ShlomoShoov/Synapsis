"""Source-bias routine (elections): reference/case_f6_elections.py, the 'routine_bias+prior_blend' forecast.

Each state's polling bias is learned only from previous elections (shrunk to the national bias, kappa 2) and
corrects the mean of the polls of the last 30 days; the corrected margin is blended with the previous result
(shifted by this year's national swing) with weight n / (n + 3). The probability that the Republican wins is
Phi(corrected / sigma), sigma = the spread (np.std, ddof 0) of the previous cycle's errors (result - corrected
forecast), learned from that cycle only (round 3, reference/remaining_tests_r3.py test S5): 2024 uses 2020's errors,
2020 has no earlier forecast and so no probability. The election day comes from a fixed calendar (the Tuesday after
the first Monday of November), never from the results: an estimate is issued at the end of its 30-day poll window,
election day 00:00Z, from the polls known by then.
"""
import json

import numpy as np
import pandas as pd
from scipy.stats import norm

from . import findings

KAPPA = 2.0
BLEND = 3.0


def election_day(year):
    """US presidential election day: the Tuesday after the first Monday of November."""
    first = pd.Timestamp(int(year), 11, 1)
    return first + pd.Timedelta(days=(7 - first.weekday()) % 7 + 1)


def _attr(a, k):
    return (json.loads(a) if isinstance(a, str) else {}).get(k)


def frames(ws, spec, as_of=None):
    """Polls of the last `days` days before each election day (and known by then), the results, the election days."""
    m = spec["measure"]
    res = ws.typed_wide(spec["result"], [m], as_of)
    res = res[res.subdivision.notna() & res[m].notna()].copy()
    if res.empty:
        return None
    res["year"] = [_attr(a, "election_year") or t.year for a, t in zip(res["attrs"], res.t_start)]
    res = res.sort_values(["year", "subdivision", "id"]).drop_duplicates(["year", "subdivision"])
    polls = ws.typed_wide(spec["poll"], [m, "share_r", "share_d"], as_of)
    polls["year"] = [_attr(a, "election_year") for a in polls["attrs"]]
    polls = polls[polls.year.notna() & polls.subdivision.notna() & polls[m].notna()].copy()
    polls["year"] = polls.year.astype(int)
    eday = pd.Series({y: election_day(y) for y in sorted(polls.year.unique())}, dtype="datetime64[ns]")
    polls["end"] = polls.t_start.dt.normalize()
    polls["eday"] = polls.year.map(eday)
    polls = polls[(polls.end < polls.eday) & (polls.end >= polls.eday - pd.Timedelta(days=spec["days"]))
                  & (polls.known_at <= polls.eday) & polls.share_r.between(1, 99) & polls.share_d.between(1, 99)]
    polls = polls.drop_duplicates(["year", "subdivision", "p0", "end"])
    return polls.rename(columns={m: "margin", "subdivision": "state"}), \
        res.rename(columns={m: "margin", "subdivision": "state"}), eday


def market_prices(ws, spec, polls, eday, as_of=None):
    """P(Republican wins) per (year, state): the last market price before election day."""
    mk = ws.typed_wide(spec["market"], ["probability"], as_of)
    mk = mk[mk.subdivision.notna() & mk.probability.notna()]
    out = {}
    for y, d in eday.items():
        p = polls[polls.year == y]
        names = {k: {_attr(a, k) for a in p["attrs"]} - {None} for k in ("republican", "democrat")}
        w = mk[(mk.t_start < d) & (mk.t_start >= d - pd.Timedelta(days=365))].sort_values(["t_start", "id"])
        for state, g in w.groupby("subdivision"):
            last = g.groupby(g["attrs"].map(lambda a: _attr(a, "outcome") or "")).tail(1)
            rep = [r for r in last.itertuples() if any(n in (_attr(r.attrs, "outcome") or "") for n in names["republican"])]
            dem = [r for r in last.itertuples() if any(n in (_attr(r.attrs, "outcome") or "") for n in names["democrat"])]
            if rep:
                out[(y, state)] = (rep[0].probability, rep[0].id, rep[0].known_at)
            elif dem:
                out[(y, state)] = (1 - dem[0].probability, dem[0].id, dem[0].known_at)
    return out


def estimates(polls, res):
    """Per election year with a history: the corrected margins of every polled state, the years used, the national
    bias and the spreads (corrected, polls alone) learned from the previous cycle (None when it has no forecast)."""
    avg = polls.groupby(["year", "state"]).agg(poll=("margin", "mean"), n=("margin", "size")).reset_index()
    D = avg.merge(res[["year", "state", "margin"]], on=["year", "state"])
    D["err"] = D.margin - D.poll
    prev = res.set_index(["year", "state"]).margin
    out, spread = {}, None
    for y in sorted(avg.year.unique()):
        hist = sorted(h for h in D.year.unique() if h < y)
        if not hist:
            continue
        H = D[D.year.isin(hist)]
        nat = H.groupby("year").err.mean().mean()
        sb = H.groupby("state").err.agg(["sum", "size"])
        shrunk = (sb["sum"] + KAPPA * nat) / (sb["size"] + KAPPA)
        T = avg[avg.year == y].sort_values("state").reset_index(drop=True)
        T["bias"] = shrunk.reindex(T.state).fillna(nat).values
        last = pd.Series([prev.get((y - 4, s), np.nan) for s in T.state])
        T["prior"] = last.values + (T.poll.mean() - last.mean())
        w = T.n / (T.n + BLEND)
        T["corrected"] = w * (T.poll + T.bias) + (1 - w) * T.prior.fillna(T.poll + T.bias)
        sigma, sigma_polls = spread or (None, None)
        T["probability"] = norm.cdf(T.corrected / sigma) if sigma else None
        T["probability_polls"] = norm.cdf(T.poll / sigma_polls) if sigma_polls else None
        out[y] = (T, hist, float(nat), sigma, sigma_polls)
        # this cycle's errors, once its results are known, give the next cycle's spreads
        R = T.merge(D[D.year == y][["state", "margin"]], on="state")
        spread = (float(np.std(R.margin - R.corrected)), float(np.std(R.margin - R.poll))) if len(R) > 1 else None
    return out


def conclusions(ws, decl, as_of=None):
    spec = decl["bias_pair"]
    f = frames(ws, spec, as_of)
    if f is None:
        return [], {"skip": f"no {spec['result']} records"}
    polls, res, eday = f
    est = estimates(polls, res)
    market = market_prices(ws, spec, polls, eday, as_of)
    src = polls.source.iloc[0] if len(polls) else spec["poll"]
    info, out = {}, []
    limit = None if as_of is None else pd.Timestamp(as_of)
    for y, (T, hist, nat, sigma, sigma_polls) in est.items():
        used = polls[polls.year == y]
        past = res[res.year.isin(hist)]
        hist_polls = polls[polls.year.isin(hist)]
        # issued at the end of the poll window (election day, from the calendar); never from this cycle's results
        known = eday[y]
        counted = max(used.known_at.max(), past.known_at.max(), hist_polls.known_at.max())
        params = {"model": "routine_bias+prior_blend", "kappa": KAPPA, "blend": BLEND, "days": spec["days"],
                  "history": [int(h) for h in hist], "national_bias": round(nat, 4),
                  "sigma": None if sigma is None else round(sigma, 4)}
        if limit is None or known <= limit:
            for r in T.itertuples():
                ids = sorted(used.id[used.state == r.state])
                out.append(findings.conclusion(
                    "calibrated_estimate", src, findings.key(r.state), findings.day(eday[y]), known,
                    [f"ent:subdivision:{r.state}"],
                    {"poll": r.poll, "bias": r.bias, "corrected": r.corrected, "probability": r.probability, "polls": r.n},
                    f"{r.state} {y}: polls R-D {r.poll:+.1f} ({r.n} in the last {spec['days']} days), learned bias "
                    f"{r.bias:+.1f}, corrected {r.corrected:+.1f}"
                    + (f"; P(Republican wins) {r.probability:.2f}" if sigma else ""),
                    ids, "engine.source_bias", params, eday[y], "day", None, counted))
        R = res[res.year == y].set_index("state").margin
        S = T[T.state.isin(R.index)]
        if as_of is not None or S.empty:
            continue
        truth = R.reindex(S.state).values
        err, err_polls = truth - S.corrected.values, truth - S.poll.values
        won = (truth > 0).astype(float)
        mk = [(i, market[(y, s)]) for i, s in enumerate(S.state) if (y, s) in market]
        val = {"mae": np.mean(np.abs(err)), "winners": int(np.sum(np.sign(S.corrected.values) == np.sign(truth))),
               "of": len(S), "mae_polls": np.mean(np.abs(err_polls)),
               "winners_polls": int(np.sum(np.sign(S.poll.values) == np.sign(truth)))}
        if sigma:
            val["brier_all"] = np.mean((S.probability.values.astype(float) - won) ** 2)
        if mk and sigma:
            # as round 3: every Brier on the states that have a market
            idx = [i for i, _ in mk]
            pm = np.array([p for _, (p, _, _) in mk])
            pr, pp, w = S.probability.values[idx].astype(float), S.probability_polls.values[idx].astype(float), won[idx]
            val.update(brier=np.mean((pr - w) ** 2), brier_market=np.mean((pm - w) ** 2),
                       brier_polls=np.mean((pp - w) ** 2), brier_avg=np.mean(((pr + pm) / 2 - w) ** 2), markets=len(mk))
        info[y] = val
        rk = res[res.year == y]
        sk = max([known, rk.known_at.max()] + [k for _, (_, _, k) in mk])
        about, logical = findings.logical_about("engine.score", findings.source_key(src), f"election-{y}")
        out.append(findings.conclusion(
            "forecast_score", src, f"election-{y}", findings.day(eday[y]), sk, about, val,
            f"Elections {y}: corrected polls missed the margin by {val['mae']:.2f} points on average (polls alone "
            f"{val['mae_polls']:.2f}); winners {val['winners']} of {val['of']}"
            + (f"; Brier {val['brier']:.4f} (Polymarket {val['brier_market']:.4f}, polls alone {val['brier_polls']:.4f}"
               f" on {len(mk)} states)" if "brier" in val else ""),
            sorted(rk.id)[:50], "engine.source_bias_score", params, eday[y], "day", logical, sk))
    return out, info
