"""Framework tests that need no network: record builders, batching, checkpoints and the runner's ordering."""
from __future__ import annotations

import json
from datetime import date, datetime, timezone
from pathlib import Path

from synapsis_feeders import sil
from synapsis_feeders.base import Checkpoint
from synapsis_feeders.batch import BatchSender
from synapsis_feeders.state import FeederState


class FakeApi:
    def __init__(self):
        self.bundles = []

    def ingest(self, bundle):
        self.bundles.append(bundle)
        n = len(bundle["records"])
        return {"status": "accepted", "counts": {"received": n, "new": n, "versioned": 0, "unchanged": 0,
                                                 "duplicates": 0, "rejected": 0}, "rejected": []}


def test_time_spec_floors_to_precision():
    t = datetime(2024, 5, 17, 13, 42, 7, tzinfo=timezone.utc)
    assert sil.time_spec(t, "day") == {"start": "2024-05-17T00:00:00Z", "precision": "day"}
    assert sil.time_spec(t, "quarter")["start"] == "2024-04-01T00:00:00Z"
    assert sil.time_spec(t, "week")["start"] == "2024-05-13T00:00:00Z"


def test_measures_skip_missing_values():
    assert sil.measures(("a", None, "count"), ("b", float("nan"), "count"), ("c", "3", "count")) == \
        [{"name": "c", "value": 3, "unit": "count"}]


def test_slug_and_key_are_stable():
    assert sil.slug("Progress Software|MOVEit Transfer") == "progress-software-moveit-transfer"
    assert sil.key("a b/c") == "a-b/c"


def test_batches_are_split_and_ids_come_from_content(tmp_path: Path):
    api = FakeApi()
    sender = BatchSender(api, "t", "feeder:t", "t", 1, 2, FeederState(tmp_path, "t"))
    for i in range(5):
        sender.add({"i": i})
    sender.flush()
    assert [len(b["records"]) for b in api.bundles] == [2, 2, 1]
    again = FakeApi()
    sender2 = BatchSender(again, "t", "feeder:t", "t", 1, 2, FeederState(tmp_path, "t"))
    for i in range(5):
        sender2.add({"i": i})
    sender2.flush()
    assert [b["batch_id"] for b in api.bundles] == [b["batch_id"] for b in again.bundles]


def test_cursor_is_written_atomically(tmp_path: Path):
    st = FeederState(tmp_path, "t")
    st.save_cursor({"last_day": "2024-01-01"})
    assert json.loads((tmp_path / "t" / "cursor.json").read_text()) == {"last_day": "2024-01-01"}
    assert FeederState(tmp_path, "t").cursor == {"last_day": "2024-01-01"}


def test_run_every_retries_soon_after_a_failure_and_keeps_since_until_a_success(monkeypatch):
    from synapsis_feeders import runner

    calls, waits = [], []
    outcomes = iter([RuntimeError("API still starting"), None, None])

    def fake_run_once(name, settings, **kw):
        calls.append(dict(kw))
        result = next(outcomes)
        if isinstance(result, Exception):
            raise result
        return {}

    class FakeStop:
        def __init__(self):
            self.stopped = False

        def is_set(self):
            return self.stopped

        def wait(self, seconds=None):
            waits.append(seconds)
            if len(waits) == 3:
                self.stopped = True

        def set(self):
            self.stopped = True

    monkeypatch.setattr(runner, "run_once", fake_run_once)
    monkeypatch.setattr(runner, "_STOP", FakeStop())
    runner.run_every("t", "6h", settings=None, since=date(2023, 1, 1), reset=True)
    assert waits == [60.0, 6 * 3600, 6 * 3600]
    assert [c["since"] for c in calls] == [date(2023, 1, 1), date(2023, 1, 1), None]
    assert [c["reset"] for c in calls] == [True, False, False]


def test_wait_for_records_raises_when_another_feeders_records_never_arrive(tmp_path: Path):
    import logging

    import pytest

    from synapsis_feeders.base import Context

    class Api:
        def record_exists(self, record_id):
            return record_id == "evt:kev:CVE-2021-44228"

    ctx = Context(name="t", settings=None, http=None, package={}, cursor={}, since=None, until=None,
                  started_at=datetime.now(timezone.utc), log=logging.getLogger("t"), memo_dir=tmp_path / "memo", api=Api())
    ctx.wait_for_records(["evt:kev:CVE-2021-44228"], minutes=0, what="the KEV catalog")      # present: returns
    with pytest.raises(RuntimeError, match="the KEV catalog is not in Synapsis"):
        ctx.wait_for_records(["evt:kev:CVE-2099-0001"], minutes=0, what="the KEV catalog")


def test_memos_are_cleared_by_reset_but_observations_are_kept(tmp_path: Path):
    import logging

    from synapsis_feeders.base import Context

    state = FeederState(tmp_path, "t")
    ctx = Context(name="t", settings=None, http=None, package={}, cursor={}, since=None, until=None,
                  started_at=datetime.now(timezone.utc), log=logging.getLogger("t"), memo_dir=state.memo_dir)
    ctx.save_memo("sent", ["a", "b"])
    ctx.save_memo("first_seen", {"x": "2026-01-01"}, survives_reset=True)
    assert ctx.load_memo("sent", []) == ["a", "b"]
    state.reset()
    assert ctx.load_memo("sent", []) == []
    assert ctx.load_memo("first_seen", {}, survives_reset=True) == {"x": "2026-01-01"}
