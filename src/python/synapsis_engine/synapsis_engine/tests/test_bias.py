"""The source-bias routine on synthetic polls, results and market prices, through the workspace's own parser."""
import datetime as dt
import json
import unittest

import numpy as np
import pandas as pd

from synapsis_engine import bias, config, workspace

EDAY = {2016: dt.datetime(2016, 11, 8), 2020: dt.datetime(2020, 11, 3), 2024: dt.datetime(2024, 11, 5)}
NAMES = {2016: ("Trump", "Clinton"), 2020: ("Trump", "Biden"), 2024: ("Trump", "Harris")}


def docs(result_shift=0):
    rng = np.random.default_rng(5)
    states = [f"US-{c}" for c in ("AL", "AZ", "GA", "MI", "NV", "PA", "WI", "TX", "NY", "CA")]
    lean = dict(zip(states, rng.normal(0, 12, len(states))))
    skew = dict(zip(states, rng.normal(-2, 2, len(states))))
    out = []
    for y, d in EDAY.items():
        for s in states:
            truth = lean[s] + rng.normal(0, 2)
            r = d + dt.timedelta(days=result_shift if y == 2024 else 0)
            out.append({"id": f"evt:test:result/{y}/{s}", "source": "src:test:results", "type": "POL.ELECTION.RESULT",
                        "state": 1, "known_at": r + dt.timedelta(days=1),
                        "record": {"time": {"start": r.strftime("%Y-%m-%dT00:00:00Z"), "precision": "day"},
                                   "place": {"admin": {"country": "US", "subdivision": s}},
                                   "measures": [{"name": "margin", "value": round(truth, 1)}],
                                   "attributes": {"election_year": y}}})
            for k in range(int(rng.integers(0, 6))):
                end = d - dt.timedelta(days=int(rng.integers(1, 40)))
                m = round(truth - skew[s] + rng.normal(0, 3))
                out.append({"id": f"evt:test:poll/{y}/{s}/{k}", "source": "src:test:polls", "type": "POL.POLL.STATE",
                            "state": 1, "known_at": end + dt.timedelta(days=2),
                            "record": {"time": {"start": end.strftime("%Y-%m-%dT00:00:00Z"), "precision": "day"},
                                       "place": {"admin": {"country": "US", "subdivision": s}},
                                       "participants": [{"entity": f"ent:pollster:p{k % 3}", "role": "publisher"}],
                                       "measures": [{"name": "margin", "value": m}, {"name": "share_r", "value": 48 + m / 2},
                                                    {"name": "share_d", "value": 48 - m / 2}],
                                       "attributes": {"election_year": y, "republican": NAMES[y][0], "democrat": NAMES[y][1]}}})
            if y == 2024:
                p = float(1 / (1 + np.exp(-lean[s] / 4)))
                out.append({"id": f"evt:test:market/{s}", "source": "src:test:markets", "type": "MKT.PREDICTION.PRICE",
                            "state": 1, "known_at": d - dt.timedelta(days=3),
                            "record": {"time": {"start": (d - dt.timedelta(days=3)).strftime("%Y-%m-%dT10:00:00Z"),
                                                "precision": "minute"},
                                       "place": {"admin": {"country": "US", "subdivision": s}},
                                       "measures": [{"name": "probability", "value": round(1 - p, 4)}],
                                       "attributes": {"outcome": "Kamala Harris"}}})
    return out


class BiasTest(unittest.TestCase):
    def test_routine_bias(self):
        ws = workspace.Workspace(":memory:")
        ws.load([docs()])
        decl = config.load()
        out, info = bias.conclusions(ws, decl)
        self.assertEqual(sorted(info), [2020, 2024])
        for y, v in info.items():
            self.assertLess(v["mae"], v["mae_polls"])
        self.assertIn("brier_market", info[2024])
        est = [r for r, m in out if r["type"] == "calibrated_estimate"]
        # 2024's probability uses 2020's spread; 2020 has no earlier forecast, so no probability
        p24 = [r["statement"]["value"].get("probability") for r in est if r["attributes"]["period_start"][:4] == "2024"]
        self.assertTrue(p24 and all(p is not None and 0 <= p <= 1 for p in p24))
        self.assertTrue(all("probability" not in r["statement"]["value"] for r in est
                            if r["attributes"]["period_start"][:4] == "2020"))
        self.assertIn("brier_polls", info[2024])
        self.assertNotIn("brier", info[2020])
        tau = max(m["known_at"] for r, m in out if r["type"] == "calibrated_estimate")
        cut, _ = bias.conclusions(ws, decl, tau)
        self.assertEqual(sorted(json.dumps(r, sort_keys=True) for r, _ in cut), sorted(json.dumps(r, sort_keys=True) for r in est))

    def test_issued_at_the_end_of_the_poll_window(self):
        for y, d in EDAY.items():
            self.assertEqual(bias.election_day(y), pd.Timestamp(d))
        ws = workspace.Workspace(":memory:")
        ws.load([docs()])
        out, _ = bias.conclusions(ws, config.load())
        est = {r["id"]: r for r, m in out if r["type"] == "calibrated_estimate"}
        for r, m in out:
            if r["type"] == "calibrated_estimate":
                self.assertEqual(r["known_at"], EDAY[int(r["attributes"]["period_start"][:4])].strftime("%Y-%m-%dT00:00:00Z"))
                self.assertLessEqual(m["counted_max"], m["known_at"])
        # this cycle's results (here dated two weeks late) change nothing in its estimates
        ws2 = workspace.Workspace(":memory:")
        ws2.load([docs(result_shift=14)])
        out2, _ = bias.conclusions(ws2, config.load())
        self.assertEqual({r["id"]: r for r, m in out2 if r["type"] == "calibrated_estimate"}, est)


if __name__ == "__main__":
    unittest.main()
