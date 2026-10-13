"""Remaining previously-untested designs: S2 anchor-aligned meaning cells, S3 streaming discovery control (LORD++),
S4 template-first grounded answers (local SLM), S5 prediction markets (Polymarket) vs polls vs routine correction."""
import os, json, time, traceback, re
os.environ["SECTIONS"] = "none"
import numpy as np, pandas as pd
OUT = json.load(open("results/remaining_tests.json")) if os.path.exists("results/remaining_tests.json") else {}
def save(): json.dump(OUT, open("results/remaining_tests.json", "w"), indent=1, default=str)
def section(name, f):
    if name in OUT and "error" not in OUT[name]: return
    t = time.time()
    try: OUT[name] = f(); OUT[name]["secs"] = round(time.time() - t)
    except Exception: OUT[name] = {"error": traceback.format_exc()[-900:]}
    save(); print(name, json.dumps(OUT[name], default=str)[:1500], flush=True)
def s2():
    import synapsis_r3.case_f7_semantic as f7
    return f7.run_b("anchor_aligned")
def s3():
    from synapsis_r3.engines_workbench import load_p1
    from synapsis_r3.core import fit_m3, m3_lambda
    from scipy.stats import poisson
    Y, drivers, pub, cut, H, kev, K = load_p1(); S, T = Y.shape; lam = m3_lambda(Y, fit_m3(Y, cut))
    spark = (-np.log10(np.clip(poisson.sf(Y.values - 1, np.clip(lam, 1e-6, None)), 1e-300, 1)) >= 3) & (Y.values >= 10)
    def prec(dr, sp):
        ev = np.where(dr > 0)[0]; ts = np.where(sp)[0]
        return np.mean([((t - ev >= 1) & (t - ev <= 10)).any() for t in ts]) if len(ev) and len(ts) else np.nan
    rng = np.random.default_rng(3); stream = []
    cps = [int(np.searchsorted(Y.columns, d)) for d in pd.date_range("2023-03-31", "2026-09-20", freq="QE")]
    for c in cps:
        for g in range(S):
            sp = spark[g, :c]
            if sp.sum() < 2: continue
            for k in K.index:
                dr = K.loc[k].values[:c]
                if (dr > 0).sum() < 3: continue
                pr = prec(dr, sp); null = [prec(np.roll(dr, int(rng.integers(12, c - 12))), sp) for _ in range(49)]
                stream.append((str(Y.columns[c - 1].date()), k, Y.index[g], round(float(pr), 3), (1 + sum(n >= pr for n in null)) / 50))
    alpha, W0 = 0.10, 0.05; j = np.arange(1, len(stream) + 2); gam = np.log(np.maximum(j, 2)) / (j * np.exp(np.sqrt(np.log(np.maximum(j, 2)))))
    gam = gam / gam.sum(); rej = []
    for t, (cp, k, g, pr, p) in enumerate(stream, 1):
        a = gam[t - 1] * W0 + sum(((alpha - W0) if i == 0 else alpha) * gam[t - tau - 1] for i, tau in enumerate(rej) if t > tau)
        if p <= a: rej.append(t)
    naive = sum(p <= 0.05 for *_, p in stream)
    mc = [(cp, pr, p) for cp, k, g, pr, p in stream if k == "MFT" and g == "clop"]
    return {"tests_in_stream": len(stream), "checkpoints": len(cps), "lord_rejections": len(rej), "lord_rejected": [stream[t - 1][:3] for t in rej][:10],
            "naive_p05_rejections": int(naive), "naive_expected_false_at_null": round(0.05 * len(stream), 1), "MFT_clop_over_time": mc}
def s4():
    from llama_cpp import Llama, LlamaGrammar
    llm = Llama(model_path="models/qwen15/qwen2.5-1.5b-instruct-q4_k_m.gguf", n_ctx=1024, n_threads=1, verbose=False)
    g = LlamaGrammar.from_string(r'root ::= [A-Za-z ,;:()\'-]{30,260} "."', verbose=False)
    cases = {"health_omicron": ("US hospital admissions, 2 weeks from 27 Dec 2021", [("forecast (single source)", 167654), ("forecast (fusion)", 221641), ("observed", 291901)]),
             "election_2024": ("2024 state presidential margins", [("average error of polls alone (points)", 3.31), ("average error after routine correction (points)", 2.24), ("winners called by polls alone", "34 of 37"), ("winners called after correction", "37 of 37")]),
             "bank_svb": ("Silicon Valley Bank, year-end 2022 report", [("uninsured share of deposits", "86%"), ("unrealized securities losses vs equity", "114%"), ("joint-anomaly rank among 4,772 banks", 5)])}
    out = {}
    for name, (subj, facts) in cases.items():
        body = " ".join(f"The {k} was {v:,}." if isinstance(v, (int, float)) else f"The {k} was {v}." for k, v in facts)
        r = llm.create_chat_completion(messages=[{"role": "user", "content": f"Facts about {subj}: {body}\nWrite ONE short sentence for a non-expert saying what these facts mean. Do not repeat numbers."}],
                                       grammar=g, max_tokens=80, temperature=0)
        q = r["choices"][0]["message"]["content"].strip(); out[name] = {"answer": f"{subj}: {body} {q}", "model_sentence_has_digits": bool(re.search(r"\d", q))}
    return out
