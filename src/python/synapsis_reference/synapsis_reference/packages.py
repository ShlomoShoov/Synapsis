"""Schema packages: parsing, the canonical hash, the flat list of definitions a package owns, and the change rules.

A package is turned into definitions keyed '<kind>:<code>' (for example 'event_type:CYB.VULN.KNOWN_EXPLOITED').
Every definition has exactly one owner package; that is what registry_entries stores. Comparing the definitions of
two versions of a package gives the list of changes and, for each change, whether it would break stored records."""
from __future__ import annotations

import hashlib
import json
from typing import Any

import yaml
from jsonschema import Draft202012Validator

from .errors import ApiError, E_SCHEMA_PACKAGE, E_SCHEMA_REF

DEFINITION_KINDS = (
    "vocabulary", "shared_namespace", "records_namespace", "entity_namespace", "source", "entity_type",
    "event_type", "relation_type", "conclusion_type", "role", "measure", "logical_space",
)


# ----------------------------------------------------------------------------------------------- parsing and hashing
def parse_package(raw: bytes | str, content_type: str | None = None) -> dict:
    """Read a package sent as JSON or YAML. YAML is a superset of JSON, so one parser serves both."""
    try:
        text = raw.decode("utf-8") if isinstance(raw, bytes) else raw
        data = json.loads(text) if (content_type or "").startswith("application/json") else yaml.safe_load(text)
    except Exception as exc:  # noqa: BLE001 - any parse failure is the producer's error
        raise ApiError(422, E_SCHEMA_PACKAGE, f"The package is not valid YAML or JSON: {exc}") from exc
    if not isinstance(data, dict):
        raise ApiError(422, E_SCHEMA_PACKAGE, "A package must be a mapping at the top level.")
    return data


def canonical_json(obj: Any) -> str:
    """Sorted keys, no spaces, UTF-8: the same content always gives the same bytes."""
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, default=str)


def content_hash(obj: Any) -> str:
    return "sha256:" + hashlib.sha256(canonical_json(obj).encode("utf-8")).hexdigest()


class PackageSchema:
    """The JSON Schema every package must satisfy before any rule looks at it."""

    def __init__(self, schema_path: str):
        with open(schema_path, encoding="utf-8") as fh:
            self._validator = Draft202012Validator(json.load(fh))

    def check(self, package: dict) -> None:
        errors = sorted(self._validator.iter_errors(package), key=lambda e: list(e.absolute_path))
        if errors:
            details = [{"path": "/".join(map(str, e.absolute_path)) or "/", "message": e.message[:300]} for e in errors[:50]]
            raise ApiError(422, E_SCHEMA_PACKAGE, f"The package breaks the package schema in {len(errors)} place(s).", details)


# ---------------------------------------------------------------------------------------------------- definitions
def _attrs(raw: dict | None) -> dict:
    return {name: dict(sorted(spec.items())) for name, spec in sorted((raw or {}).items())}


