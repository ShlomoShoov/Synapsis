"""Source-bias routine (elections): reference/case_f6_elections.py, the 'routine_bias+prior_blend' forecast.

Each state's polling bias is learned only from previous elections (shrunk to the national bias, kappa 2) and
corrects the mean of the polls of the last 30 days; the corrected margin is blended with the previous result
(shifted by this year's national swing) with weight n / (n + 3). The probability that the Republican wins is
Phi(corrected / sigma), sigma = the root mean square of the history's errors left after the learned bias (the
reference does not give its probability model). The election day is read from the result records, so an estimate is
dated no earlier than the first result of its year (the last polls of the window are published about as late).
"""
import json

import numpy as np
import pandas as pd
from scipy.stats import norm

from . import findings

KAPPA = 2.0
BLEND = 3.0


def _attr(a, k):
    return (json.loads(a) if isinstance(a, str) else {}).get(k)


def frames(ws, spec, as_of=None):
    """Polls of the last `days` days before each election day, the results, the election days."""
    m = spec["measure"]
    res = ws.typed_wide(spec["result"], [m], as_of)
    res = res[res.subdivision.notna() & res[m].notna()].copy()
    if res.empty:
        return None
    res["year"] = [_attr(a, "election_year") or t.year for a, t in zip(res["attrs"], res.t_start)]
    res["eday"] = res.t_start.dt.normalize()
    eday = res.groupby("year").eday.min()
    res = res.sort_values(["year", "subdivision", "id"]).drop_duplicates(["year", "subdivision"])
    polls = ws.typed_wide(spec["poll"], [m, "share_r", "share_d"], as_of)
    polls["year"] = [_attr(a, "election_year") for a in polls["attrs"]]
    polls = polls[polls.year.isin(eday.index) & polls.subdivision.notna() & polls[m].notna()].copy()
    polls["end"] = polls.t_start.dt.normalize()
    polls["eday"] = polls.year.map(eday)
    polls = polls[(polls.end < polls.eday) & (polls.end >= polls.eday - pd.Timedelta(days=spec["days"]))
                  & polls.share_r.between(1, 99) & polls.share_d.between(1, 99)]
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
    """Per election year with a history: the corrected margins of every polled state, and the years used."""
    avg = polls.groupby(["year", "state"]).agg(poll=("margin", "mean"), n=("margin", "size")).reset_index()
    D = avg.merge(res[["year", "state", "margin"]], on=["year", "state"])
    D["err"] = D.margin - D.poll
    prev = res.set_index(["year", "state"]).margin
    out = {}
    for y in sorted(avg.year.unique()):
        hist = sorted(h for h in D.year.unique() if h < y)
        if not hist:
            continue
        H = D[D.year.isin(hist)]
        nat = H.groupby("year").err.mean().mean()
        sb = H.groupby("state").err.agg(["sum", "size"])
        shrunk = (sb["sum"] + KAPPA * nat) / (sb["size"] + KAPPA)
        sigma = float(np.sqrt(np.mean((H.err - H.state.map(shrunk).values) ** 2)))
        T = avg[avg.year == y].sort_values("state").reset_index(drop=True)
        T["bias"] = shrunk.reindex(T.state).fillna(nat).values
        last = pd.Series([prev.get((y - 4, s), np.nan) for s in T.state])
        T["prior"] = last.values + (T.poll.mean() - last.mean())
        w = T.n / (T.n + BLEND)
        T["corrected"] = w * (T.poll + T.bias) + (1 - w) * T.prior.fillna(T.poll + T.bias)
        T["probability"] = norm.cdf(T.corrected / sigma) if sigma > 0 else np.nan
        out[y] = (T, hist, float(nat), sigma)
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
    for y, (T, hist, nat, sigma) in est.items():
        used = polls[polls.year == y]
        past = res[res.year.isin(hist)]
        hist_polls = polls[polls.year.isin(hist)]
        # the election day itself comes from the result records: the estimate is dated no earlier than the first of them
        known = max(used.known_at.max(), past.known_at.max(), hist_polls.known_at.max(), res[res.year == y].known_at.min())
        params = {"model": "routine_bias+prior_blend", "kappa": KAPPA, "blend": BLEND, "days": spec["days"],
                  "history": [int(h) for h in hist], "national_bias": round(nat, 4), "sigma": round(sigma, 4)}
        if limit is None or known <= limit:
            for r in T.itertuples():
                ids = sorted(used.id[used.state == r.state])
                out.append(findings.conclusion(
                    "calibrated_estimate", src, findings.key(r.state), findings.day(eday[y]), known,
                    [f"ent:subdivision:{r.state}"],
                    {"poll": r.poll, "bias": r.bias, "corrected": r.corrected, "probability": r.probability, "polls": r.n},
                    f"{r.state} {y}: polls R-D {r.poll:+.1f} ({r.n} in the last {spec['days']} days), learned bias "
                    f"{r.bias:+.1f}, corrected {r.corrected:+.1f}; P(Republican wins) {r.probability:.2f}",
                    ids, "engine.source_bias", params, eday[y], "day", None, known))
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
               "winners_polls": int(np.sum(np.sign(S.poll.values) == np.sign(truth))),
               "brier": np.mean((S.probability.values - won) ** 2)}
        if mk:
            idx = [i for i, _ in mk]
            val.update(brier_market=np.mean([(p - won[i]) ** 2 for i, (p, _, _) in mk]),
                       brier_same_states=np.mean((S.probability.values[idx] - won[idx]) ** 2), markets=len(mk))
        info[y] = val
        rk = res[res.year == y]
        sk = max([known, rk.known_at.max()] + [k for _, (_, _, k) in mk])
        about, logical = findings.logical_about("engine.score", findings.source_key(src), f"election-{y}")
        out.append(findings.conclusion(
            "forecast_score", src, f"election-{y}", findings.day(eday[y]), sk, about, val,
            f"Elections {y}: corrected polls missed the margin by {val['mae']:.2f} points on average (polls alone "
            f"{val['mae_polls']:.2f}); winners {val['winners']} of {val['of']}; Brier {val['brier']:.4f}"
            + (f" (Polymarket {val['brier_market']:.4f} on {len(mk)} states)" if mk else ""),
            sorted(rk.id)[:50], "engine.source_bias_score", params, eday[y], "day", logical, sk))
    return out, info
