"""LatticeCalculator: the shared-space keys every store uses, computed once at the door.

Precision rule: a point gets an H3 cell at a resolution only if its uncertainty is smaller than that resolution's
edge length, so a country centroid never pretends to be a city."""
from __future__ import annotations

from datetime import date, datetime, timedelta

import h3

EDGE_KM = {2: 158.2, 3: 59.8, 4: 22.6, 5: 8.5}


def week_start(day: date) -> str:
    """ISO week, Monday first, as a date string."""
    return (day - timedelta(days=day.weekday())).isoformat()


def lattice_keys(record: dict, start: datetime | None) -> dict:
    out: dict = {}
    place = record.get("place") or {}
    if "point" in place:
        lon, lat = place["point"]
        u = place["uncertainty_km"]
        for res, edge in EDGE_KM.items():
            if u < edge:
                out[f"h3_{res}"] = h3.latlng_to_cell(lat, lon, res)
    if start is not None:
        out["week"] = week_start(start.date())
    if "logical" in place:
        path = place["logical"]["path"]
        out["lspace"] = place["logical"]["space"]
        out["lpath"] = ["/".join(path[: i + 1]) for i in range(len(path))]
    if "admin" in place:
        out["country"] = place["admin"]["country"]
        if "subdivision" in place["admin"]:
            out["subdivision"] = place["admin"]["subdivision"]
    return out
