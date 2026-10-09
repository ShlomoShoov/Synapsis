"""Semantic search: the text index and the hybrid query.

TextIndexer does, for the stand-in, what the C# indexer does in the product: it follows the outbox, loads the records
of each accepted batch, embeds those that carry text and bulk-indexes them. HybridSearch answers
/search/semantic: one k-nearest-neighbour query and one keyword query, both filtered by known_at <= as_of,
merged by reciprocal-rank fusion, newest known version per record."""
from __future__ import annotations

import logging
import math
import threading
import time
import unicodedata
from datetime import datetime, timezone

import httpx
import orjson
from elasticsearch import Elasticsearch
from pymongo.database import Database

log = logging.getLogger("synapsis.search")

INDEX = {"event": "synapsis-events", "entity": "synapsis-entities", "conclusion": "synapsis-findings"}
VERSIONED_INDEX = {k: f"{v}-v1" for k, v in INDEX.items()}
DIMS = 512
MAPPING = {
    # The vector is indexed but not kept in _source: there it was 65% of each document's disk space (measured on 20,000
    # events), and it can always be computed again from the text, which a rebuild into a new index version does anyway.
    # Elasticsearch keeps a recovery copy of the full source for 12 hours before the space is freed.
    "_source": {"excludes": ["text_vector"]},
    "dynamic": "strict",
    "properties": {
        "id": {"type": "keyword"}, "kind": {"type": "keyword"}, "type": {"type": "keyword"},
        "source": {"type": "keyword"}, "reliability": {"type": "byte"}, "version": {"type": "integer"},
        "known_at": {"type": "date"}, "time_start": {"type": "date"},
        "title": {"type": "text", "analyzer": "english", "fields": {"raw": {"type": "keyword", "ignore_above": 512}}},
        "text": {"type": "text", "analyzer": "english"},
        "refs": {"type": "text", "analyzer": "standard"},
        "h3_3": {"type": "keyword"}, "lspace": {"type": "keyword"}, "lpath": {"type": "keyword"},
        "country": {"type": "keyword"}, "subdivision": {"type": "keyword"},
        "embed_model": {"type": "keyword"},
        "text_vector": {"type": "dense_vector", "dims": DIMS, "index": True, "similarity": "cosine"},
    },
}


class EmbedderClient:
    """HTTP client for synapsis-embedder: POST /embed and POST /rerank."""

    def __init__(self, base_url: str, timeout: float = 120.0):
        self._http = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)

    def embed(self, texts: list[str]) -> tuple[str, list[list[float]]]:
        vectors, model = [], ""
        for i in range(0, len(texts), 512):
            r = self._http.post("/embed", json={"texts": texts[i:i + 512], "model": "search"})
            r.raise_for_status()
            body = orjson.loads(r.content)
            model = body["model"]
            vectors += body["vectors"]
        return model, vectors

    def rerank(self, query: str, texts: list[str]) -> list[float]:
        r = self._http.post("/rerank", json={"query": query, "texts": texts})
        r.raise_for_status()
        return r.json()["scores"]


def text_of(doc: dict) -> tuple[str, str]:
    """(title, full text) of a stored record; full text is what gets embedded."""
    if doc["kind"] == "conclusion":
        title = (doc.get("text") or {}).get("title") or f"{doc['type']}: {doc['statement']['predicate']}"
        return title, title + "\n" + ((doc.get("text") or {}).get("body") or "")
    t = doc.get("text") or {}
    title = t.get("title") or doc.get("name") or ""
    parts = [title, t.get("body") or ""]
    if doc["kind"] == "entity" and doc.get("aliases"):
        parts.append("; ".join(doc["aliases"]))
    return title, "\n".join(p for p in parts if p)


def has_text(doc: dict) -> bool:
    """Entities are always searchable by name; events and findings only when they carry text."""
    return doc["kind"] == "entity" or bool(doc.get("text"))


