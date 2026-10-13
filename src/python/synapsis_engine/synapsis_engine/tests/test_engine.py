"""Quick checks without MongoDB: same maths, own past, and the truncation property on synthetic events."""
import json
import unittest

import numpy as np
import pandas as pd

from synapsis_engine import anomalies, backfill, config, verify


class EngineTest(unittest.TestCase):
    def test_same_maths(self):
        for name, d in verify.same_maths():
            self.assertLessEqual(d, 1e-12, name)

    def test_own_past(self):
        v = np.r_[np.arange(20.0), 100.0]
        change, z = anomalies.own_past(v)
        self.assertTrue(np.isnan(z[:9]).all())
        self.assertTrue(np.isnan(z[10]))           # changes all equal: MAD 0, no z
        v = np.r_[np.cumsum(np.tile([1.0, 2.0, 3.0], 6)), 500.0]
        change, z = anomalies.own_past(v)
        self.assertGreater(z[-1], 4)

    def test_truncated_run_gives_same_conclusions(self):
        rng = np.random.default_rng(3)
        n, start = 4000, pd.Timestamp("2020-01-06")
        t = start + pd.to_timedelta(rng.integers(0, 700, n), "D") + pd.to_timedelta(rng.integers(0, 86400, n), "s")
        ev = pd.DataFrame({"id": [f"evt:test:{i:05d}" for i in range(n)],
                           "known_at": t + pd.to_timedelta(rng.exponential(2.0, n) * 86400, "s"),
                           "bucket": config.bucket_start(pd.Series(t), "week"),
                           "subject": rng.choice(["space.x/a", "space.x/b", "space.x/c", "space.x/d"], n)})
        end = ev.bucket.max() + config.offset("week")
        series = backfill.Series(ev, "week", start, end, 0, 30.0)
        card = backfill.occurrence_card(series, config.load(), "src:test:x", "logical", None, None, start, end)
        card.update(types=[], lag_hours=30.0, budget=0.005)
        full = backfill.occurrence_conclusions(None, series, card)
        canon = lambda r: json.dumps(r, sort_keys=True)
        for row in card["forecast_rows"][::17]:
            tau = series.tau[row]
            cut = backfill.Series(ev[ev.known_at <= tau], "week", start, end, 0, 30.0)
            got = {canon(r) for r, _ in backfill.occurrence_conclusions(None, cut, card, tau)}
            want = {canon(r) for r, m in full if r["type"] != "forecast_score" and m["known_at"] <= tau}
            self.assertEqual(got, want)


if __name__ == "__main__":
    unittest.main()
