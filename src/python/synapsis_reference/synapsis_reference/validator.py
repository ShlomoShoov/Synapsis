"""SilValidator: the SIL 1.2 rules, applied in a fixed order. The first failure decides a record's code.

Order: kind, envelope structure, source, identity, type, valid time, knowledge time, place, measures, text,
attributes, then (for the whole batch) roles and references, premises and duplicate ids. The C# SilValidator must keep
the same order, because a record that breaks two rules must get the same code from both implementations."""
from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from typing import Iterable, Protocol

from jsonschema import Draft202012Validator

from . import errors as E
from .errors import RecordError
from .packages import canonical_json
from .registry import AttributeRule, RegistrySnapshot

KINDS = ("entity", "event", "relation", "conclusion")
ID_PREFIX = {"entity": "ent", "event": "evt", "relation": "rel", "conclusion": "con"}
INSTANT = re.compile(r"^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,6}))?Z$")
DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
SUBJECT_KEY = re.compile(r"^subject:")
FUTURE_TOLERANCE = timedelta(minutes=5)


def parse_instant(value: str, path: str) -> datetime:
    m = INSTANT.match(value)
    if not m:
        raise RecordError(E.E_SCHEMA, path, f"'{value}' is not a UTC instant like 2024-01-31T00:00:00Z")
    try:
        frac = int((m.group(7) or "0").ljust(6, "0"))
        return datetime(*map(int, m.groups()[:6]), frac, tzinfo=timezone.utc)
    except ValueError as exc:
        raise RecordError(E.E_SCHEMA, path, f"'{value}' is not a real date: {exc}") from exc


def aligned(t: datetime, precision: str) -> bool:
    """R3: the start of a period must sit on that period's boundary."""
    if precision == "second":
        return t.microsecond == 0
    if precision == "minute":
        return t.second == 0 and t.microsecond == 0
    if precision == "hour":
        return t.minute == 0 and t.second == 0 and t.microsecond == 0
    day = t.hour == 0 and t.minute == 0 and t.second == 0 and t.microsecond == 0
    if precision == "day":
        return day
    if precision == "week":
        return day and t.weekday() == 0
    if precision == "month":
        return day and t.day == 1
    if precision == "quarter":
        return day and t.day == 1 and t.month in (1, 4, 7, 10)
    if precision == "year":
        return day and t.day == 1 and t.month == 1
    return False


def dotted(parts) -> str:
    """A JSON Schema error path in the same form as every other rule's path: measures[2].value. Empty: the record."""
    out = ""
    for part in parts:
        out += f"[{part}]" if isinstance(part, int) else (f".{part}" if out else str(part))
    return out


def version_hash(record: dict) -> str:
    """Content that decides whether a re-sent record is new: everything except when and how it became known."""
    body = {k: v for k, v in record.items() if k not in ("known_at", "known_at_basis")}
    return canonical_json(body)


class Resolver(Protocol):
    """What the validator needs from the store: which referenced ids exist already, and the types of entities."""

    def entity_types(self, ids: Iterable[str]) -> dict[str, set[str]]: ...

    def existing(self, ids: Iterable[str]) -> set[str]: ...


@dataclass
class Prepared:
    """A record that passed every rule, with what the writer needs."""
    index: int
    kind: str
    record: dict
    known_at: datetime
    content: str = field(repr=False)


