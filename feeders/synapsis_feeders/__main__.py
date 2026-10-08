"""Synapsis feeders.

  python -m synapsis_feeders list
  python -m synapsis_feeders schema <feeder>                  register the feeder's schema packages and exit
  python -m synapsis_feeders run <feeder> [options]           one run: fetch, send, checkpoint, exit
  python -m synapsis_feeders serve <feeder> [--every 6h]      the long-running service Docker Compose starts
  python -m synapsis_feeders run-all [--only a,b] [options]   every feeder once, in turn

serve registers the schema packages at start-up, runs, then waits for the next trigger and runs again: the timer
(--every) or a manual trigger (the signal SIGUSR1: docker kill --signal SIGUSR1 <container>).

Options for run, serve and run-all:
  --since YYYY-MM-DD   start of a backfill (default: continue from the cursor, or the feeder's own start)
  --until YYYY-MM-DD   end of a backfill (default: today)
  --limit N            stop after N records (smoke tests)
  --reset              forget the cursor and start from --since or the feeder's default
  --set key=value      a feeder option (for example --set months=2024-01,2024-02); may repeat
Only for run and run-all:
  --dry-run DIR        write the batches to DIR instead of sending them

Environment: SYNAPSIS_API_URL (default http://localhost:8080), SYNAPSIS_API_KEY, FEEDER_STATE_DIR,
FEEDER_CACHE_DIR, FEEDER_BATCH_SIZE, FEEDER_PACKAGES_DIR, and each source's key (FIRMS_MAP_KEY, GFW_TOKEN,
FRED_API_KEY)."""
from __future__ import annotations

import argparse
import json
import logging
import os
import sys
from datetime import date
from pathlib import Path

from . import sources  # noqa: F401  (registers every feeder)
from .api import ApiRefused, SynapsisApi
from .base import FEEDERS, MissingKey
from .config import Settings
from .runner import register_packages, run_all, run_once, serve

log = logging.getLogger("feeder")


def _date(text: str) -> date:
    """Read a YYYY-MM-DD command-line value."""
    return date.fromisoformat(text)


def _build_parser() -> argparse.ArgumentParser:
    """The command-line grammar: list, schema, run, serve and run-all, with their options."""
    parser = argparse.ArgumentParser(prog="synapsis_feeders", description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("list")
    schema = commands.add_parser("schema")
    schema.add_argument("feeder", choices=sorted(FEEDERS))
    run = commands.add_parser("run")
    run.add_argument("feeder", choices=sorted(FEEDERS))
    _add_run_options(run, dry_run=True)
    service = commands.add_parser("serve")
    service.add_argument("feeder", choices=sorted(FEEDERS))
    service.add_argument("--every", help="run again on this interval, for example 30m, 6h or 7d")
    _add_run_options(service, dry_run=False)
    everything = commands.add_parser("run-all")
    everything.add_argument("--only", help="a comma-separated list of feeders")
    _add_run_options(everything, dry_run=True)
    return parser


def _add_run_options(command: argparse.ArgumentParser, dry_run: bool) -> None:
    """The options that decide what a run sends (shared by run, serve and run-all)."""
    command.add_argument("--since", type=_date)
    command.add_argument("--until", type=_date)
    command.add_argument("--limit", type=int)
    command.add_argument("--reset", action="store_true")
    command.add_argument("--set", action="append", default=[], metavar="KEY=VALUE")
    if dry_run:
        command.add_argument("--dry-run", type=Path)


def _run_options(args: argparse.Namespace) -> dict:
    """The parsed options as keyword arguments for run_once."""
    options = {}
    for pair in args.set:
        key, value = pair.split("=", 1)
        options[key] = value
    return {"since": args.since, "until": args.until, "limit": args.limit, "reset": args.reset,
            "options": options, "dry_run_dir": getattr(args, "dry_run", None)}


def _list() -> int:
    """Print every feeder with its package, title and the keys it needs."""
    for name in sorted(FEEDERS):
        feeder_class = FEEDERS[name]
        keys = ""
        if feeder_class.keys:
            keys = f" (needs {', '.join(feeder_class.keys)})"
        print(f"{name:22s} package {feeder_class.package:22s} {feeder_class.title}{keys}")
    return 0


def _schema(name: str, settings: Settings) -> int:
    """Register the feeder's schema packages (shared ones first) and print its own package's version."""
    version = register_packages(name, settings, SynapsisApi(settings))
    print(json.dumps({"feeder": name, "package": FEEDERS[name].package, "version": version}, indent=1))
    return 0


def _run(name: str, settings: Settings, run_options: dict) -> int:
    """One run of one feeder. Exit code 2 when a key is missing, 3 when the API refused the request."""
    try:
        print(json.dumps(run_once(name, settings, **run_options), indent=1, default=str))
    except MissingKey as exc:
        log.error("%s", exc)
        return 2
    except ApiRefused as exc:
        log.error("the API refused the request: %s", exc)
        return 3
    return 0


def _serve(name: str, settings: Settings, every: str | None, run_options: dict) -> int:
    """Run the feeder as a long-running service until it is stopped."""
    run_options.pop("dry_run_dir")
    serve(name, settings, every, **run_options)
    return 0


def _run_all(only: str | None, settings: Settings, run_options: dict) -> int:
    """Every feeder (or the --only list) once, in turn, and print each run's summary."""
    names = None
    if only:
        names = only.split(",")
    print(json.dumps(run_all(settings, names, **run_options), indent=1, default=str))
    return 0


def main(argv: list[str] | None = None) -> int:
    """Parse the command line and run the chosen command. Returns the process exit code."""
    logging.basicConfig(level=os.environ.get("LOG_LEVEL", "INFO"),
                        format="%(asctime)s %(levelname)s %(name)s %(message)s")
    args = _build_parser().parse_args(argv)
    if args.command == "list":
        return _list()
    settings = Settings.from_env()
    if args.command == "schema":
        return _schema(args.feeder, settings)
    if args.command == "run":
        return _run(args.feeder, settings, _run_options(args))
    if args.command == "serve":
        return _serve(args.feeder, settings, args.every, _run_options(args))
    return _run_all(args.only, settings, _run_options(args))


if __name__ == "__main__":
    sys.exit(main())
