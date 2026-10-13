"""The source-bias routine on synthetic polls, results and market prices, through the workspace's own parser."""
import datetime as dt
import unittest

import numpy as np

from synapsis_engine import bias, config, workspace

EDAY = {2016: dt.datetime(2016, 11, 8), 2020: dt.datetime(2020, 11, 3), 2024: dt.datetime(2024, 11, 5)}
NAMES = {2016: ("Trump", "Clinton"), 2020: ("Trump", "Biden"), 2024: ("Trump", "Harris")}


def docs():
    rng = np.random.default_rng(5)
    states = [f"US-{c}" for c in ("AL", "AZ", "GA", "MI", "NV", "PA", "WI", "TX", "NY", "CA")]
    lean = dict(zip(states, rng.normal(0, 12, len(states))))
    skew = dict(zip(states, rng.normal(-2, 2, len(states))))
    out = []
    for y, d in EDAY.items():
        for s in states:
            truth = lean[s] + rng.normal(0, 2)
            out.append({"id": f"evt:test:result/{y}/{s}", "source": "src:test:results", "type": "POL.ELECTION.RESULT",
                        "state": 1, "known_at": d + dt.timedelta(days=1),
                        "record": {"time": {"start": d.strftime("%Y-%m-%dT00:00:00Z"), "precision": "day"},
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
        self.assertTrue(all(0 <= r["statement"]["value"]["probability"] <= 1 for r in est))
        tau = max(m["known_at"] for r, m in out if r["type"] == "calibrated_estimate")
        cut, _ = bias.conclusions(ws, decl, tau)
        self.assertEqual(sorted(r["id"] for r, _ in cut), sorted(r["id"] for r in est))


if __name__ == "__main__":
    unittest.main()
