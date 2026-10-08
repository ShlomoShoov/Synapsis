"""Synapsis feeders.

  python -m synapsis_feeders list
  python -m synapsis_feeders schema <feeder>                 register the feeder's schema package and exit
  python -m synapsis_feeders run <feeder> [options]          fetch, send, checkpoint
  python -m synapsis_feeders run-all [--only a,b] [options]  every feeder once, in turn

Options for run and run-all:
  --since YYYY-MM-DD   start of a backfill (default: continue from the cursor, or the feeder's own start)
  --until YYYY-MM-DD   end of a backfill (default: today)
  --every 6h           keep running and repeat on this interval (run only)
  --limit N            stop after N records (smoke tests)
  --dry-run DIR        write the batches to DIR instead of sending them
  --reset              forget the cursor and start from --since or the feeder's default
  --set key=value      a feeder option (for example --set months=2024-01,2024-02); may repeat

Environment: SYNAPSIS_API_URL (default http://localhost:8080), SYNAPSIS_API_KEY, FEEDER_STATE_DIR,
FEEDER_CACHE_DIR, FEEDER_BATCH_SIZE, and each source's key (FIRMS_MAP_KEY, GFW_TOKEN, FRED_API_KEY)."""
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
from .runner import run_all, run_every, run_once


def _date(s: str) -> date:
    return date.fromisoformat(s)


def main(argv: list[str] | None = None) -> int:
    logging.basicConfig(level=os.environ.get("LOG_LEVEL", "INFO"), format="%(asctime)s %(levelname)s %(name)s %(message)s")
    p = argparse.ArgumentParser(prog="synapsis_feeders", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="command", required=True)
    sub.add_parser("list")
    s = sub.add_parser("schema")
    s.add_argument("feeder", choices=sorted(FEEDERS))
    for name in ("run", "run-all"):
        r = sub.add_parser(name)
        if name == "run":
            r.add_argument("feeder", choices=sorted(FEEDERS))
            r.add_argument("--every")
        else:
            r.add_argument("--only")
        r.add_argument("--since", type=_date)
        r.add_argument("--until", type=_date)
        r.add_argument("--limit", type=int)
        r.add_argument("--dry-run", type=Path)
        r.add_argument("--reset", action="store_true")
        r.add_argument("--set", action="append", default=[], metavar="KEY=VALUE")
    args = p.parse_args(argv)
    settings = Settings.from_env()

    if args.command == "list":
        for n in sorted(FEEDERS):
            f = FEEDERS[n]
            keys = f" (needs {', '.join(f.keys)})" if f.keys else ""
            print(f"{n:22s} package {f.package:22s} {f.title}{keys}")
        return 0
    if args.command == "schema":
        f = FEEDERS[args.feeder]
        body = (settings.schemas_dir / "seed" / f"{f.package}.yaml").read_bytes()
        print(json.dumps(SynapsisApi(settings).register_schema(f.package, body), indent=1))
        return 0

    options = dict(kv.split("=", 1) for kv in args.set)
    kw = dict(since=args.since, until=args.until, limit=args.limit, dry_run_dir=args.dry_run, reset=args.reset, options=options)
    if args.command == "run":
        if args.every:
            run_every(args.feeder, args.every, settings, **kw)
            return 0
        try:
            print(json.dumps(run_once(args.feeder, settings, **kw), indent=1, default=str))
        except MissingKey as exc:
            logging.getLogger("feeder").error("%s", exc)
            return 2
        except ApiRefused as exc:
            logging.getLogger("feeder").error("the API refused the request: %s", exc)
            return 3
        return 0
    results = run_all(settings, args.only.split(",") if args.only else None, **kw)
    print(json.dumps(results, indent=1, default=str))
    return 0


if __name__ == "__main__":
    sys.exit(main())
