"""The runner: how a feeder runs.

run_once   One run: register the schema packages, pull records from the feeder, send them in batches, save the cursor
           at each checkpoint and write a run summary. A refused schema stops the run before any data is sent.
serve      The feeder as a long-running service, which is what Docker Compose starts. At start-up it registers the
           feeder's schema packages (retrying until the API accepts them); then it runs, waits for the next trigger,
           and runs again, until the container stops.
run_all    Every feeder once, in turn.

A trigger is either the timer (serve --every 6h) or a manual nudge: the signal SIGUSR1, which
`docker kill --signal SIGUSR1 <container>` sends. SIGTERM and SIGINT stop the service cleanly after the current
batch."""
from __future__ import annotations

import json
import logging
import re
import signal
import threading
import time
from datetime import date, datetime, timezone
from pathlib import Path

import yaml

from .api import SynapsisApi
from .base import FEEDERS, Checkpoint, Context, Feeder, MissingKey
from .batch import BatchSender
from .config import Settings
from .fetch import Http
from .state import FeederState

log = logging.getLogger("feeder.runner")

_STOP = threading.Event()     # set by SIGTERM or SIGINT: finish the current batch, then exit
_WAKE = threading.Event()     # set by every signal the service listens to: stop waiting and look again

FIRST_RETRY_SECONDS = 60.0    # after a failure, wait 1 minute, then 2, 4 and so on
LONGEST_RETRY_SECONDS = 3600.0


def parse_every(text: str) -> float:
    """Turn an interval such as "30m", "6h" or "7d" into seconds."""
    match = re.fullmatch(r"(\d+)\s*([smhd])", text.strip())
    if not match:
        raise ValueError(f"interval like 30m, 6h or 7d expected, got {text!r}")
    seconds_per_unit = {"s": 1, "m": 60, "h": 3600, "d": 86400}
    return int(match.group(1)) * seconds_per_unit[match.group(2)]


# ------------------------------------------------------------------------------------------------ schema packages
def register_packages(name: str, settings: Settings, api: SynapsisApi) -> int:
    """Register the feeder's schema packages with the API: first the shared ones it requires, then its own.

    The API answers an identical package with "unchanged", so this is safe at every start-up and before every run.
    Returns the version of the feeder's own package."""
    feeder_class = FEEDERS[name]
    for shared in feeder_class.requires:
        _register_one(name, shared, settings, api)
    return _register_one(name, feeder_class.package, settings, api)


def _register_one(name: str, package: str, settings: Settings, api: SynapsisApi) -> int:
    """Send one package file to PUT /schemas/{package}, log the answer and return the package's version."""
    body = settings.package_file(package).read_bytes()
    answer = api.register_schema(package, body)
    log.info("%s: schema package '%s' %s (version %s)", name, package, answer["status"], answer["version"])
    return answer["version"]


# ------------------------------------------------------------------------------------------------ one run
def run_once(name: str, settings: Settings, *, since: date | None = None, until: date | None = None,
             limit: int | None = None, dry_run_dir: Path | None = None, reset: bool = False,
             options: dict | None = None) -> dict:
    """One run of one feeder: check its keys, register its packages, send its records, save the cursor.

    With dry_run_dir the batches are written to that folder instead of being sent, and the feeder's state is left
    untouched. Returns the run summary, which is also appended to the feeder's runs.jsonl."""
    feeder_class = FEEDERS[name]
    started = datetime.now(timezone.utc)
    clock = time.monotonic()
    _check_keys(name, feeder_class, settings)

    state = FeederState(settings.state_dir, name)
    cursor = _starting_cursor(state, reset, dry_run_dir)
    api = None
    version = None
    if dry_run_dir is None:
        api = SynapsisApi(settings)
        version = register_packages(name, settings, api)

    sender = BatchSender(api, name, f"feeder:{name}", settings.batch_size, state, dry_run_dir)
    ctx = _context(name, feeder_class, settings, state, cursor, since, until, started, options, dry_run_dir, api)
    sent, outcome = _send_records(feeder_class(ctx), sender, state, limit, dry_run_dir)

    summary = {"feeder": name, "package": feeder_class.package, "package_version": version, "outcome": outcome,
               "started_at": started.isoformat(), "seconds": round(time.monotonic() - clock, 1),
               "records_sent": sent, "batches": sender.batches, "counts": dict(sender.counts),
               "rejected_by_code": dict(sender.codes), "cursor": state.cursor, "dry_run": dry_run_dir is not None}
    _save_summary(summary, state, dry_run_dir)
    log.info("%s: %s in %.0fs - %s", name, outcome, summary["seconds"], dict(sender.counts))
    return summary


