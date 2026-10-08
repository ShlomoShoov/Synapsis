"""Conformance suite: the same records must get the same answer from every Synapsis API (the Python stand-in and the
C# API).

  python tests/conformance/run.py --api http://localhost:8080 [--only 03-time] [--verbose]

Each case in cases/*.json is sent as one bundle to POST /v1/ingest?dry_run=true, so nothing is written. A record case
passes when the receipt rejects exactly the listed records with the listed codes (and paths, where a case gives one)
and accepts every other record. A request case passes when the API answers with the listed status and code.

The cases use the feeders' schema packages (feeders/packages). The API seeds only the core package itself, so the
runner registers the others first, the way a feeder does at start-up (an identical package is answered "unchanged").
Exit code 0 when every case passes, 1 otherwise. Needs only the Python standard library."""
from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

CASES = Path(__file__).resolve().parent / "cases"
PACKAGES = Path(__file__).resolve().parents[2] / "feeders" / "packages"
TRY_AGAIN = {"E-SCHEMA-REF", "E-SCHEMA-VERSION"}    # a package it needs is not there yet, or another writer was first


def send(api: str, method: str, path: str, body: bytes, headers: dict, key: str | None) -> tuple[int, dict]:
    """Send one HTTP request to the API and return (status, JSON body); an error status is returned, not raised."""
    if key:
        headers = {**headers, "Authorization": f"Bearer {key}"}
    req = urllib.request.Request(f"{api.rstrip('/')}{path}", data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return r.status, json.loads(r.read())
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read())
        except ValueError:
            return e.code, {}


def post(api: str, body: bytes, headers: dict, key: str | None) -> tuple[int, dict]:
    """POST one bundle to /v1/ingest?dry_run=true, so nothing is written."""
    return send(api, "POST", "/v1/ingest?dry_run=true", body, {"Content-Type": "application/json", **headers}, key)


def register_packages(api: str, key: str | None) -> None:
    """Register every feeder package the cases rely on. A package that is refused only because a package it needs
    is not registered yet is tried again after the others, up to three rounds."""
    pending = sorted(PACKAGES.glob("*.yaml"))
    for _ in range(3):
        retry = []
        for path in pending:
            status, body = send(api, "PUT", f"/v1/schemas/{path.stem}", path.read_bytes(),
                                {"Content-Type": "application/yaml"}, key)
            if status in (200, 201):
                continue
            if body.get("code") in TRY_AGAIN:
                retry.append(path)
                continue
            raise SystemExit(f"package {path.stem} was refused: HTTP {status} {json.dumps(body)[:500]}")
        if not retry:
            return
        pending = retry
    raise SystemExit(f"packages still refused after three rounds: {[p.stem for p in pending]}")


def bundle_for(name: str, records: list) -> dict:
    digest = hashlib.sha256(json.dumps(records, sort_keys=True).encode()).hexdigest()[:20]
    return {"sil": "1.2", "batch_id": f"conformance-{digest}", "producer": "conformance-suite", "records": records}


def check_records(case: dict, receipt: dict) -> list[str]:
    problems = []
    got = {r["index"]: r for r in receipt.get("rejected", [])}
    want = {r["index"]: r for r in case["rejected"]}
    for i, w in want.items():
        g = got.get(i)
        if g is None:
            problems.append(f"record {i}: expected {w['code']}, but it was accepted")
        elif g["code"] != w["code"]:
            problems.append(f"record {i}: expected {w['code']}, got {g['code']} ({g.get('path')}: {g.get('message')})")
        elif "path" in w and g.get("path") != w["path"]:
            problems.append(f"record {i}: {w['code']} expected at '{w['path']}', reported at '{g.get('path')}'")
    for i, g in got.items():
        if i not in want:
            problems.append(f"record {i}: expected to be accepted, got {g['code']} ({g.get('path')}: {g.get('message')})")
    counts = receipt.get("counts", {})
    if counts.get("duplicates", 0) != case.get("duplicates", 0):
        problems.append(f"expected {case.get('duplicates', 0)} duplicates, got {counts.get('duplicates')}")
    return problems


def run_case(api: str, case: dict, key: str | None) -> list[str]:
    if "records" in case:
        status, body = post(api, json.dumps(bundle_for(case["name"], case["records"])).encode(), {}, key)
        if status != 200:
            return [f"HTTP {status}: {json.dumps(body)[:300]}"]
        if not body.get("dry_run"):
            return ["the API ignored dry_run=true (the receipt has no dry_run flag)"]
        return check_records(case, body)
    # request-level cases
    headers = {}
    if "raw" in case:
        raw = case["raw"].encode()
        if case.get("gzip_header_only"):
            headers["Content-Encoding"] = "gzip"
    else:
        records = case.get("bundle", {}).get("records")
        if "repeat" in case:
            base = {"kind": "event", "id": "evt:usgs:conformance-many", "type": "GEO.EARTHQUAKE", "source": "src:usgs:comcat",
                    "reliability": 95, "reliability_basis": "measured", "time": {"start": "2024-01-01T00:00:00Z", "precision": "day"},
                    "place": {"point": [0, 0], "uncertainty_km": 5}, "known_at": "2024-01-02T00:00:00Z",
                    "known_at_basis": "published"}
            records = [dict(base, id=f"evt:usgs:conformance-many-{i}") for i in range(case["repeat"])]
        bundle = bundle_for(case["name"], records if records is not None else [{"kind": "event"}])
        bundle.update({k: v for k, v in case.get("bundle", {}).items() if k != "records"})
        if records is not None:
            bundle["records"] = records
        raw = gzip.compress(json.dumps(bundle).encode())
        headers["Content-Encoding"] = "gzip"
    status, body = post(api, raw, headers, key)
    problems = []
    if status != case["status"]:
        problems.append(f"expected HTTP {case['status']}, got {status}")
    if body.get("code") != case["code"]:
        problems.append(f"expected code {case['code']}, got {body.get('code')}: {body.get('message')}")
    return problems


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--api", default="http://localhost:8080")
    p.add_argument("--key", help="API key, when the API requires one")
    p.add_argument("--only", help="run one group, for example 03-time")
    p.add_argument("--verbose", action="store_true", help="print passing cases too")
    args = p.parse_args(argv)
    register_packages(args.api, args.key)
    total = failed = 0
    for path in sorted(CASES.glob("*.json")):
        if args.only and not path.stem.startswith(args.only):
            continue
        group = json.loads(path.read_text(encoding="utf-8"))
        print(f"{path.stem}: {group['title']}")
        for case in group["cases"]:
            total += 1
            problems = run_case(args.api, case, args.key)
            if problems:
                failed += 1
                print(f"  FAIL  {case['name']}")
                for line in problems:
                    print(f"        {line}")
            elif args.verbose:
                print(f"  ok    {case['name']}")
    print(f"\n{total - failed} of {total} cases pass against {args.api}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
