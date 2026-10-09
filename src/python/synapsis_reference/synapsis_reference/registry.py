"""RegistrySnapshot: the effective registry compiled into lookup tables for one revision.

The validator never reads MongoDB; it asks a snapshot. A snapshot is immutable, so a request keeps a consistent view
even if a package changes while it runs. RegistryCache swaps in a new snapshot when the stored revision moves."""
from __future__ import annotations

import re
import threading
import time
from dataclasses import dataclass
from typing import Callable


@dataclass(frozen=True)
class AttributeRule:
    """One declared attribute, with its pattern compiled once."""
    type: str
    required: bool = False
    max_length: int | None = None
    max_items: int | None = None
    enum: frozenset | None = None
    pattern: re.Pattern | None = None
    min: float | None = None
    max: float | None = None

    @staticmethod
    def compile(spec: dict) -> "AttributeRule":
        return AttributeRule(
            type=spec["type"], required=bool(spec.get("required", False)), max_length=spec.get("max_length"),
            max_items=spec.get("max_items"), enum=frozenset(spec["enum"]) if "enum" in spec else None,
            pattern=re.compile(spec["pattern"]) if spec.get("pattern") else None, min=spec.get("min"), max=spec.get("max"))


class RegistrySnapshot:
    """Every definition of every package, indexed for the rules. Built from the registry_entries documents."""

    def __init__(self, entries: list[dict], revision: int):
        self.revision = revision
        by_kind: dict[str, dict[str, dict]] = {}
        self.owner: dict[str, str] = {}
        for e in entries:
            kind, code = e["_id"].split(":", 1)
            by_kind.setdefault(kind, {})[code] = e["def"]
            self.owner[e["_id"]] = e["owner"]
        vocab = {name: set(d["values"]) for name, d in by_kind.get("vocabulary", {}).items()}
        self.precisions = vocab.get("precision", set())
        self.known_at_bases = vocab.get("known_at_basis", set())
        self.reliability_bases = vocab.get("reliability_basis", set())
        self.method_by = vocab.get("method_by", set())
        self.countries = vocab.get("country", set())
        self.sources: dict[str, dict] = by_kind.get("source", {})
        self.source_package = {sid: self.owner[f"source:{sid}"] for sid in self.sources}
        self.records_ns = {ns: self.owner[f"records_namespace:{ns}"] for ns in by_kind.get("records_namespace", {})}
        self.entity_ns = {ns: self.owner[f"entity_namespace:{ns}"] for ns in by_kind.get("entity_namespace", {})}
        self.shared_ns = {ns: (d["entity_type"], re.compile(d["key_pattern"]))
                          for ns, d in by_kind.get("shared_namespace", {}).items()}
        self.types = {kind: by_kind.get(f"{kind}_type", {}) for kind in ("entity", "event", "relation", "conclusion")}
        self.roles = set(by_kind.get("role", {}))
        self.measures = by_kind.get("measure", {})
        self.spaces = by_kind.get("logical_space", {})
        self.attributes: dict[tuple[str, str], dict[str, AttributeRule]] = {
            (kind, code): {name: AttributeRule.compile(spec) for name, spec in d.get("attributes", {}).items()}
            for kind in ("entity", "event", "relation") for code, d in self.types[kind].items()}

    def summary(self) -> dict:
        return {"revision": self.revision, "sources": len(self.sources),
                **{f"{k}_types": len(v) for k, v in self.types.items()},
                "roles": len(self.roles), "measures": len(self.measures), "logical_spaces": len(self.spaces)}


class RegistryCache:
    """Hands out the current snapshot. Checks the stored revision at most every `poll_seconds`; `invalidate()`
    forces a reload (the instance that wrote a package calls it, so its own next request already sees the change)."""

    def __init__(self, load: Callable[[], RegistrySnapshot], current_revision: Callable[[], int], poll_seconds: float):
        self._load, self._rev, self._poll = load, current_revision, poll_seconds
        self._snapshot: RegistrySnapshot | None = None
        self._checked = 0.0
        self._lock = threading.Lock()

    def get(self) -> RegistrySnapshot:
        now = time.monotonic()
        with self._lock:
            if self._snapshot is None:
                self._snapshot, self._checked = self._load(), now
            elif now - self._checked >= self._poll:
                self._checked = now
                if self._rev() != self._snapshot.revision:
                    self._snapshot = self._load()
            return self._snapshot

    def invalidate(self) -> None:
        with self._lock:
            self._snapshot = None
