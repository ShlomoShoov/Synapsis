"""BatchSender: groups records into batches of at most FEEDER_BATCH_SIZE (5,000), sends them, reads the receipts.

Batch ids are derived from content (feeder name + hash of the records), so the same records always travel under the
same id; the API answers records it already holds with "unchanged"."""
from __future__ import annotations

import hashlib
import json
import logging
from collections import Counter
from pathlib import Path

from .api import ApiRefused, SynapsisApi
from .state import FeederState

log = logging.getLogger("feeder.batch")


class BatchSender:
    def __init__(self, api: SynapsisApi | None, feeder: str, producer: str, batch_size: int, state: FeederState,
                 dry_run_dir: Path | None = None):
        self.api, self.feeder, self.producer = api, feeder, producer
        self.batch_size, self.state, self.dry_run_dir = batch_size, state, dry_run_dir
        self.buffer: list[dict] = []
        self.counts: Counter = Counter()
        self.codes: Counter = Counter()
        self.batches = 0

    def add(self, record: dict) -> None:
        self.buffer.append(record)
        if len(self.buffer) >= self.batch_size:
            self.flush()

    def flush(self) -> None:
        while self.buffer:
            chunk, self.buffer = self.buffer[: self.batch_size], self.buffer[self.batch_size:]
            self._send(chunk)

    def _send(self, records: list[dict]) -> None:
        digest = hashlib.sha256(json.dumps(records, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
                                .encode("utf-8")).hexdigest()[:24]
        bundle = {"sil": "1.2", "batch_id": f"{self.feeder}-{digest}", "producer": self.producer, "records": records}
        self.batches += 1
        if self.dry_run_dir is not None:
            self.dry_run_dir.mkdir(parents=True, exist_ok=True)
            (self.dry_run_dir / f"{bundle['batch_id']}.json").write_text(json.dumps(bundle, indent=1, ensure_ascii=False))
            self.counts["written_to_disk"] += len(records)
            return
        try:
            receipt = self.api.ingest(bundle)
        except ApiRefused as exc:
            if exc.status == 413 and len(records) > 1:      # too large for this API: halve and resend
                half = len(records) // 2
                self._send(records[:half])
                self._send(records[half:])
                return
            raise
        for k, v in receipt["counts"].items():
            self.counts[k] += v
        for r in receipt.get("rejected", []):
            self.codes[r["code"]] += 1
        self.state.log_rejections(bundle["batch_id"], receipt.get("rejected", []))
        log.info("%s %s: %s", self.feeder, bundle["batch_id"], receipt["status"])
        log.info("   new %d, versioned %d, unchanged %d, rejected %d", receipt["counts"]["new"],
                 receipt["counts"]["versioned"], receipt["counts"]["unchanged"], receipt["counts"]["rejected"])
