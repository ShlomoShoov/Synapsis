"""The one check a feeder makes before sending: every measure value is a number that its package allows.

The API no longer knows the schema packages, so this check lives here. For each measure of a record:
  1. the value must be a number (not text, not true/false, not NaN or infinity)       -> E-MEASURE
  2. it must not be negative, unless the package declares the measure `signed`        -> E-MEASURE-NEGATIVE
  3. it must be within the package's `min` and `max`, when they are given             -> E-MEASURE-RANGE
A measure the packages do not declare is not checked (that check is on the later list). A record that fails is not
sent: the runner writes it to rejections.jsonl, so nothing is fixed silently."""
from __future__ import annotations

import json
import math

from .config import Settings


def declared_measures(settings: Settings, feeder_class) -> dict:
    """Every measure the feeder may send, by name: from the core package, the shared packages it requires and its own."""
    packages = ["core", *feeder_class.requires, feeder_class.package]
    measures = {}
    for package in packages:
        content = json.loads(settings.package_file(package).read_text(encoding="utf-8"))
        for measure in content.get("measures") or []:
            measures[measure["name"]] = measure
    return measures


def check_measures(record: dict, measures: dict) -> dict | None:
    """None when every measure of the record passes; otherwise the first problem as {"code", "reason"}."""
    for position, measure in enumerate(record.get("measures") or []):
        name = measure.get("name")
        value = measure.get("value")
        where = f"measures[{position}] ({name})"
        if not _is_number(value):
            return {"code": "E-MEASURE", "reason": f"{where}: the value {value!r} is not a number"}
        declared = measures.get(name)
        if declared is None:
            continue
        if value < 0 and not declared.get("signed", False):
            return {"code": "E-MEASURE-NEGATIVE", "reason": f"{where}: {value} is negative, and the measure is not signed"}
        if "min" in declared and value < declared["min"]:
            return {"code": "E-MEASURE-RANGE", "reason": f"{where}: {value} is below the minimum {declared['min']}"}
        if "max" in declared and value > declared["max"]:
            return {"code": "E-MEASURE-RANGE", "reason": f"{where}: {value} is above the maximum {declared['max']}"}
    return None


def _is_number(value) -> bool:
    """True for an int or a float that is a real number; False for text, true/false, None, NaN and infinity."""
    if isinstance(value, bool):
        return False
    if isinstance(value, int):
        return True
    if isinstance(value, float):
        return math.isfinite(value)
    return False