def _check_keys(name: str, feeder_class: type[Feeder], settings: Settings) -> None:
    """Raise MissingKey when an environment variable the source needs is empty."""
    missing = []
    for key_name in feeder_class.keys:
        if not settings.key(key_name):
            missing.append(key_name)
    if missing:
        raise MissingKey(f"{name} needs {', '.join(missing)} in the environment "
                         f"(in Docker: the same name with FEEDERS_ in front, in deploy/feeders/.env)")


def _starting_cursor(state: FeederState, reset: bool, dry_run_dir: Path | None) -> dict:
    """Where this run starts: the saved cursor, or nothing after --reset. A dry run never changes the state."""
    if not reset:
        return dict(state.cursor)
    if dry_run_dir is None:
        state.reset()
    return {}


def _context(name: str, feeder_class: type[Feeder], settings: Settings, state: FeederState, cursor: dict,
             since: date | None, until: date | None, started: datetime, options: dict | None,
             dry_run_dir: Path | None, api: SynapsisApi | None) -> Context:
    """Build the Context the feeder reads: its package, cursor, date range, options, HTTP client and memo folder."""
    package = yaml.safe_load(settings.package_file(feeder_class.package).read_bytes())
    memo_dir = state.memo_dir
    if dry_run_dir is not None:
        memo_dir = dry_run_dir / "memo"
    http = Http(settings.user_agent, settings.http_timeout, settings.cache_dir / name, feeder_class.min_interval)
    return Context(name=name, settings=settings, package=package, cursor=cursor, since=since, until=until,
                   started_at=started, options=options or {}, memo_dir=memo_dir, api=api, http=http,
                   log=logging.getLogger(f"feeder.{name}"))


def _send_records(feeder: Feeder, sender: BatchSender, state: FeederState, limit: int | None,
                  dry_run_dir: Path | None) -> tuple[int, str]:
    """Pull records from the feeder and hand them to the batch sender. Returns (records sent, outcome).

    At each Checkpoint everything pending is sent first, and only then is the cursor saved, so a crash can repeat
    work but never skip it."""
    sent = 0
    outcome = "complete"
    for item in feeder.records():
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
    return sent, outcome


def _save_summary(summary: dict, state: FeederState, dry_run_dir: Path | None) -> None:
    """Append the summary to runs.jsonl, or, for a dry run, write it next to the batches."""
    if dry_run_dir is None:
        state.log_run(summary)
        return
    dry_run_dir.mkdir(parents=True, exist_ok=True)
    (dry_run_dir / "run.json").write_text(json.dumps(summary, indent=1, default=str))


