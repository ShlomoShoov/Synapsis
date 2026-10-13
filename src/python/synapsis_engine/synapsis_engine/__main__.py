"""python -m synapsis_engine backfill [--source S] [--dry-run] [--force] | verify | coverage"""
import argparse
import collections
import hashlib
import json
import sys
import time

from . import backfill, config, findings, workspace
from .io import api_client, mongo_reader, pipeline_log

MAX_WITHOUT_FORCE = 200_000


def load_workspace():
    t0 = time.time()
    ws = workspace.Workspace()
    n = ws.load(mongo_reader.events(), mongo_reader.entity_names())
    print(f"workspace: {n:,} event states read, {sum(ws.counts().values()):,} events kept ({time.time() - t0:.0f} s)",
          flush=True)
    return ws


def compute(ws, decl, only=None):
    shapes = config.find_shapes(ws, decl)
    out, cards, notes = backfill.run(ws, decl, shapes, only)
    ids = [r["id"] for r, _ in out]
    dup = [i for i, c in collections.Counter(ids).items() if c > 1]
    if dup:
        raise SystemExit(f"duplicate conclusion ids: {dup[:5]}")
    return shapes, out, cards, notes


def records_of(out):
    recs = sorted((r for r, _ in out), key=lambda r: r["id"])
    return recs + findings.cell_entities(recs)


def digest(records):
    h = hashlib.sha256()
    for r in sorted(records, key=lambda r: r["id"]):
        h.update(json.dumps(r, sort_keys=True, separators=(",", ":")).encode())
    return h.hexdigest()


def volume(out):
    c = collections.Counter((m["source"], r["type"]) for r, m in out)
    return sorted(c.items())


def print_volume(out, cells):
    print("conclusions per source and type:")
    for (s, t), n in volume(out):
        print(f"  {s:32s} {t:20s} {n:>8,}")
    print(f"  {'MapCell entities':53s} {cells:>8,}")
    print(f"  {'total':53s} {len(out) + cells:>8,}", flush=True)


def save(name, obj):
    p = config.data_dir() / name
    p.write_text(json.dumps(obj, indent=1, sort_keys=True, default=str))
    return p


def cmd_backfill(a):
    decl = config.load()
    ws = load_workspace()
    shapes, out, cards, notes = compute(ws, decl, a.source)
    recs = records_of(out)
    print_volume(out, len(recs) - len(out))
    save("shapes.json", shapes)
    save("cards.json", cards)
    with open(config.data_dir() / "conclusions.jsonl", "w") as f:
        for r in recs:
            f.write(json.dumps(r, sort_keys=True) + "\n")
    print(f"sha256 over the sorted records: {digest(recs)}")
    per_source = collections.Counter(m["source"] for _, m in out)
    if len(recs) > MAX_WITHOUT_FORCE and not a.force:
        print(f"stopped: {len(recs):,} records > {MAX_WITHOUT_FORCE:,}; add --force to send them")
        for s, n in sorted(per_source.items()):
            pipeline_log.run("backfill", s, n, 0, message=f"{n:,} conclusions computed, not sent (over the limit without --force)")
        return 2
    if a.dry_run:
        print("dry run: nothing sent")
        for s, n in sorted(per_source.items()):
            pipeline_log.run("backfill", s, n, 0, message=f"dry run: {n:,} conclusions computed, nothing sent")
        return 0
    t0 = time.time()
    ans = api_client.post(recs)
    print(f"POST /ingest: {json.dumps({k: v for k, v in ans.items() if k != 'rejections'})} ({time.time() - t0:.0f} s)")
    for r in ans["rejections"]:
        print("  rejected:", r)
    source_of = {r["id"]: m["source"] for r, m in out}
    refused = collections.Counter(source_of.get(r.get("id"), "") for r in ans["rejections"])
    for s, n in sorted(per_source.items()):
        bad = refused.get(s, 0)  # the API lists at most 5 refusals per batch: a floor
        pipeline_log.run("backfill", s, n, n - bad, bad, f"{n:,} conclusions sent ({ans['new']:,} new, "
                         f"{ans['unchanged']:,} unchanged, {ans['rejected']:,} refused in the whole run)")
    return 1 if ans["rejected"] else 0


def cmd_coverage(a):
    from . import verify
    decl = config.load()
    ws = load_workspace()
    shapes, out, cards, notes = compute(ws, decl)
    print("\n".join(verify.coverage(shapes, cards, notes, out)))
    return 0


def main(argv=None):
    p = argparse.ArgumentParser(prog="synapsis_engine")
    sub = p.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("backfill")
    b.add_argument("--source")
    b.add_argument("--dry-run", action="store_true")
    b.add_argument("--force", action="store_true")
    sub.add_parser("verify")
    sub.add_parser("coverage")
    a = p.parse_args(argv)
    if a.cmd == "verify":
        from . import verify
        return verify.main()
    return {"backfill": cmd_backfill, "coverage": cmd_coverage}[a.cmd](a)


if __name__ == "__main__":
    sys.exit(main())
