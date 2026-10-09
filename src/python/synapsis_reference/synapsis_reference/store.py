"""MongoRecordStore: the truth. One collection per record kind, plus receipts and outbox.

Each stored document is the validated record plus storage fields:
  _id      '<id>|<source>|<version>'  (a record is identified by its id and its source; versions never overwrite)
  version  1, 2, ...                  (a changed record is a new version, see IngestService)
  lattice  the shared-space keys      (h3_2..h3_5, week, lspace, lpath prefixes, country, subdivision)
  _t       BSON dates for start, end and known_at, so range filters do not compare strings
  _ingest  batch id, content hash, registry revision, receive time"""
from __future__ import annotations

import hashlib
from typing import Iterable

from pymongo import ASCENDING, DESCENDING
from pymongo.database import Database

COLLECTION = {"entity": "entities", "event": "events", "relation": "relations", "conclusion": "conclusions"}


def doc_id(record_id: str, source: str, version: int) -> str:
    return f"{record_id}|{source}|{version}"


def content_digest(content: str) -> str:
    return hashlib.sha256(content.encode("utf-8")).hexdigest()


class MongoRecordStore:
    def __init__(self, db: Database):
        self.db = db

    def ensure_indexes(self) -> None:
        for kind, name in COLLECTION.items():
            c = self.db[name]
            c.create_index([("id", ASCENDING), ("source", ASCENDING), ("version", DESCENDING)])
            c.create_index([("_t.known_at", ASCENDING)])
            c.create_index([("type", ASCENDING)])
            c.create_index([("source", ASCENDING)])
        ev = self.db.events
        ev.create_index([("type", ASCENDING), ("_t.start", ASCENDING)])
        ev.create_index([("participants.entity", ASCENDING)])
        ev.create_index([("lattice.h3_3", ASCENDING), ("lattice.week", ASCENDING)])
        ev.create_index([("lattice.lpath", ASCENDING)])
        self.db.conclusions.create_index([("type", ASCENDING), ("about", ASCENDING)])
        self.db.conclusions.create_index([("premises", ASCENDING)])
        self.db.receipts.create_index([("producer", ASCENDING), ("received_at", DESCENDING)])
        self.db.receipts.create_index([("batch_id", ASCENDING), ("received_at", DESCENDING)])
        self.db.outbox.create_index([("sent_at", ASCENDING)])
        self.db.outbox.create_index([("indexed_at", ASCENDING)])

    # --------------------------------------------------------------------------------------- lookups for rules
    def entity_types(self, ids: Iterable[str]) -> dict[str, set[str]]:
        out: dict[str, set[str]] = {}
        ids = list(ids)
        for i in range(0, len(ids), 20000):
            for d in self.db.entities.find({"id": {"$in": ids[i:i + 20000]}}, {"id": 1, "type": 1, "_id": 0}):
                out.setdefault(d["id"], set()).add(d["type"])
        return out

    def existing(self, ids: Iterable[str]) -> set[str]:
        ids, found = list(ids), set()
        for name in COLLECTION.values():
            missing = [i for i in ids if i not in found]
            if not missing:
                break
            for k in range(0, len(missing), 20000):
                found.update(self.db[name].distinct("id", {"id": {"$in": missing[k:k + 20000]}}))
        return found

    def heads(self, kind: str, keys: list[tuple[str, str]]) -> dict[tuple[str, str], dict]:
        """Latest stored version of each (id, source) (version, content digest, known_at) with every version in
        `history`, which the version planner needs when a source re-reads its past (versions.py, rule 3)."""
        out: dict[tuple[str, str], dict] = {}
        wanted = set(keys)
        ids = list({k[0] for k in keys})
        for i in range(0, len(ids), 20000):
            cursor = self.db[COLLECTION[kind]].find({"id": {"$in": ids[i:i + 20000]}},
                                                    {"id": 1, "source": 1, "version": 1, "_ingest.content": 1, "_t.known_at": 1})
            for d in cursor:
                k = (d["id"], d["source"])
                if k not in wanted:
                    continue
                v = {"version": d["version"], "content": d["_ingest"]["content"], "known_at": d["_t"]["known_at"]}
                if k not in out:
                    out[k] = dict(v, history=[v])
                else:
                    out[k]["history"].append(v)
                    if v["version"] > out[k]["version"]:
                        out[k].update(v)
        return out

    # --------------------------------------------------------------------------------------- reads for clients
    def record_as_of(self, record_id: str, as_of, source: str | None = None) -> list[dict]:
        """For each source that stated this id: the latest version known by `as_of`."""
        out = []
        for name in COLLECTION.values():
            q = {"id": record_id, "_t.known_at": {"$lte": as_of}}
            if source:
                q["source"] = source
            best: dict[str, dict] = {}
            for d in self.db[name].find(q):
                if d["source"] not in best or d["version"] > best[d["source"]]["version"]:
                    best[d["source"]] = d
            out += list(best.values())
        return out

    def stats(self) -> dict:
        out = {}
        for kind, name in COLLECTION.items():
            pipeline = [{"$group": {"_id": {"source": "$source", "type": "$type"}, "n": {"$sum": 1},
                                    "ids": {"$addToSet": "$id"}}},
                        {"$project": {"n": 1, "distinct": {"$size": "$ids"}}}]
            rows = list(self.db[name].aggregate(pipeline, allowDiskUse=True))
            out[name] = sorted(({"source": r["_id"]["source"], "type": r["_id"]["type"], "documents": r["n"],
                                 "distinct_ids": r["distinct"]} for r in rows), key=lambda r: (r["source"], r["type"]))
        out["receipts"] = self.db.receipts.estimated_document_count()
        return out
