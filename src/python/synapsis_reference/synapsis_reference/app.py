"""HTTP layer of the stand-in API: the same routes and bodies as the C# API for schemas, ingestion, records and
semantic search. Business logic lives in the services; this file only maps HTTP to them."""
from __future__ import annotations

import hmac
import logging
from contextlib import asynccontextmanager
from datetime import datetime, timezone

from elasticsearch import Elasticsearch
from fastapi import FastAPI, Header, Query, Request
from fastapi.concurrency import run_in_threadpool
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pymongo import MongoClient

from . import errors as E
from .errors import ApiError
from .ingest import IngestService
from .packages import PackageSchema
from .registry import RegistryCache
from .registry_store import RegistryStore
from .search import EmbedderClient, HybridSearch, TextIndexer
from .settings import Settings
from .store import MongoRecordStore
from .validator import SilValidator, parse_instant

log = logging.getLogger("synapsis.api")


class Services:
    """Everything the routes need, built once at start-up."""

    def __init__(self, settings: Settings):
        self.settings = settings
        self.client = MongoClient(settings.mongo_url, tz_aware=True)
        self.db = self.client[settings.mongo_db]
        self.registry_store = RegistryStore(self.db, PackageSchema(settings.package_schema_path))
        self.records = MongoRecordStore(self.db)
        self.registry = RegistryCache(self.registry_store.load_snapshot, self.registry_store.current_revision,
                                      settings.registry_poll_seconds)
        self.ingest = IngestService(self.db, self.records, SilValidator(settings.sil_schema_path), self.registry, settings)
        self.search: HybridSearch | None = None
        self.indexer: TextIndexer | None = None
        if settings.es_url and settings.embedder_url:
            es = Elasticsearch(settings.es_url, request_timeout=60)
            embedder = EmbedderClient(settings.embedder_url)
            self.indexer = TextIndexer(self.db, es, embedder, settings.index_poll_seconds)
            self.search = HybridSearch(es, embedder)

    def start(self) -> list[dict]:
        self.registry_store.ensure_indexes()
        self.records.ensure_indexes()
        seeded = self.registry_store.seed(self.settings.seed_dir)
        self.registry.invalidate()
        if self.indexer:
            self.indexer.ensure_indices()
            self.indexer.start()
        return seeded


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or Settings.from_env()
    services = Services(settings)

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        seeded = await run_in_threadpool(services.start)
        log.info("seeded: %s", seeded)
        app.state.seeded = seeded
        yield
        if services.indexer:
            services.indexer.stop()

    app = FastAPI(title="Synapsis API (stand-in)", version="1.2.0", lifespan=lifespan,
                  description="Python stand-in for the C# API: schemas, ingestion, records and semantic search.")

    @app.exception_handler(ApiError)
    async def api_error(_: Request, exc: ApiError):
        return JSONResponse(status_code=exc.status, content=exc.body())

    @app.exception_handler(RequestValidationError)
    async def bad_parameters(_: Request, exc: RequestValidationError):
        details = [{"path": ".".join(str(x) for x in e.get("loc", ()) if x not in ("query", "path", "header")),
                    "message": e.get("msg", "")} for e in exc.errors()][:20]
        return JSONResponse(status_code=400, content=ApiError(400, E.E_PARAM, "A parameter is missing or invalid.", details).body())

    def principal_of(authorization: str | None, x_api_key: str | None) -> str:
        key = settings.api_key
        given = x_api_key or (authorization[7:] if authorization and authorization.lower().startswith("bearer ") else None)
        if key and not (given and hmac.compare_digest(given.encode(), key.encode())):
            raise ApiError(401, E.E_AUTH, "A valid API key is required (Authorization: Bearer <key>).")
        return "api-key" if key else "anonymous"

    def as_of_param(value: str | None) -> datetime:
        if not value:
            return datetime.now(timezone.utc)
        if len(value) == 10:
            value += "T23:59:59Z"
        try:
            return parse_instant(value, "as_of")
        except Exception as exc:  # noqa: BLE001
            raise ApiError(400, E.E_PARAM, f"as_of must be a date or a UTC instant: {exc}") from exc

    @app.get("/v1/health")
    def health():
        services.client.admin.command("ping")
        backlog = services.db.outbox.count_documents({"topic": "ingest.accepted", "indexed_at": None})
        return {"status": "ok", "registry": services.registry.get().summary(),
                "search": bool(services.search), "index_backlog": backlog}

    # ----------------------------------------------------------------------------------------------- schemas
    @app.get("/v1/schemas")
    def list_schemas():
        return {"packages": services.registry_store.list_packages(), "revision": services.registry_store.current_revision()}

    @app.get("/v1/schemas/{package}")
    def get_schema(package: str, version: int | None = None):
        return services.registry_store.get_package(package, version)

    @app.get("/v1/schemas/{package}/versions")
    def schema_versions(package: str):
        return {"package": package, "versions": services.registry_store.versions(package)}

    @app.put("/v1/schemas/{package}")
    async def put_schema(package: str, request: Request, dry_run: bool = False,
                         if_match: str | None = Header(default=None),
                         authorization: str | None = Header(default=None), x_api_key: str | None = Header(default=None)):
        who = principal_of(authorization, x_api_key)
        body = await request.body()
        expected = int(if_match.strip('"')) if if_match else None
        status, result = await run_in_threadpool(services.registry_store.put_package, package, body,
                                                 request.headers.get("content-type"), who, expected, dry_run)
        if result["status"] in ("created", "updated"):
            services.registry.invalidate()
        return JSONResponse(status_code=status, content=result)

    @app.get("/v1/registry")
    def registry():
        return services.registry_store.registry()

    # ----------------------------------------------------------------------------------------------- ingest
    @app.post("/v1/ingest")
    async def ingest(request: Request, dry_run: bool = False, authorization: str | None = Header(default=None),
                     x_api_key: str | None = Header(default=None)):
        who = principal_of(authorization, x_api_key)
        body = await request.body()
        receipt = await run_in_threadpool(services.ingest.ingest, body, request.headers.get("content-encoding"), who,
                                          dry_run)
        return JSONResponse(content=_jsonable(receipt))

    @app.get("/v1/receipts/{batch_id}")
    def receipt(batch_id: str):
        doc = services.db.receipts.find_one({"_id": batch_id})
        if not doc:
            raise ApiError(404, E.E_NOT_FOUND, f"No receipt for batch '{batch_id}'.")
        doc.pop("_id")
        return _jsonable(doc)

    # ----------------------------------------------------------------------------------------------- reads
    @app.get("/v1/records/{record_id:path}")
    def record(record_id: str, as_of: str | None = None, source: str | None = None):
        """Every source's latest version of this id known by as_of (ids may contain '/')."""
        found = services.records.record_as_of(record_id, as_of_param(as_of), source)
        if not found:
            raise ApiError(404, E.E_NOT_FOUND, f"No record '{record_id}' known by {as_of or 'now'}.")
        return {"id": record_id, "as_of": as_of_param(as_of).isoformat(),
                "statements": [_jsonable(_public(d)) for d in found]}

    @app.get("/v1/stats")
    def stats():
        return services.records.stats()

    @app.get("/v1/search/semantic")
    def semantic(q: str = Query(min_length=2, max_length=500), as_of: str | None = None, k: int = Query(20, ge=1, le=100),
                 kinds: str | None = None, types: str | None = None, sources: str | None = None, rerank: bool = False,
                 mode: str = Query("hybrid", pattern="^(hybrid|vector|keyword)$")):
        if not services.search:
            raise ApiError(503, E.E_UNAVAILABLE, "Semantic search needs ES_URL and EMBEDDER_URL.")
        split = lambda s: [x for x in s.split(",") if x] if s else None  # noqa: E731
        return services.search.search(q, as_of_param(as_of), k, split(kinds), split(types), split(sources), rerank, mode)

    return app


def _public(doc: dict) -> dict:
    out = {k: v for k, v in doc.items() if k not in ("_id", "_t")}
    out["_ingest"] = {k: v for k, v in doc.get("_ingest", {}).items() if k != "content"}
    return out


def _jsonable(obj):
    if isinstance(obj, dict):
        return {k: _jsonable(v) for k, v in obj.items()}
    if isinstance(obj, (list, tuple)):
        return [_jsonable(v) for v in obj]
    if isinstance(obj, datetime):
        return obj.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")
    return obj