# ------------------------------------------------------------------------------------------------ the service
def serve(name: str, settings: Settings, every: str | None = None, **run_options) -> None:
    """Run the feeder as a long-running service, until SIGTERM or SIGINT.

    1. Set-up: register the schema packages, retrying until the API accepts them. The registry then knows this
       feeder's vocabulary from the start, even before the first run and even when the source key is missing.
    2. Run: send what is new at the source (run_once continues from the saved cursor).
    3. Wait for a trigger: the timer when `every` is given, or SIGUSR1 at any time. Then run again.

    A failed run is retried after 1 minute, then 2, 4 and so on (never later than the timer). --since is kept until a
    run succeeds; --reset applies to the first run only. A missing key leaves the feeder idle with one clear log line:
    set the key in deploy/feeders/.env and recreate the service."""
    _listen_for_signals()
    period = None
    if every:
        period = parse_every(every)
    _setup(name, settings)
    retry = FIRST_RETRY_SECONDS
    while not _STOP.is_set():
        _WAKE.clear()
        try:
            run_once(name, settings, **run_options)
        except MissingKey as exc:
            log.error("%s; the feeder stays idle until it is recreated with the key", exc)
            _STOP.wait()
            return
        except Exception:  # noqa: BLE001 - a long-running feeder must survive a bad night at the source
            wait = _retry_wait(retry, period)
            retry = min(retry * 2, LONGEST_RETRY_SECONDS)
            run_options["reset"] = False
            log.exception("%s: run failed; next attempt in %.0f s", name, wait)
            _wait_for_trigger(name, wait)
            continue
        retry = FIRST_RETRY_SECONDS
        run_options["since"] = None        # later runs continue from the cursor
        run_options["reset"] = False       # a reset happens once, at the first run
        _wait_for_trigger(name, period)


def _setup(name: str, settings: Settings) -> None:
    """Register the feeder's schema packages once at start-up, retrying until the API accepts them.

    The API may still be starting, or two feeders may register a shared package at the same moment (the second one
    is told to try again); both pass on a later attempt."""
    api = SynapsisApi(settings)
    wait = FIRST_RETRY_SECONDS
    while not _STOP.is_set():
        try:
            register_packages(name, settings, api)
            log.info("%s: set-up done, schema packages registered", name)
            return
        except Exception:  # noqa: BLE001 - keep trying until the API is there
            log.exception("%s: could not register the schema packages; next attempt in %.0f s", name, wait)
            _STOP.wait(wait)
            wait = min(wait * 2, LONGEST_RETRY_SECONDS)


def _retry_wait(retry: float, period: float | None) -> float:
    """How long to wait after a failed run: the retry delay, but never longer than the timer."""
    if period is None:
        return retry
    return min(retry, period)


def _wait_for_trigger(name: str, seconds: float | None) -> None:
    """Wait until the timer runs out (`seconds`; None means no timer), a manual trigger or a stop signal."""
    if seconds is None:
        log.info("%s: waiting for a manual trigger", name)
    else:
        log.info("%s: next run in %.0f s, or earlier at a manual trigger", name, seconds)
    woken = _WAKE.wait(seconds)
    if woken and not _STOP.is_set():
        log.info("%s: manual trigger received, running now", name)


def _listen_for_signals() -> None:
    """SIGTERM and SIGINT (docker stop, Ctrl+C) stop the service; SIGUSR1 is a manual trigger: run now."""
    signal.signal(signal.SIGTERM, _on_stop)
    signal.signal(signal.SIGINT, _on_stop)
    signal.signal(signal.SIGUSR1, _on_trigger)


def _on_stop(signum, frame) -> None:
    """Signal handler for SIGTERM and SIGINT: finish the current batch, then exit."""
    _STOP.set()
    _WAKE.set()


def _on_trigger(signum, frame) -> None:
    """Signal handler for SIGUSR1: stop waiting and run now."""
    _WAKE.set()


# ------------------------------------------------------------------------------------------------ every feeder
def run_all(settings: Settings, names: list[str] | None = None, **run_options) -> list[dict]:
    """Every feeder once, in turn: the full load of every source we hold. A failing feeder does not stop the rest."""
    if not names:
        names = sorted(FEEDERS)
    results = []
    for name in names:
        try:
            results.append(run_once(name, settings, **run_options))
        except Exception as exc:  # noqa: BLE001
            log.exception("%s failed", name)
            results.append({"feeder": name, "outcome": f"failed: {exc}"})
    return results

