"""IngestService: the one door. A batch of up to 5,000 records goes through three steps:

1. Rules: SilValidator checks every record against the registry snapshot of this moment.
2. Versions: a record identical to its latest stored version is a no-op; a changed one becomes version n + 1, and its
   known_at is never earlier than the previous version's.
3. Write: one MongoDB transaction stores the new documents, the receipt and an outbox entry.

A batch sent again is simply processed again: every record it holds is found unchanged and nothing is written."""
from __future__ import annotations

import gzip
import json
import time
from datetime import datetime, timezone

from pymongo.database import Database
from pymongo.errors import BulkWriteError, PyMongoError

from . import errors as E
from .errors import ApiError
from .lattice import lattice_keys
from .registry import RegistryCache
from .settings import Settings
from .store import COLLECTION, MongoRecordStore, content_digest, doc_id
from .validator import SilValidator, parse_instant
from .versions import plan


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def iso(t: datetime) -> str:
    return t.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _no_constants(name: str):
    raise ValueError(f"{name} is not a number SIL accepts")


class IngestService:
    def __init__(self, db: Database, store: MongoRecordStore, validator: SilValidator, registry: RegistryCache,
                 settings: Settings):
        self.db, self.store, self.validator, self.registry, self.settings = db, store, validator, registry, settings

    # ------------------------------------------------------------------------------------------------ parsing
    def parse(self, body: bytes, content_encoding: str | None) -> dict:
        if content_encoding and "gzip" in content_encoding.lower():
            try:
                body = gzip.decompress(body)
            except OSError as exc:
                raise ApiError(400, E.E_BUNDLE, f"Content-Encoding says gzip but the body is not: {exc}") from exc
        if len(body) > self.settings.max_body_bytes:
            raise ApiError(413, E.E_TOO_LARGE, f"The request is larger than {self.settings.max_body_bytes} bytes.")
        try:
            bundle = json.loads(body, parse_constant=_no_constants)
        except ValueError as exc:
            raise ApiError(400, E.E_BUNDLE, f"The body is not JSON: {exc}") from exc
        if not isinstance(bundle, dict):
            raise ApiError(400, E.E_BUNDLE, "The body must be a JSON object.")
        if isinstance(bundle.get("records"), list) and len(bundle["records"]) > self.settings.max_records:
            raise ApiError(413, E.E_TOO_LARGE, f"At most {self.settings.max_records} records per request.")
        problems = self.validator.bundle_errors(bundle)
        if problems:
            raise ApiError(400, E.E_BUNDLE, "The bundle envelope is invalid.", problems)
        return bundle

    # ------------------------------------------------------------------------------------------------ ingest
    def ingest(self, body: bytes, content_encoding: str | None, principal: str, dry_run: bool = False) -> dict:
        """Validate, plan versions and write one batch in one transaction. With dry_run, everything but the write: the
        receipt says what would be stored and what would be rejected (conformance tests, feeder development)."""
        started = time.perf_counter()
        received_at = utcnow()
        bundle = self.parse(body, content_encoding)
        batch_id = bundle["batch_id"]
        reg = self.registry.get()
        accepted, rejected, duplicates = self.validator.validate_batch(bundle["records"], reg, self.store, received_at)

        # versions
        by_kind: dict[str, list] = {}
        for p in accepted:
            by_kind.setdefault(p.kind, []).append(p)
        docs: dict[str, list[dict]] = {}
        counts = {"received": len(bundle["records"]), "new": 0, "versioned": 0, "unchanged": 0,
                  "duplicates": duplicates, "rejected": 0}
        versioned_ids = []
        for kind, items in by_kind.items():
            heads = self.store.heads(kind, [(p.record["id"], p.record["source"]) for p in items])
            for p in items:
                rec = p.record
                digest = content_digest(p.content)
                decision = plan(heads.get((rec["id"], rec["source"])), digest, p.known_at, received_at)
                if decision.outcome == "unchanged":
                    counts["unchanged"] += 1
                    continue
                version, known_at = decision.version, decision.known_at
                if decision.ingested:
                    rec = dict(rec, known_at=iso(received_at), known_at_basis="ingested")
                if decision.outcome == "new":
                    counts["new"] += 1
                else:
                    counts["versioned"] += 1
                    if len(versioned_ids) < 200:
                        versioned_ids.append({"id": rec["id"], "source": rec["source"], "version": version})
                start = parse_instant(rec["time"]["start"], "time.start") if "time" in rec else None
                end = parse_instant(rec["time"]["end"], "time.end") if "time" in rec and "end" in rec["time"] else None
                doc = dict(rec)
                doc["_id"] = doc_id(rec["id"], rec["source"], version)
                doc["version"] = version
                if kind == "event":
                    doc["lattice"] = lattice_keys(rec, start)
                doc["_t"] = {"known_at": known_at, **({"start": start} if start else {}), **({"end": end} if end else {})}
                doc["_ingest"] = {"batch_id": batch_id, "content": digest, "registry_revision": reg.revision,
                                  "received_at": received_at, "producer": bundle["producer"]}
                docs.setdefault(kind, []).append(doc)

        rejected.sort(key=lambda x: x["index"])
        counts["rejected"] = len(rejected)
        written = counts["new"] + counts["versioned"]
        status = ("rejected" if counts["rejected"] == counts["received"] else
                  "partial" if counts["rejected"] else
                  "unchanged" if written == 0 else "accepted")
        receipt = {"batch_id": batch_id, "status": status, "producer": bundle["producer"],
                   "principal": principal, "received_at": received_at,
                   "registry_revision": reg.revision, "counts": counts, "rejected": rejected,
                   "versioned": versioned_ids}

        if dry_run:
            receipt.update(dry_run=True, duration_ms=round((time.perf_counter() - started) * 1000))
            return receipt

        touched = {"h3_3": set(), "week": set(), "lspace": set()}
        for d in docs.get("event", []):
            for k in touched:
                if k in d.get("lattice", {}):
                    touched[k].add(d["lattice"][k])

        def write(session):
            db = self.db
            for kind, items in docs.items():
                db[COLLECTION[kind]].insert_many(items, ordered=False, session=session)
            receipt["duration_ms"] = round((time.perf_counter() - started) * 1000)
            db.receipts.insert_one(dict(receipt), session=session)
            if docs:
                db.outbox.insert_one({"topic": "ingest.accepted", "batch_id": batch_id,
                                      "docs": {COLLECTION[k]: [d["_id"] for d in v] for k, v in docs.items()},
                                      "touched": {k: sorted(v) for k, v in touched.items()},
                                      "created_at": received_at, "sent_at": None, "indexed_at": None}, session=session)

        try:
            with self.db.client.start_session() as session:
                session.with_transaction(write)
        except BulkWriteError as exc:
            raise ApiError(503, E.E_UNAVAILABLE, "A concurrent write stored some of these records first; send the batch again.") from exc
        except PyMongoError as exc:
            raise ApiError(503, E.E_UNAVAILABLE, f"MongoDB did not accept the write: {exc}") from exc
        return receipt
