"""POST /ingest: the engine's conclusions and map cells, in batches of at most 5,000 records."""
import hashlib

import requests

from .. import config

MAX_BATCH = 5000


def batch_id(records):
    ids = sorted(r["id"] for r in records)
    return "engine-" + hashlib.sha256("\n".join(ids).encode()).hexdigest()[:24]


def post(records, url=None, size=MAX_BATCH):
    """Send the records in id order; returns the summed answer (received, new, changed, unchanged, ...)."""
    url = (url or config.api_url()) + "/ingest"
    records = sorted(records, key=lambda r: r["id"])
    total = {"batches": 0, "received": 0, "new": 0, "changed": 0, "unchanged": 0, "duplicates": 0, "rejected": 0,
             "rejections": []}
    with requests.Session() as s:
        for i in range(0, len(records), size):
            chunk = records[i:i + size]
            r = s.post(url, json={"batch_id": batch_id(chunk), "producer": "engine", "records": chunk}, timeout=600)
            r.raise_for_status()
            a = r.json()
            total["batches"] += 1
            for k in ("received", "new", "changed", "unchanged", "duplicates", "rejected"):
                total[k] += int(a.get(k, 0))
            total["rejections"] += (a.get("rejections") or [])[:5]
    return total