def for_keywords(text: str) -> str:
    """NFKC, so words written in styled Unicode letters (mathematical bold, full-width) match plain queries. The record
    keeps the text as published; only the search copy is folded."""
    return unicodedata.normalize("NFKC", text)


def usable(vector: list[float]) -> bool:
    """False for the zero vector the embedder returns when a text has no known token (cosine cannot use it)."""
    return math.fsum(x * x for x in vector) > 1e-12


class TextIndexer:
    def __init__(self, db: Database, es: Elasticsearch, embedder: EmbedderClient, poll_seconds: float):
        self.db, self.es, self.embedder, self.poll = db, es, embedder, poll_seconds
        self._stop = threading.Event()

    def ensure_indices(self) -> None:
        for kind, name in VERSIONED_INDEX.items():
            if not self.es.indices.exists(index=name):
                self.es.indices.create(index=name, mappings=MAPPING,
                                       settings={"number_of_shards": 1, "number_of_replicas": 0},
                                       aliases={INDEX[kind]: {}})

    def start(self) -> None:
        threading.Thread(target=self._loop, name="text-indexer", daemon=True).start()

    def stop(self) -> None:
        self._stop.set()

    def _loop(self) -> None:
        while not self._stop.is_set():
            try:
                done = self.run_once()
            except Exception:  # noqa: BLE001 - keep the loop alive; the entry stays pending and is retried
                log.exception("text indexing failed; retrying")
                done = 0
            if done == 0:
                self._stop.wait(self.poll)

    def run_once(self, limit: int = 10) -> int:
        """Index the oldest pending batches. A store that cannot be reached raises and the batch stays pending; a
        document the index refuses is recorded on the outbox entry and does not hold up the batches behind it."""
        entries = list(self.db.outbox.find({"topic": "ingest.accepted", "indexed_at": None}).sort("created_at", 1).limit(limit))
        for entry in entries:
            _, failed = self.index_batch(entry)
            update = {"indexed_at": datetime.now(timezone.utc)}
            if failed:
                update["index_errors"] = failed[:20]
                log.warning("batch %s: %d documents not indexed, first: %s", entry["batch_id"], len(failed), failed[0])
            self.db.outbox.update_one({"_id": entry["_id"]}, {"$set": update})
        return len(entries)

    def index_batch(self, entry: dict) -> tuple[int, list[dict]]:
        """Returns (documents indexed, documents refused with the reason)."""
        docs = []
        for coll, ids in entry["docs"].items():
            if coll == "relations":
                continue
            docs += [d for d in self.db[coll].find({"_id": {"$in": ids}}) if has_text(d)]
        if not docs:
            return 0, []
        pairs = [text_of(d) for d in docs]
        model, vectors = self.embedder.embed([p[1][:4000] for p in pairs])
        actions = []
        for d, (title, text), vec in zip(docs, pairs, vectors):
            lat = d.get("lattice") or {}
            src = {"id": d["id"], "kind": d["kind"], "type": d["type"], "source": d["source"],
                   "reliability": d["reliability"], "version": d["version"], "known_at": d["_t"]["known_at"],
                   "title": for_keywords(title[:1000]), "text": for_keywords(text[:20000]),
                   "refs": " ".join([d["id"].split(":", 2)[2]] + [p["entity"].split(":", 2)[2] for p in d.get("participants") or []])}
            if usable(vec):     # a text with no known token stays findable by its words only
                src.update(embed_model=model, text_vector=vec)
            if d["_t"].get("start"):
                src["time_start"] = d["_t"]["start"]
            for k in ("h3_3", "lspace", "lpath", "country", "subdivision"):
                if k in lat:
                    src[k] = lat[k]
            actions.append({"_index": INDEX[d["kind"]], "_id": d["_id"], "_source": src})
        return self._bulk(actions)

    def _bulk(self, actions: list[dict], chunk: int = 1000) -> tuple[int, list[dict]]:
        """Bulk-index with the NDJSON body encoded by orjson (the standard encoder spends most of the time on the
        vectors). A refused document is returned with its reason; a store that cannot be reached raises."""
        ok, failed = 0, []
        for i in range(0, len(actions), chunk):
            body = bytearray()
            for a in actions[i:i + chunk]:
                body += orjson.dumps({"index": {"_index": a["_index"], "_id": a["_id"]}}) + b"\n"
                body += orjson.dumps(a["_source"]) + b"\n"
            resp = self.es.bulk(operations=bytes(body), refresh=False)
            for item in resp["items"]:
                r = item.get("index") or {}
                if r.get("status", 500) < 300:
                    ok += 1
                else:
                    failed.append({"id": r.get("_id"), "reason": str((r.get("error") or {}).get("reason", r))[:300]})
        return ok, failed