def definitions(package: dict) -> dict[str, dict]:
    """Flatten a package into normalised definitions. Defaults are filled in so that two packages that mean the
    same thing compare equal."""
    out: dict[str, dict] = {}
    for name, values in (package.get("vocabularies") or {}).items():
        out[f"vocabulary:{name}"] = {"name": name, "values": list(values)}
    for ns in package.get("shared_entity_namespaces") or []:
        out[f"shared_namespace:{ns['namespace']}"] = {
            "namespace": ns["namespace"], "entity_type": ns["entity_type"], "key_pattern": ns["key_pattern"]}
    for ns in (package.get("namespaces") or {}).get("records") or []:
        out[f"records_namespace:{ns}"] = {"namespace": ns}
    for ns in (package.get("namespaces") or {}).get("entities") or []:
        out[f"entity_namespace:{ns}"] = {"namespace": ns}
    for src in package.get("sources") or []:
        out[f"source:{src['id']}"] = dict(sorted(src.items()))
    for t in package.get("entity_types") or []:
        out[f"entity_type:{t['code']}"] = {"code": t["code"], "title": t["title"], "attributes": _attrs(t.get("attributes"))}
    for t in package.get("event_types") or []:
        out[f"event_type:{t['code']}"] = {
            "code": t["code"], "title": t["title"], "place": sorted(t["place"]),
            "roles": {r: sorted(types) for r, types in sorted((t.get("roles") or {}).items())},
            "measures": sorted(t.get("measures") or []), "text": t.get("text", "none"),
            "attributes": _attrs(t.get("attributes"))}
    for t in package.get("relation_types") or []:
        out[f"relation_type:{t['code']}"] = {"code": t["code"], "title": t["title"], "from": sorted(t["from"]),
                                             "to": sorted(t["to"]), "attributes": _attrs(t.get("attributes"))}
    for t in package.get("conclusion_types") or []:
        out[f"conclusion_type:{t['code']}"] = {"code": t["code"], "title": t["title"]}
    for r in package.get("roles") or []:
        out[f"role:{r['code']}"] = {"code": r["code"], "title": r["title"]}
    for m in package.get("measures") or []:
        out[f"measure:{m['name']}"] = {"name": m["name"], "title": m["title"], "unit": m["unit"],
                                       "signed": bool(m.get("signed", False)), "min": m.get("min"), "max": m.get("max")}
    for s in package.get("logical_spaces") or []:
        out[f"logical_space:{s['space']}"] = {"space": s["space"], "title": s["title"],
                                              "max_depth": s["max_depth"], "closed": bool(s.get("closed", False)),
                                              "values": sorted(s.get("values") or [])}
    return out


# ------------------------------------------------------------------------------------------------- change rules
def _attribute_breaks(where: str, old: dict, new: dict) -> list[str]:
    reasons = []
    for name, spec in old.items():
        if name not in new:
            reasons.append(f"{where}: attribute '{name}' removed")
            continue
        n = new[name]
        if n["type"] != spec["type"]:
            reasons.append(f"{where}: attribute '{name}' changed type {spec['type']} -> {n['type']}")
        if n.get("required") and not spec.get("required"):
            reasons.append(f"{where}: attribute '{name}' became required")
        if "enum" in n and set(spec.get("enum") or []) - set(n["enum"]) or ("enum" in n and "enum" not in spec):
            reasons.append(f"{where}: attribute '{name}' lost allowed values")
        if n.get("pattern") and n.get("pattern") != spec.get("pattern"):
            reasons.append(f"{where}: attribute '{name}' changed its pattern")
        for limit in ("max_length", "max_items", "max"):
            if n.get(limit) is not None and (spec.get(limit) is None or n[limit] < spec[limit]):
                reasons.append(f"{where}: attribute '{name}' lowered {limit}")
        if n.get("min") is not None and (spec.get("min") is None or n["min"] > spec["min"]):
            reasons.append(f"{where}: attribute '{name}' raised min")
    for name, n in new.items():
        if name not in old and n.get("required"):
            reasons.append(f"{where}: new attribute '{name}' is required")
    return reasons


