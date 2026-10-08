"""run_once: one feeder, one run. run_every: the same on a schedule, until stopped.

A run: check the source's keys, register the schema package with the API, pull records from the feeder, send them in
batches, save the cursor at each checkpoint, write a run summary. A refused schema stops the run before any data."""
from __future__ import annotations

import json
import logging
import re
import signal
import threading
import time
from collections import Counter
from datetime import date, datetime, timezone
from pathlib import Path

import yaml

from .api import SynapsisApi
from .base import FEEDERS, Checkpoint, Context, MissingKey
from .batch import BatchSender
from .config import Settings
from .fetch import Http
from .state import FeederState

log = logging.getLogger("feeder.runner")
_STOP = threading.Event()


def parse_every(text: str) -> float:
    m = re.fullmatch(r"(\d+)\s*([smhd])", text.strip())
    if not m:
        raise ValueError(f"interval like 30m, 6h or 7d expected, got {text!r}")
    return int(m.group(1)) * {"s": 1, "m": 60, "h": 3600, "d": 86400}[m.group(2)]


def run_once(name: str, settings: Settings, *, since: date | None = None, until: date | None = None,
             limit: int | None = None, dry_run_dir: Path | None = None, reset: bool = False,
             options: dict | None = None) -> dict:
    cls = FEEDERS[name]
    started = datetime.now(timezone.utc)
    t0 = time.monotonic()
    missing = [k for k in cls.keys if not settings.key(k)]
    if missing:
        raise MissingKey(f"{name} needs {', '.join(missing)} in the environment (.env)")

    state = FeederState(settings.state_dir, name)
    cursor = dict(state.cursor)
    if reset:
        cursor = {}
        if dry_run_dir is None:     # a dry run leaves no trace, not even a reset
            state.reset()
    pkg_path = settings.schemas_dir / "seed" / f"{cls.package}.yaml"
    pkg_bytes = pkg_path.read_bytes()
    package = yaml.safe_load(pkg_bytes)

    api, version = None, None
    if dry_run_dir is None:
        api = SynapsisApi(settings)
        for shared in cls.requires:
            dep = api.register_schema(shared, (settings.schemas_dir / "seed" / f"{shared}.yaml").read_bytes())
            log.info("%s: shared package '%s' %s (version %s)", name, shared, dep["status"], dep["version"])
        reg = api.register_schema(cls.package, pkg_bytes)
        version = reg["version"]
        log.info("%s: schema package '%s' %s (version %s)", name, cls.package, reg["status"], version)

    sender = BatchSender(api, name, f"feeder:{name}", cls.package, version, settings.batch_size, state, dry_run_dir)
    ctx = Context(name=name, settings=settings, package=package, cursor=cursor, since=since, until=until,
                  started_at=started, options=options or {},
                  memo_dir=(dry_run_dir / "memo") if dry_run_dir is not None else state.memo_dir, api=api,
                  http=Http(settings.user_agent, settings.http_timeout, settings.cache_dir / name, cls.min_interval),
                  log=logging.getLogger(f"feeder.{name}"))
    sent = 0
    outcome = "complete"
    for item in cls(ctx).records():
        if isinstance(item, Checkpoint):
            sender.flush()
            if dry_run_dir is None:
                state.save_cursor(item.cursor)
            continue
        sender.add(item)
        sent += 1
        if limit and sent >= limit:
            outcome = f"stopped at --limit {limit}"
            break
        if _STOP.is_set():
            outcome = "stopped by signal"
            break
    sender.flush()
    summary = {"feeder": name, "package": cls.package, "package_version": version, "outcome": outcome,
               "started_at": started.isoformat(), "seconds": round(time.monotonic() - t0, 1), "records_sent": sent,
               "batches": sender.batches, "counts": dict(sender.counts), "rejected_by_code": dict(sender.codes),
               "cursor": state.cursor, "dry_run": dry_run_dir is not None}
    if dry_run_dir is None:
        state.log_run(summary)
    else:                           # a dry run leaves its summary next to its batches, not in the feeder's state
        dry_run_dir.mkdir(parents=True, exist_ok=True)
        (dry_run_dir / "run.json").write_text(json.dumps(summary, indent=1, default=str))
    log.info("%s: %s in %.0fs - %s", name, outcome, summary["seconds"], dict(sender.counts))
    return summary


def run_every(name: str, every: str, settings: Settings, **kw) -> None:
    """Run now, then every `every`, until SIGTERM or SIGINT.

    A failed run (the API still starting, the source down for the night) is retried after 1 minute, then 2, 4 and so
    on up to the period, so a feeder that starts before the API does not wait a whole period. A missing key leaves the
    feeder idle with one clear log line instead of restarting in a loop; set the key in .env and recreate the service."""
    period = parse_every(every)
    for sig in (signal.SIGTERM, signal.SIGINT):
        signal.signal(sig, lambda *_: _STOP.set())
    retry = 60.0
    while not _STOP.is_set():
        wait = period
        try:
            run_once(name, settings, **kw)
            kw["since"] = None      # later runs continue from the cursor
            retry = 60.0
        except MissingKey as exc:
            log.error("%s; the feeder is idle until it is restarted with the key", exc)
            _STOP.wait()
            return
        except Exception:  # noqa: BLE001 - a scheduled feeder must survive a bad night at the source
            wait = min(retry, period)
            retry = min(retry * 2, period)
            log.exception("%s: run failed; next attempt in %.0f s", name, wait)
        kw["reset"] = False         # the reset happened at the start of the first run; never repeat it
        _STOP.wait(wait)


def run_all(settings: Settings, names: list[str] | None = None, **kw) -> list[dict]:
    """Every feeder once, in turn: the full load of every source we hold."""
    out = []
    for name in names or sorted(FEEDERS):
        try:
            out.append(run_once(name, settings, **kw))
        except Exception as exc:  # noqa: BLE001
            log.exception("%s failed", name)
            out.append({"feeder": name, "outcome": f"failed: {exc}"})
    return out


def summarise(results: list[dict]) -> dict:
    total = Counter()
    for r in results:
        total.update(r.get("counts", {}))
    return dict(total)