def s5():
    import urllib.request, urllib.parse
    from scipy.stats import norm
    UA = {"User-Agent": "Mozilla/5.0 synapsis-r3"}
    get = lambda u: json.load(urllib.request.urlopen(urllib.request.Request(u, headers=UA), timeout=60))
    ns = {}; exec(compile(open("synapsis_r3/case_f6_elections.py").read().replace('json.dump(R, open("results/f6_elections.json", "w"), indent=1, default=str)', ''), "f6", "exec"), ns)
    D, Rs, prev = ns["D"], ns["Rs"], ns["prev"]
    H16 = D[D.year == 2016]; nat16 = H16.err.mean(); sb = H16.groupby("state").err.agg(["sum", "size"])
    T20 = D[D.year == 2020].copy(); b20 = ((sb["sum"] + 2 * nat16) / (sb["size"] + 2)).reindex(T20.state).fillna(nat16).values
    T20["prior"] = T20.state.map(lambda s: prev.get((2016, s), np.nan)) + (T20.poll.mean() - pd.Series([prev.get((2016, s), np.nan) for s in T20.state]).mean())
    w20 = T20.n / (T20.n + 3); f20 = w20 * (T20.poll + b20) + (1 - w20) * T20.prior.fillna(T20.poll + b20)
    sd_poll, sd_rout = float(np.std(T20.margin - T20.poll)), float(np.std(T20.margin - f20))      # spreads learned from 2020 only
    T24 = ns["T"].copy(); f24 = ns["f"]; T24["p_polls"] = norm.cdf(np.asarray(f24["polls_only"]) / sd_poll); T24["p_routine"] = norm.cdf(np.asarray(f24["routine_bias+prior_blend"]) / sd_rout)
    mk = {}; t_end = int(pd.Timestamp("2024-11-05T10:00Z").timestamp())
    for st in T24.state:
        slug = st.lower().replace(" ", "-") + "-presidential-election-winner"
        try: ev = get("https://gamma-api.polymarket.com/events?" + urllib.parse.urlencode({"slug": slug}))
        except Exception: continue
        if not ev: continue
        for m in ev[0].get("markets", []):
            if re.search(r"republican|trump", (m.get("groupItemTitle") or "") + " " + (m.get("question") or ""), re.I):
                tok = json.loads(m["clobTokenIds"])[0]
                h = get("https://clob.polymarket.com/prices-history?" + urllib.parse.urlencode({"market": tok, "startTs": t_end - 4 * 86400, "endTs": t_end, "fidelity": 60})).get("history", [])
                if h: mk[st] = float(h[-1]["p"])
                break
        time.sleep(0.2)
    T24["p_market"] = T24.state.map(mk); C = T24.dropna(subset=["p_market"]); y = (C.margin > 0).astype(float)
    brier = lambda p: round(float(np.mean((p - y) ** 2)), 4)
    sw = C[np.abs(C.margin) < 8]; ys = (sw.margin > 0).astype(float)
    return {"states_with_market": len(C), "sd_polls_2020": round(sd_poll, 2), "sd_routine_2020": round(sd_rout, 2),
            "brier": {"market": brier(C.p_market), "polls_only": brier(C.p_polls), "routine_corrected": brier(C.p_routine),
                      "routine_x_market_avg": brier((C.p_routine + C.p_market) / 2)},
            "brier_swing_states": {k: round(float(np.mean((sw[c] - ys) ** 2)), 4) for k, c in (("market", "p_market"), ("polls_only", "p_polls"), ("routine_corrected", "p_routine"))},
            "swing_states": {r.state: {"market": round(r.p_market, 3), "polls": round(r.p_polls, 3), "routine": round(r.p_routine, 3), "result_margin": round(r.margin, 2)} for _, r in sw.iterrows()}}
RUN = os.environ.get("RT", "S2,S3,S4,S5").split(",")
for name, fn in (("S2_anchor_aligned_meaning_cells", s2), ("S3_streaming_discovery_LORDpp", s3), ("S5_prediction_markets_2024", s5), ("S4_template_first_answers", s4)):
    if name[:2] in RUN: section(name, fn)
print("REMAINING_DONE")
