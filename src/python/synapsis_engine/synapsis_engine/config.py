"""engine.yaml (declarations the data cannot tell) and the shapes found in the data (everything else)."""
import json
import math
import os
import pathlib

import numpy as np
import pandas as pd

ROOT = pathlib.Path(__file__).resolve().parent.parent
ENGINE_SOURCE = "src:synapsis:engine"
IGNORED = ["src:synapsis:engine", "src:synapsis:engine-test"]
PRECISIONS = ["second", "minute", "hour", "day", "week", "month", "quarter", "year"]
RANK = {p: i for i, p in enumerate(PRECISIONS)}
EDGE_KM = {2: 158.2, 3: 59.8, 4: 22.6, 5: 8.5}          # a point gets a cell at r only if uncertainty_km < edge(r)
FREQ = {"day": "D", "week": "W-MON", "month": "MS", "quarter": "QS", "year": "YS"}
HOURS = {"day": 24.0, "week": 168.0, "month": 730.5, "quarter": 2191.5, "year": 8766.0}
MEASUREMENT_ONE_SHARE = 0.8      # measurement: >= 80% of (subject, period) pairs hold exactly one record
CELL_SHARE = 0.5                 # occurrence subject: the r3 cell if >= 50% of the events have one
CALIBRATION_SHARE = 0.6          # calibration: the first 60% of the source's buckets
MIN_CALIBRATION_BUCKETS = 8      # fewer: no fit, the coverage report says so


def mongo_url():
    return os.environ.get("ENGINE_MONGO_URL", "mongodb://localhost:27017/?directConnection=true")


def mongo_db():
    return os.environ.get("ENGINE_MONGO_DB", "synapsis_v2")


def api_url():
    return os.environ.get("SYNAPSIS_API_URL", "http://localhost:8080").rstrip("/")


def data_dir():
    d = pathlib.Path(os.environ.get("ENGINE_DATA", ROOT / "data"))
    d.mkdir(parents=True, exist_ok=True)
    return d


def load(path=None):
    text = pathlib.Path(path or ROOT / "engine.yaml").read_text()
    return json.loads("\n".join(line for line in text.splitlines() if not line.lstrip().startswith("#")))


def bucket_start(ts: pd.Series, bucket: str) -> pd.Series:
    """Start of the bucket holding each time (naive UTC). Weeks start on Monday (as the reference's W-SUN periods)."""
    if bucket == "day":
        return ts.dt.floor("D")
    return ts.dt.to_period({"week": "W-SUN", "month": "M", "quarter": "Q", "year": "Y"}[bucket]).dt.start_time


def offset(bucket: str):
    return pd.tseries.frequencies.to_offset(FREQ[bucket])


def _per_expr():
    whens = " ".join(f"WHEN '{p}' THEN date_trunc('{p}', t_start)" for p in PRECISIONS)
    return f"CASE prec {whens} END"


def find_shapes(ws, decl):
    """Per source: bucket, lag, and per event type its shape (measurement or occurrence) and subject rule."""
    pins = decl["pins"]
    stats = ws.con.execute("""
        SELECT source, type, count(*) n, avg(has_m::INT) m_share,
               avg((lon IS NOT NULL AND unc < 59.8)::INT) cell_share, avg((lkey IS NOT NULL)::INT) logical_share,
               avg((p0 IS NOT NULL)::INT) part_share, min(prank) finest
        FROM events GROUP BY 1, 2 ORDER BY 1, 2""").df()
    pairs = ws.con.execute(f"""
        WITH x AS (SELECT source, type, coalesce(p0, lkey, subdivision, country) subj, {_per_expr()} per
                   FROM events WHERE has_m),
             g AS (SELECT source, type, subj, per, count(*) c FROM x
                   WHERE subj IS NOT NULL AND per IS NOT NULL GROUP BY ALL)
        SELECT source, type, avg((c = 1)::INT) one_share, count(*) pairs FROM g GROUP BY 1, 2""").df()
    one = {(r.source, r.type): (r.one_share, r.pairs) for r in pairs.itertuples()}
    shapes = {}
    for source, g in stats.groupby("source", sort=True):
        pin = pins.get(source)
        finest = int(g.finest.min())
        if pin:
            bucket = pin["bucket"]
        elif finest <= RANK["day"]:
            bucket = "week"
        else:
            bucket = PRECISIONS[finest]
        types = {}
        for r in g.itertuples():
            share, npairs = one.get((source, r.type), (0.0, 0))
            t = {"n": int(r.n), "measures_share": round(float(r.m_share), 3), "one_record_share": round(float(share), 3),
                 "cell_share": round(float(r.cell_share), 3), "logical_share": round(float(r.logical_share), 3),
                 "participant_share": round(float(r.part_share), 3), "finest": PRECISIONS[int(r.finest)]}
            if pin:
                t.update(shape="occurrence", subject="cell", resolution=pin["resolution"],
                         rule=f"pinned in engine.yaml: H3 r{pin['resolution']}, {bucket}")
            elif r.m_share >= 0.5 and npairs and share >= MEASUREMENT_ONE_SHARE:
                t.update(shape="measurement", subject="entity|logical|admin",
                         rule=f"measurement: {share:.0%} of {npairs} (subject, period) pairs hold one record (>= 80%)")
            else:
                why = (f"no measures ({r.m_share:.0%} of events)" if r.m_share < 0.5
                       else f"only {share:.0%} of (subject, period) pairs hold one record (< 80%)")
                if r.cell_share >= CELL_SHARE:
                    subj, res = "cell", 3
                else:
                    cand = [("logical", r.logical_share), ("participant", r.part_share)]
                    best = next((c for c in cand if c[1] >= 0.5), max(cand, key=lambda c: c[1]))
                    subj, res = (best[0], None) if best[1] > 0 else (None, None)
                t.update(shape="occurrence", subject=subj, resolution=res,
                         rule=f"occurrence ({why}); subject: " + {"cell": "H3 r3 cell (>= 50% of events have one)",
                                                                  "logical": "logical prefix at depth 2",
                                                                  "participant": "first participant entity",
                                                                  None: "none: no cell, logical path or participant"}[subj])
            types[r.type] = t
        shapes[source] = {"bucket": bucket, "pinned": bool(pin), "types": types}
    for source, s in shapes.items():
        s.update(lag(ws, source, s["bucket"]))
        pinned = (pins.get(source) or {}).get("lag_hours")
        if pinned is not None:
            s.update(lag_measured=s["lag_hours"], lag_hours=float(pinned),
                     L=int(math.floor(pinned / HOURS[s["bucket"]])))
    return shapes


def lag(ws, source, bucket):
    """lag_hours = the 99th percentile of (known_at - end of the event's bucket), floored at 0; L = whole buckets in it."""
    ev = ws.con.execute("SELECT known_at, t_start FROM events WHERE source = ? AND prank <= ? AND t_start IS NOT NULL",
                        [source, RANK[bucket]]).df()
    if ev.empty:
        return {"lag_hours": None, "L": None, "lag_events": 0}
    end = bucket_start(ev.t_start, bucket) + offset(bucket)
    hours = ((ev.known_at - end).dt.total_seconds() / 3600.0).to_numpy()
    lag_hours = max(0.0, float(np.percentile(hours, 99)))
    return {"lag_hours": round(lag_hours, 6), "L": int(math.floor(lag_hours / HOURS[bucket])), "lag_events": len(ev)}
