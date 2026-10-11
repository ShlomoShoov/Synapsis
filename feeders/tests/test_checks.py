"""The measure check before sending (checks.py). The refusal cases are the number cases of the old API conformance
suite (05-measures), checked here against the real package files."""
from __future__ import annotations

import synapsis_feeders.sources  # noqa: F401  (importing the sources fills FEEDERS)
from synapsis_feeders.base import FEEDERS
from synapsis_feeders.checks import check_measures, declared_measures
from synapsis_feeders.config import Settings


def _measures_of(feeder: str) -> dict:
    """The measures the given feeder may send, read from feeders/packages/."""
    return declared_measures(Settings.from_env(), FEEDERS[feeder])


def _record(*measures) -> dict:
    """An event with the given (name, value) measures; only the measures matter to the check."""
    return {"kind": "event", "id": "evt:test:1", "measures": [{"name": n, "value": v} for n, v in measures]}


def test_valid_values_pass():
    assert check_measures(_record(("magnitude", 7.5), ("depth_km", 10)), _measures_of("usgs")) is None


def test_a_negative_value_for_an_unsigned_measure_is_refused():
    problem = check_measures(_record(("assets", -5), ("deposits", 1000000)), _measures_of("fdic"))
    assert problem["code"] == "E-MEASURE-NEGATIVE"


def test_a_negative_daily_count_is_refused():
    problem = check_measures(_record(("cases", -12)), _measures_of("jhu-covid"))
    assert problem["code"] == "E-MEASURE-NEGATIVE"


def test_a_value_above_the_maximum_is_refused():
    problem = check_measures(_record(("magnitude", 11), ("depth_km", 10)), _measures_of("usgs"))
    assert problem["code"] == "E-MEASURE-RANGE"


def test_a_value_below_a_signed_measures_minimum_is_refused():
    problem = check_measures(_record(("margin", -120), ("share_r", 47)), _measures_of("wikipedia-elections"))
    assert problem["code"] == "E-MEASURE-RANGE"


def test_a_negative_value_of_a_signed_measure_within_range_passes():
    assert check_measures(_record(("margin", -1.5), ("share_r", 47)), _measures_of("wikipedia-elections")) is None


def test_a_probability_above_1_is_refused():
    problem = check_measures(_record(("epss", 1.2), ("epss_percentile", 0.999)), _measures_of("epss"))
    assert problem["code"] == "E-MEASURE-RANGE"


def test_a_value_that_is_not_a_number_is_refused():
    for value in ("7.5", True, None, float("nan"), float("inf")):
        problem = check_measures(_record(("magnitude", value)), _measures_of("usgs"))
        assert problem["code"] == "E-MEASURE", value


def test_a_measure_the_packages_do_not_declare_is_not_checked():
    assert check_measures(_record(("not_declared", -3)), _measures_of("usgs")) is None


def test_a_record_without_measures_passes():
    assert check_measures({"kind": "entity", "id": "ent:test:1"}, _measures_of("kev")) is None


def test_the_reason_names_the_measure_and_its_position():
    problem = check_measures(_record(("magnitude", 7.5), ("depth_km", -20)), _measures_of("usgs"))
    assert "measures[1] (depth_km)" in problem["reason"]