def breaking_reasons(key: str, old: dict, new: dict) -> list[str]:
    """Why replacing `old` by `new` would invalidate records that are already stored. Empty list = compatible."""
    kind = key.split(":", 1)[0]
    r: list[str] = []
    if kind == "vocabulary":
        lost = [v for v in old["values"] if v not in new["values"]]
        if lost:
            r.append(f"{key}: values removed {lost}")
    elif kind == "shared_namespace":
        if old["entity_type"] != new["entity_type"] or old["key_pattern"] != new["key_pattern"]:
            r.append(f"{key}: entity type or key pattern changed")
    if kind == "entity_type":
        r += _attribute_breaks(key, old["attributes"], new["attributes"])
    elif kind == "event_type":
        lost_forms = set(old["place"]) - set(new["place"])
        if lost_forms:
            r.append(f"{key}: place forms removed {sorted(lost_forms)}")
        for role, types in old["roles"].items():
            if role not in new["roles"]:
                r.append(f"{key}: role '{role}' removed")
            elif set(types) - set(new["roles"][role]):
                r.append(f"{key}: role '{role}' allows fewer entity types")
        lost_measures = set(old["measures"]) - set(new["measures"])
        if lost_measures:
            r.append(f"{key}: measures removed {sorted(lost_measures)}")
        widening = {("none", "optional"), ("required", "optional")}
        if old["text"] != new["text"] and (old["text"], new["text"]) not in widening:
            r.append(f"{key}: text policy {old['text']} -> {new['text']}")
        r += _attribute_breaks(key, old["attributes"], new["attributes"])
    elif kind == "relation_type":
        for end in ("from", "to"):
            lost = set(old[end]) - set(new[end])
            if lost:
                r.append(f"{key}: '{end}' lost entity types {sorted(lost)}")
        r += _attribute_breaks(key, old["attributes"], new["attributes"])
    elif kind == "measure":
        if old["unit"] != new["unit"]:
            r.append(f"{key}: unit changed {old['unit']} -> {new['unit']}")
        if old["signed"] and not new["signed"]:
            r.append(f"{key}: signed measure made unsigned")
        if new["min"] is not None and (old["min"] is None or new["min"] > old["min"]):
            r.append(f"{key}: minimum raised")
        if new["max"] is not None and (old["max"] is None or new["max"] < old["max"]):
            r.append(f"{key}: maximum lowered")
    elif kind == "logical_space":
        if new["max_depth"] < old["max_depth"]:
            r.append(f"{key}: max_depth lowered {old['max_depth']} -> {new['max_depth']}")
        if new["closed"] and not old["closed"]:
            r.append(f"{key}: space closed")
        lost = set(old["values"]) - set(new["values"])
        if new["closed"] and lost:
            r.append(f"{key}: closed values removed {sorted(lost)}")
    return r


def diff(old: dict[str, dict], new: dict[str, dict]) -> tuple[list[dict], list[str]]:
    """The changes from `old` to `new`, and the reasons any of them would break stored records."""
    changes, breaks = [], []
    for key in sorted(set(old) | set(new)):
        if key not in new:
            changes.append({"op": "remove", "key": key})
            breaks.append(f"{key}: removed (add a new code instead; the old one stays)")
        elif key not in old:
            changes.append({"op": "add", "key": key})
        elif canonical_json(old[key]) != canonical_json(new[key]):
            changes.append({"op": "change", "key": key})
            breaks += breaking_reasons(key, old[key], new[key])
    return changes, breaks


# --------------------------------------------------------------------------------------------------- references
def check_references(all_defs: dict[str, dict]) -> None:
    """Every code a definition points to must exist somewhere in the registry (after the change)."""
    has = lambda kind, code: f"{kind}:{code}" in all_defs  # noqa: E731
    problems = []
    for key, d in all_defs.items():
        kind = key.split(":", 1)[0]
        if kind == "event_type":
            for role, types in d["roles"].items():
                if not has("role", role):
                    problems.append(f"{key}: unknown role '{role}'")
                problems += [f"{key}: unknown entity type '{t}' in role '{role}'" for t in types if not has("entity_type", t)]
            problems += [f"{key}: unknown measure '{m}'" for m in d["measures"] if not has("measure", m)]
        elif kind == "relation_type":
            problems += [f"{key}: unknown entity type '{t}'" for t in d["from"] + d["to"] if not has("entity_type", t)]
        elif kind == "shared_namespace" and not has("entity_type", d["entity_type"]):
            problems.append(f"{key}: unknown entity type '{d['entity_type']}'")
        elif kind == "source":
            basis = all_defs.get("vocabulary:reliability_basis")
            if basis and d["reliability_basis"] not in basis["values"]:
                problems.append(f"{key}: reliability_basis '{d['reliability_basis']}' is not in the core vocabulary")
        elif kind == "logical_space" and d["closed"] and not d["values"]:
            problems.append(f"{key}: a closed space needs values")
        elif kind == "entity_namespace" and has("shared_namespace", d["namespace"]):
            problems.append(f"{key}: '{d['namespace']}' is a shared namespace owned by core")
    if problems:
        raise ApiError(422, E_SCHEMA_REF, f"{len(problems)} reference(s) point to codes that do not exist.",
                       [{"message": p} for p in problems[:100]])
