"""The feeder contract.

A feeder is a class with a name, the schema package it speaks, and one generator, records(), that yields SIL records
and Checkpoint markers. Everything else (the schema call, batching, receipts, retries, the cursor, the schedule) is
the runner's job, so a feeder file holds only what is specific to its source: where to fetch, how to map."""
from __future__ import annotations

import json
import logging
import os
import time
from dataclasses import dataclass, field
from datetime import date, datetime
from pathlib import Path
from typing import ClassVar, Iterator

from .config import Settings
from .fetch import Http

FEEDERS: dict[str, type["Feeder"]] = {}


@dataclass
class Checkpoint:
    """Yielded after a unit of work (a month, a file, a page). The runner first sends every pending record, and only
    then saves this cursor, so a crash can repeat work but never skip it."""
    cursor: dict


class MissingKey(RuntimeError):
    pass


@dataclass
class Context:
    name: str
    settings: Settings
    http: Http
    package: dict
    cursor: dict
    since: date | None
    until: date | None
    started_at: datetime
    options: dict = field(default_factory=dict)
    log: logging.Logger = field(default_factory=lambda: logging.getLogger("feeder"))
    memo_dir: Path | None = None
    api: object | None = None          # SynapsisApi; None in a dry run
    _seen: set = field(default_factory=set)

    def source(self, source_id: str) -> dict:
        """The source as the package declares it (reliability, basis, lag): one place for those numbers."""
        for s in self.package.get("sources", []):
            if s["id"] == source_id:
                return s
        raise KeyError(f"source {source_id} is not in package {self.package['package']}")

    def once(self, key: str) -> bool:
        """True the first time `key` is seen in this run; used to send each entity once."""
        if key in self._seen:
            return False
        self._seen.add(key)
        return True

    def _memo_path(self, name: str, survives_reset: bool) -> Path | None:
        if not self.memo_dir:
            return None
        folder = self.memo_dir.parent / "observed" if survives_reset else self.memo_dir
        return folder / f"{name}.json"

    def load_memo(self, name: str, default, survives_reset: bool = False):
        """A JSON value the feeder kept from earlier runs. Ordinary memos (memo/) are cleared by --reset. Use
        survives_reset=True (observed/) only for the feeder's own observations that the source cannot give again,
        such as the day a listing was first seen."""
        path = self._memo_path(name, survives_reset)
        if path and path.exists():
            return json.loads(path.read_text(encoding="utf-8"))
        return default

    def save_memo(self, name: str, value, survives_reset: bool = False) -> None:
        """Save a memo atomically. Call it after the Checkpoint that sent the records it describes, so a crash can
        only make the feeder send something again, never skip it."""
        path = self._memo_path(name, survives_reset)
        if not path:
            return
        path.parent.mkdir(parents=True, exist_ok=True)
        tmp = path.with_suffix(".tmp")
        tmp.write_text(json.dumps(value, ensure_ascii=False, sort_keys=True), encoding="utf-8")
        os.replace(tmp, path)

    def wait_for_records(self, ids: list[str], minutes: float, what: str) -> None:
        """Wait until Synapsis holds every id (records another feeder sends), checking every 30 seconds. Used when a
        feeder's records point at another feeder's records: kev-classes needs the KEV catalog. Skipped in a dry run."""
        if self.api is None or not ids:
            return
        deadline = time.monotonic() + minutes * 60
        while True:
            missing = [i for i in ids if not self.api.record_exists(i)]
            if not missing:
                return
            if time.monotonic() > deadline:
                raise RuntimeError(f"{what} is not in Synapsis after {minutes:.0f} minutes (missing {missing[:3]})")
            self.log.info("waiting for %s (%d of %d sample records missing)", what, len(missing), len(ids))
            time.sleep(30)

    def key(self, name: str) -> str:
        value = self.settings.key(name)
        if not value:
            raise MissingKey(f"{self.name} needs {name} in the environment (.env)")
        return value


class Feeder:
    name: ClassVar[str]
    package: ClassVar[str]
    title: ClassVar[str]
    keys: ClassVar[tuple[str, ...]] = ()
    requires: ClassVar[tuple[str, ...]] = ()     # shared vocabulary packages registered before this feeder's own
    min_interval: ClassVar[float] = 0.0          # seconds between requests to the source

    def __init__(self, ctx: Context):
        self.ctx = ctx

    def records(self) -> Iterator[dict | Checkpoint]:
        raise NotImplementedError


def register(cls: type[Feeder]) -> type[Feeder]:
    FEEDERS[cls.name] = cls
    return cls
