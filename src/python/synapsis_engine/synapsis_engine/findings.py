"""Conclusion builders: ids, titles, statements. Every conclusion is a SIL record sent through POST /ingest."""
import hashlib
import re

import pandas as pd

from . import config

RELIABILITY = 70


def iso(ts):
    return pd.Timestamp(ts).strftime("%Y-%m-%dT%H:%M:%SZ")


def day(ts):
    return pd.Timestamp(ts).strftime("%Y-%m-%d")


def num(x):
    """A number as stored: integers stay integers, others keep 6 significant digits."""
    if x is None:
        return None
    x = float(x)
    if x.is_integer() and abs(x) < 1e15:
        return int(x)
    return float(f"{x:.6g}")


def fmt(x, digits=3):
    if x is None:
        return "n/a"
    x = float(x)
    if x.is_integer() and abs(x) < 1e15:
        return f"{int(x):,}"
    return f"{x:.{digits}g}"


def key(s):
    """A safe id segment (the SIL record id alphabet); long keys keep a hash of the original."""
    k = re.sub(r"[^A-Za-z0-9._:/@+~-]", "~", str(s))
    if not k[:1].isalnum():
        k = "x" + k
    return k if len(k) <= 120 else k[:100] + "~" + hashlib.sha1(str(s).encode()).hexdigest()[:12]


def source_key(source):
    return key(source.removeprefix("src:").replace(":", "-"))


def subject_about(kind, subject, resolution=None):
    """(about ids, subject key, extra attributes) for a subject."""
    if kind == "cell":
        return ["ent:h3:" + subject], subject, {}
    if kind == "logical":
        space, _, rest = subject.partition("/")
        return (["ent:logical:" + key(subject)], key(subject),
                {"logical": {"space": space, "path": rest.split("/") if rest else []}})
    return [subject], key(subject), {}


def logical_about(space, *path):
    subject = "/".join([space, *path])
    return ["ent:logical:" + key(subject)], {"logical": {"space": space, "path": list(path)}}


def conclusion(ctype, src, subject_key, period, known_at, about, value, title, premises, method, params,
               period_start, precision, attributes=None, counted_max=None):
    """(record, meta): meta carries what the proofs check (the latest known_at of the events counted)."""
    rec = {"kind": "conclusion", "id": f"con:engine:{ctype}:{source_key(src)}:{subject_key}:{period}", "type": ctype,
           "source": config.ENGINE_SOURCE, "reliability": RELIABILITY, "reliability_basis": "model",
           "known_at": iso(known_at), "known_at_basis": "observed", "about": about,
           "statement": {"predicate": ctype, "value": {k: num(v) for k, v in value.items() if v is not None}},
           "premises": sorted(dict.fromkeys(premises))[:50], "method": {"by": "system", "name": method, "version": "1"},
           "confidence": RELIABILITY,
           "attributes": {"period_start": iso(period_start), "period_precision": precision, "params": params,
                          **(attributes or {})},
           "text": {"title": title}}
    return rec, {"source": src, "known_at": pd.Timestamp(known_at), "counted_max": counted_max}


def cell_entities(records):
    """Each referenced H3 cell once, as a MapCell entity known from the first conclusion that names it."""
    first = {}
    for r in records:
        for a in r.get("about", []):
            if a.startswith("ent:h3:") and (a not in first or r["known_at"] < first[a]):
                first[a] = r["known_at"]
    out = []
    for cid, known in sorted(first.items()):
        cell = cid.removeprefix("ent:h3:")
        res = int(cell[1], 16)
        out.append({"kind": "entity", "id": cid, "type": "MapCell", "name": f"{cell} (H3 r{res})",
                    "source": config.ENGINE_SOURCE, "reliability": RELIABILITY, "reliability_basis": "model",
                    "known_at": known, "known_at_basis": "observed"})
    return out