class SilValidator:
    def __init__(self, sil_schema_path: str):
        with open(sil_schema_path, encoding="utf-8") as fh:
            schema = json.load(fh)
        self._bundle = Draft202012Validator({k: v for k, v in schema.items() if k != "$defs"} | {"$defs": schema["$defs"]})
        self._kinds = {kind: Draft202012Validator({"$defs": schema["$defs"], "$ref": f"#/$defs/{kind}"}) for kind in KINDS}

    # ------------------------------------------------------------------------------------------------ the bundle
    def bundle_errors(self, bundle: dict) -> list[dict]:
        return [{"path": "/".join(map(str, e.absolute_path)) or "/", "message": e.message[:300]}
                for e in self._bundle.iter_errors(bundle)][:20]

    # --------------------------------------------------------------------------------------------- one record
    def check_record(self, index: int, rec: dict, reg: RegistrySnapshot, received_at: datetime) -> Prepared:
        """Every rule that needs only this record and the registry. Raises RecordError at the first failure."""
        kind = rec.get("kind") if isinstance(rec, dict) else None
        if kind not in KINDS:
            raise RecordError(E.E_KIND, "kind", f"kind must be one of {list(KINDS)}; sources are declared in schema packages")
        errs = sorted(self._kinds[kind].iter_errors(rec), key=lambda e: [str(x) for x in e.absolute_path])
        if errs:
            e0 = errs[0]
            raise RecordError(E.E_SCHEMA, dotted(e0.absolute_path), e0.message[:300])

        # source
        source = rec["source"]
        if source not in reg.sources:
            raise RecordError(E.E_REGISTRY_SOURCE, "source", f"source '{source}' is not registered")
        if rec["reliability_basis"] not in reg.reliability_bases:
            raise RecordError(E.E_SCHEMA, "reliability_basis", f"unknown reliability basis '{rec['reliability_basis']}'")
        if rec["known_at_basis"] not in reg.known_at_bases:
            raise RecordError(E.E_SCHEMA, "known_at_basis", f"unknown known_at basis '{rec['known_at_basis']}'")

        # identity: prefix matches kind; namespace owned by the source's package, or a shared namespace
        prefix, ns, key = rec["id"].split(":", 2)
        if prefix != ID_PREFIX[kind]:
            raise RecordError(E.E_ID_NAMESPACE, "id", f"a {kind} id starts with '{ID_PREFIX[kind]}:'")
        package = reg.source_package[source]
        if kind == "entity":
            if ns in reg.shared_ns:
                etype, pattern = reg.shared_ns[ns]
                if not pattern.match(key) or rec["type"] != etype:
                    raise RecordError(E.E_ID_NAMESPACE, "id", f"shared namespace '{ns}' holds {etype} entities with keys like {pattern.pattern}")
            elif reg.entity_ns.get(ns) != package:
                raise RecordError(E.E_ID_NAMESPACE, "id", f"entity namespace '{ns}' is not owned by package '{package}'")
        elif reg.records_ns.get(ns) != package:
            raise RecordError(E.E_ID_NAMESPACE, "id", f"namespace '{ns}' is not owned by package '{package}' (source {source})")

        # type
        tdef = reg.types[kind].get(rec["type"])
        if tdef is None:
            raise RecordError(E.E_REGISTRY_TYPE[kind], "type", f"{kind} type '{rec['type']}' is not registered")

        # valid time
        start = None
        if "time" in rec:
            t = rec["time"]
            start = parse_instant(t["start"], "time.start")
            if t["precision"] not in reg.precisions or not aligned(start, t["precision"]):
                raise RecordError(E.E_TIME_ALIGN, "time.start", f"start {t['start']} is not aligned to precision '{t['precision']}'")
            if "end" in t and parse_instant(t["end"], "time.end") <= start:
                raise RecordError(E.E_TIME_ORDER, "time.end", "end must be after start")

        # knowledge time
        known_at = parse_instant(rec["known_at"], "known_at")
        if start is not None and known_at < start:
            raise RecordError(E.E_KNOWN_BEFORE_OCCURRENCE, "known_at", "a record cannot be known before it started")
        if known_at > received_at + FUTURE_TOLERANCE:
            raise RecordError(E.E_KNOWN_IN_FUTURE, "known_at", "known_at is in the future")

        # place
        if "place" in rec:
            self._check_place(rec["place"], kind, tdef, reg)

        # measures
        if kind == "event":
            self._check_measures(rec.get("measures") or [], tdef, reg)

        # text
        policy = tdef.get("text", "optional") if kind == "event" else "optional"
        if policy == "none" and "text" in rec:
            raise RecordError(E.E_TEXT, "text", f"type '{rec['type']}' carries no text")
        if policy == "required" and not (rec.get("text") or {}).get("title") and not (rec.get("text") or {}).get("body"):
            raise RecordError(E.E_TEXT, "text", f"type '{rec['type']}' requires text")

        # attributes
        rules = reg.attributes.get((kind, rec["type"]), {})
        self._check_attributes(rec.get("attributes") or {}, rules)

        # conclusion envelope
        if kind == "conclusion":
            if rec["method"]["by"] not in reg.method_by:
                raise RecordError(E.E_SCHEMA, "method.by", f"method.by must be one of {sorted(reg.method_by)}")
            if rec["statement"]["predicate"] == "in_logical_space":     # rule A-CLOSED for classifications
                space_name, _, rest = (rec["statement"].get("object") or "").partition("/")
                space = reg.spaces.get(space_name)
                path = [p for p in rest.split("/") if p]
                if space is None:
                    raise RecordError(E.E_LOGICAL_SPACE, "statement.object", f"logical space '{space_name}' is not registered")
                if not path or len(path) > space["max_depth"]:
                    raise RecordError(E.E_LOGICAL_DEPTH, "statement.object", f"'{space_name}' paths have 1 to {space['max_depth']} levels")
                if space["closed"] and path[0] not in space["values"]:
                    raise RecordError(E.E_LOGICAL_VALUE, "statement.object", f"'{path[0]}' is not a registered value of '{space_name}'")

        return Prepared(index=index, kind=kind, record=rec, known_at=known_at, content=version_hash(rec))

    @staticmethod
    def _check_place(place: dict, kind: str, tdef: dict, reg: RegistrySnapshot) -> None:
        forms = [f for f in ("point", "admin", "logical", "unplaced") if f in place]
        if not forms:
            raise RecordError(E.E_PLACE_MISSING, "place", "a place needs a point, an admin area, a logical position or unplaced: true")
        if "unplaced" in place and len(forms) > 1:
            raise RecordError(E.E_PLACE_CONFLICT, "place.unplaced", "unplaced cannot be combined with a place")
        if kind == "event":
            bad = [f for f in forms if f not in tdef["place"]]
            if bad:
                raise RecordError(E.E_PLACE_FORM, f"place.{bad[0]}", f"type '{tdef['code']}' allows places {tdef['place']}")
        if "point" in place:
            lon, lat = place["point"]
            if not (-180 <= lon <= 180 and -90 <= lat <= 90):
                raise RecordError(E.E_SCHEMA, "place.point", "longitude or latitude out of range")
            if "uncertainty_km" not in place:
                raise RecordError(E.E_SCHEMA, "place.uncertainty_km", "a point needs uncertainty_km")
        elif "uncertainty_km" in place:
            raise RecordError(E.E_SCHEMA, "place.uncertainty_km", "uncertainty_km belongs to a point")
        if "admin" in place:
            adm = place["admin"]
            if adm["country"] not in reg.countries:
                raise RecordError(E.E_PLACE_ADMIN, "place.admin.country", f"'{adm['country']}' is not an ISO 3166-1 alpha-2 code")
            if "subdivision" in adm and not adm["subdivision"].startswith(adm["country"] + "-"):
                raise RecordError(E.E_PLACE_ADMIN, "place.admin.subdivision", "the subdivision must belong to the country")
        if "logical" in place:
            lg = place["logical"]
            space = reg.spaces.get(lg["space"])
            if space is None:
                raise RecordError(E.E_LOGICAL_SPACE, "place.logical.space", f"logical space '{lg['space']}' is not registered")
            if len(lg["path"]) > space["max_depth"]:
                raise RecordError(E.E_LOGICAL_DEPTH, "place.logical.path", f"'{lg['space']}' is at most {space['max_depth']} deep")
            if space["closed"] and lg["path"][0] not in space["values"]:
                raise RecordError(E.E_LOGICAL_VALUE, "place.logical.path[0]", f"'{lg['path'][0]}' is not a registered value of '{lg['space']}'")

    @staticmethod
    def _check_measures(measures: list, tdef: dict, reg: RegistrySnapshot) -> None:
        seen = set()
        for i, m in enumerate(measures):
            name = m["name"]
            if name not in tdef["measures"] or name in seen:
                raise RecordError(E.E_MEASURE, f"measures[{i}].name", f"measure '{name}' is not allowed for '{tdef['code']}' or repeats")
            seen.add(name)
            mdef = reg.measures[name]
            if m["unit"] != mdef["unit"]:
                raise RecordError(E.E_UNIT, f"measures[{i}].unit", f"'{name}' is measured in '{mdef['unit']}'")
            v = m["value"]
            if v < 0 and not mdef["signed"]:
                raise RecordError(E.E_MEASURE_NEGATIVE, f"measures[{i}].value", f"'{name}' cannot be negative")
            if (mdef["min"] is not None and v < mdef["min"]) or (mdef["max"] is not None and v > mdef["max"]):
                raise RecordError(E.E_MEASURE_RANGE, f"measures[{i}].value", f"'{name}' must lie in [{mdef['min']}, {mdef['max']}]")

    @staticmethod
    def _check_attributes(attrs: dict, rules: dict[str, AttributeRule]) -> None:
        for name, value in attrs.items():
            rule = rules.get(name)
            path = f"attributes.{name}"
            if rule is None:
                raise RecordError(E.E_ATTRIBUTE, path, f"attribute '{name}' is not declared for this type")
            ok, why = _attribute_ok(value, rule)
            if not ok:
                raise RecordError(E.E_ATTRIBUTE, path, why)
        for name, rule in rules.items():
            if rule.required and name not in attrs:
                raise RecordError(E.E_ATTRIBUTE, f"attributes.{name}", f"attribute '{name}' is required")

    # ---------------------------------------------------------------------------------------------- the batch
    def validate_batch(self, records: list, reg: RegistrySnapshot, resolver: Resolver,
                       received_at: datetime) -> tuple[list[Prepared], list[dict], int]:
        """Returns (accepted, rejected, duplicates). Duplicates are identical copies of a record earlier in the batch."""
        prepared: list[Prepared] = []
        rejected: list[dict] = []

        def reject(i: int, rec, err: RecordError):
            rid = rec.get("id") if isinstance(rec, dict) else None
            rejected.append({"index": i, "id": rid, "code": err.code, "path": err.path, "message": err.message})

        for i, rec in enumerate(records):
            try:
                prepared.append(self.check_record(i, rec, reg, received_at))
            except RecordError as err:
                reject(i, rec, err)

        # duplicates inside the batch: identical copies are dropped, different content under one id is a conflict
        first: dict[tuple, Prepared] = {}
        unique: list[Prepared] = []
        duplicates = 0
        for p in prepared:
            k = (p.record["id"], p.record["source"])
            if k not in first:
                first[k] = p
                unique.append(p)
            elif first[k].content == p.content:
                duplicates += 1
            else:
                reject(p.index, p.record, RecordError(E.E_ID_CONFLICT, "id", "the same id appears twice in the batch with different content"))

        # references. Stored ids are looked up once for the whole batch; ids from the batch count only once the
        # record carrying them has been accepted, so kinds are checked in dependency order.
        wanted_entities, wanted_any = set(), set()
        for p in unique:
            r = p.record
            if p.kind == "event":
                wanted_entities.update(x["entity"] for x in r.get("participants") or [])
            elif p.kind == "relation":
                wanted_entities.update((r["from"], r["to"]))
            elif p.kind == "conclusion":
                wanted_any.update(a for a in r["about"] if not SUBJECT_KEY.match(a))
                wanted_any.update(r["premises"])
        stored_types = resolver.entity_types(wanted_entities) if wanted_entities else {}
        stored_any = resolver.existing(wanted_any) if wanted_any else set()
        local_types: dict[str, set[str]] = {}
        local_ids: set[str] = set()

        def types_of(eid: str) -> set[str] | None:
            found = local_types.get(eid, set()) | stored_types.get(eid, set())
            return found or None

        accepted: list[Prepared] = []
        order = {"entity": 0, "relation": 1, "event": 2, "conclusion": 3}
        for p in sorted(unique, key=lambda q: (order[q.kind], q.index)):
            try:
                self._check_references(p, reg, types_of, local_ids, stored_any)
            except RecordError as err:
                reject(p.index, p.record, err)
                continue
            accepted.append(p)
            local_ids.add(p.record["id"])
            if p.kind == "entity":
                local_types.setdefault(p.record["id"], set()).add(p.record["type"])
        accepted.sort(key=lambda q: q.index)
        rejected.sort(key=lambda x: x["index"])
        return accepted, rejected, duplicates

    @staticmethod
    def _check_references(p: Prepared, reg: RegistrySnapshot, types_of, local_ids: set[str], stored_any: set[str]) -> None:
        r = p.record
        if p.kind == "event":
            tdef = reg.types["event"][r["type"]]
            for i, part in enumerate(r.get("participants") or []):
                role, eid = part["role"], part["entity"]
                if role not in tdef["roles"]:
                    raise RecordError(E.E_ROLE, f"participants[{i}].role", f"role '{role}' is not allowed for '{r['type']}'")
                types = types_of(eid)
                if not types:
                    raise RecordError(E.E_REF_MISSING, f"participants[{i}].entity", f"entity '{eid}' is neither in the batch nor stored")
                allowed = tdef["roles"][role]
                if not types & set(allowed):
                    raise RecordError(E.E_ROLE, f"participants[{i}].entity", f"role '{role}' takes {allowed}, not {sorted(types)}")
        elif p.kind == "relation":
            tdef = reg.types["relation"][r["type"]]
            for end in ("from", "to"):
                types = types_of(r[end])
                if not types:
                    raise RecordError(E.E_REF_MISSING, end, f"entity '{r[end]}' is neither in the batch nor stored")
                if not types & set(tdef[end]):
                    raise RecordError(E.E_RELATION_DOMAIN, end, f"'{r['type']}' {end} takes {tdef[end]}, not {sorted(types)}")
        elif p.kind == "conclusion":
            for i, a in enumerate(r["about"]):
                if not SUBJECT_KEY.match(a) and a not in local_ids and a not in stored_any:
                    raise RecordError(E.E_REF_MISSING, f"about[{i}]", f"'{a}' is neither in the batch nor stored")
            for i, prem in enumerate(r["premises"]):
                if prem not in local_ids and prem not in stored_any:
                    raise RecordError(E.E_PREMISE_MISSING, f"premises[{i}]", f"premise '{prem}' is neither in the batch nor stored")


