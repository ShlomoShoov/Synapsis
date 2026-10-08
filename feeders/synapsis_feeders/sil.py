"""Small builders for SIL 1.2 records. They only assemble dictionaries; the API is the one that judges them.

Every builder drops fields that are None, so a feeder never sends a null where SIL expects a value to be absent
(rule M-NULL: missing is not zero, and missing is not null either)."""
from __future__ import annotations

import math
import re
import unicodedata
from datetime import date, datetime, timedelta, timezone

UTC = timezone.utc


# ------------------------------------------------------------------------------------------------------ time
def utc(value: datetime | date | str) -> datetime:
    """Any date, datetime or ISO string -> an aware UTC datetime."""
    if isinstance(value, datetime):
        return value.replace(tzinfo=UTC) if value.tzinfo is None else value.astimezone(UTC)
    if isinstance(value, date):
        return datetime(value.year, value.month, value.day, tzinfo=UTC)
    text = value.strip().replace(" ", "T")
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    return utc(datetime.fromisoformat(text))


def iso(value: datetime | date | str) -> str:
    return utc(value).strftime("%Y-%m-%dT%H:%M:%SZ")


def floor(value: datetime, precision: str) -> datetime:
    """The start of the period `value` falls in, for the precisions SIL knows."""
    v = utc(value)
    if precision == "second":
        return v.replace(microsecond=0)
    if precision == "minute":
        return v.replace(second=0, microsecond=0)
    if precision == "hour":
        return v.replace(minute=0, second=0, microsecond=0)
    d = v.replace(hour=0, minute=0, second=0, microsecond=0)
    if precision == "day":
        return d
    if precision == "week":
        return d - timedelta(days=d.weekday())
    if precision == "month":
        return d.replace(day=1)
    if precision == "quarter":
        return d.replace(day=1, month=3 * ((d.month - 1) // 3) + 1)
    if precision == "year":
        return d.replace(day=1, month=1)
    raise ValueError(precision)


def time_spec(start: datetime | date | str, precision: str, end: datetime | date | str | None = None) -> dict:
    out = {"start": iso(floor(utc(start), precision)), "precision": precision}
    if end is not None and utc(end) > utc(out["start"]):
        out["end"] = iso(end)
    return out


# ------------------------------------------------------------------------------------------------------ ids
_SLUG = re.compile(r"[^a-z0-9]+")


def slug(text: str, max_len: int = 120) -> str:
    """Lower-case ASCII words joined by '-': 'Progress Software|MOVEit' -> 'progress-software-moveit'."""
    t = unicodedata.normalize("NFKD", str(text)).encode("ascii", "ignore").decode("ascii").lower()
    s = _SLUG.sub("-", t).strip("-")
    return (s[:max_len].rstrip("-")) or "unknown"


def key(text: str) -> str:
    """A record key from a source identifier, keeping it readable: spaces and symbols become '-'."""
    k = re.sub(r"[^A-Za-z0-9._:/@+~-]+", "-", str(text).strip()).strip("-")
    return k[:240] or "unknown"


# ------------------------------------------------------------------------------------------------------ places
def point(lon: float, lat: float, uncertainty_km: float) -> dict:
    return {"point": [round(float(lon), 6), round(float(lat), 6)], "uncertainty_km": float(uncertainty_km)}


def admin(country: str, subdivision: str | None = None, name: str | None = None) -> dict:
    a = {"country": country}
    if subdivision:
        a["subdivision"] = subdivision
    if name:
        a["name"] = str(name)[:300]
    return {"admin": a}


def logical(space: str, *path: str) -> dict:
    return {"logical": {"space": space, "path": [str(p).replace("/", "-")[:200] for p in path if p not in (None, "")]}}


def place(*parts: dict | None) -> dict:
    out: dict = {}
    for p in parts:
        if p:
            out.update(p)
    return out


# ------------------------------------------------------------------------------------------------------ values
def finite(value) -> float | None:
    """A float, or None for missing, NaN and infinities (M-NULL)."""
    if value is None or value == "":
        return None
    try:
        f = float(value)
    except (TypeError, ValueError):
        return None
    return f if math.isfinite(f) else None


def measures(*items: tuple[str, object, str]) -> list[dict]:
    """[(name, value, unit), ...] -> SIL measures, skipping missing values."""
    out = []
    for name, value, unit in items:
        v = finite(value)
        if v is not None:
            out.append({"name": name, "value": int(v) if v.is_integer() and abs(v) < 2**53 else v, "unit": unit})
    return out


def text(title: str | None = None, body: str | None = None, lang: str | None = "en") -> dict | None:
    t = {}
    if title and str(title).strip():
        t["title"] = str(title).strip()[:1000]
    if body and str(body).strip():
        t["body"] = str(body).strip()[:20000]
    if t and lang:
        t["lang"] = lang
    return t or None


def attributes(**values) -> dict | None:
    out = {}
    for k, v in values.items():
        if v is None or v == "" or v == []:
            continue
        if isinstance(v, float) and not math.isfinite(v):
            continue
        out[k] = v
    return out or None


# ------------------------------------------------------------------------------------------------------ records
def _envelope(kind: str, id: str, type: str, source: dict, known_at, known_at_basis: str, reliability: int | None = None) -> dict:
    return {"kind": kind, "id": id, "type": type, "source": source["id"],
            "reliability": int(reliability if reliability is not None else source["reliability"]),
            "reliability_basis": source["reliability_basis"], "known_at": iso(known_at), "known_at_basis": known_at_basis}


def _extend(rec: dict, **fields) -> dict:
    for k, v in fields.items():
        if v not in (None, [], {}):
            rec[k] = v
    return rec


def event(id: str, type: str, source: dict, time: dict, place: dict, known_at, known_at_basis: str, *,
          participants: list | None = None, measures: list | None = None, text: dict | None = None,
          attributes: dict | None = None, evidence: dict | None = None, reliability: int | None = None) -> dict:
    rec = _envelope("event", id, type, source, known_at, known_at_basis, reliability)
    rec["time"] = time
    rec["place"] = place
    return _extend(rec, participants=participants, measures=measures, text=text, attributes=attributes, evidence=evidence)


def entity(id: str, type: str, name: str, source: dict, known_at, known_at_basis: str, *,
           aliases: list | None = None, attributes: dict | None = None, place: dict | None = None,
           text: dict | None = None, reliability: int | None = None) -> dict:
    rec = _envelope("entity", id, type, source, known_at, known_at_basis, reliability)
    rec["name"] = str(name).strip()[:500] or id.split(":", 2)[2]
    clean_aliases = sorted({a.strip()[:500] for a in aliases or [] if a and a.strip() and a.strip() != rec["name"]})
    return _extend(rec, aliases=clean_aliases[:100], attributes=attributes, place=place, text=text)


def relation(id: str, type: str, source: dict, frm: str, to: str, known_at, known_at_basis: str, *,
             time: dict | None = None, attributes: dict | None = None, reliability: int | None = None) -> dict:
    rec = _envelope("relation", id, type, source, known_at, known_at_basis, reliability)
    rec["from"], rec["to"] = frm, to
    return _extend(rec, time=time, attributes=attributes)


def conclusion(id: str, type: str, source: dict, about: list[str], statement: dict, premises: list[str],
               method: dict, confidence: float, known_at, known_at_basis: str, *, reliability: int | None = None,
               text: dict | None = None) -> dict:
    rec = _envelope("conclusion", id, type, source, known_at, known_at_basis, reliability)
    rec.update({"about": about, "statement": statement, "premises": premises, "method": method,
                "confidence": round(float(confidence), 2)})
    return _extend(rec, text=text)


def participant(entity_id: str, role: str) -> dict:
    return {"entity": entity_id, "role": role}