class HybridSearch:
    def __init__(self, es: Elasticsearch, embedder: EmbedderClient):
        self.es, self.embedder = es, embedder

    def search(self, q: str, as_of: datetime, k: int = 20, kinds: list[str] | None = None, types: list[str] | None = None,
               sources: list[str] | None = None, rerank: bool = False, mode: str = "hybrid") -> dict:
        t0 = time.perf_counter()
        indices = ",".join(INDEX[x] for x in (kinds or ["event", "entity", "conclusion"]) if x in INDEX)
        filters = [{"range": {"known_at": {"lte": as_of.isoformat()}}}]
        if types:
            filters.append({"terms": {"type": types}})
        if sources:
            filters.append({"terms": {"source": sources}})
        model, (qvec,) = self.embedder.embed([q])
        fields = ["id", "kind", "type", "source", "reliability", "version", "known_at", "time_start", "title", "h3_3", "lpath", "country", "subdivision"]
        if not usable(qvec):    # no known word in the question: words only
            mode = "keyword"
        knn = self.es.search(index=indices, knn={"field": "text_vector", "query_vector": qvec, "k": 50,
                                                 "num_candidates": 500, "filter": filters},
                             source=fields + ["text"], size=50) if mode != "keyword" else {"hits": {"hits": []}}
        bm25 = self.es.search(index=indices, query={"bool": {"must": {"multi_match": {"query": for_keywords(q), "fields": ["title^2", "text", "refs^3"]}},
                                                            "filter": filters}}, source=fields + ["text"], size=50) if mode != "vector" else {"hits": {"hits": []}}
        fused: dict[str, dict] = {}
        lists = {"hybrid": (("vector", knn), ("keyword", bm25)), "vector": (("vector", knn),), "keyword": (("keyword", bm25),)}[mode]
        for name, res in lists:
            for rank, hit in enumerate(res["hits"]["hits"], 1):
                s = hit["_source"]
                key = f"{s['id']}|{s['source']}"
                slot = fused.setdefault(key, {"hit": s, "score": 0.0, "ranks": {}})
                if s["version"] > slot["hit"]["version"]:
                    slot["hit"] = s          # newest version known by as_of wins
                if name not in slot["ranks"]:
                    slot["ranks"][name] = rank
                    slot["score"] += 1.0 / (60 + rank)
        ranked = sorted(fused.values(), key=lambda x: -x["score"])[: max(k, 50) if rerank else k]
        if rerank and ranked:
            scores = self.embedder.rerank(q, [r["hit"].get("text", r["hit"]["title"])[:2000] for r in ranked])
            for r, s in zip(ranked, scores):
                r["rerank"] = s
            ranked = sorted(ranked, key=lambda x: -x["rerank"])[:k]
        hits = []
        for r in ranked:
            h = dict(r["hit"])
            text = h.pop("text", "")
            h["snippet"] = text[:280]
            h["score"] = round(r["score"], 5)
            h["ranks"] = r["ranks"]
            if "rerank" in r:
                h["rerank"] = round(r["rerank"], 4)
            hits.append(h)
        return {"q": q, "as_of": as_of.isoformat(), "mode": mode, "model": model, "took_ms": round((time.perf_counter() - t0) * 1000),
                "hits": hits}