def _attribute_ok(value, rule: AttributeRule) -> tuple[bool, str]:
    t = rule.type
    if t == "string":
        if not isinstance(value, str):
            return False, "must be a string"
        items = [value]
    elif t == "string_list":
        if not isinstance(value, list) or not all(isinstance(v, str) for v in value):
            return False, "must be a list of strings"
        if rule.max_items is not None and len(value) > rule.max_items:
            return False, f"at most {rule.max_items} items"
        items = value
    elif t == "integer":
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not float(value).is_integer():
            return False, "must be an integer"
        if rule.min is not None and value < rule.min or rule.max is not None and value > rule.max:
            return False, f"must lie in [{rule.min}, {rule.max}]"
        if rule.enum is not None and value not in rule.enum:
            return False, f"must be one of {sorted(rule.enum)}"
        return True, ""
    elif t == "boolean":
        return (isinstance(value, bool), "must be true or false")
    elif t == "date":
        if not isinstance(value, str) or not DATE.match(value):
            return False, "must be a date like 2024-01-31"
        try:
            datetime.strptime(value, "%Y-%m-%d")
        except ValueError:
            return False, "is not a real date"
        return True, ""
    else:
        return False, f"unknown attribute type {t}"
    for s in items:
        if rule.max_length is not None and len(s) > rule.max_length:
            return False, f"longer than {rule.max_length} characters"
        if rule.enum is not None and s not in rule.enum:
            return False, f"must be one of {sorted(rule.enum)}"
        if rule.pattern is not None and not rule.pattern.match(s):
            return False, f"does not match {rule.pattern.pattern}"
    return True, ""
