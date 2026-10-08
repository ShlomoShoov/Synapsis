"""HTTP routes: POST /embed, POST /rerank, GET /models, GET /health.

Two static models are served by name: 'search' (potion-retrieval-32M, 512 numbers, used to index records and embed
questions) and 'meaning' (potion-base-8M, 256 numbers, the model the frozen meaning cells were built on)."""
from __future__ import annotations

import os

from fastapi import FastAPI, HTTPException
from fastapi.responses import ORJSONResponse
from pydantic import BaseModel, Field

from .models import Reranker, StaticEmbedder

MODELS = {
    "search": os.environ.get("SEARCH_MODEL", "minishlab/potion-retrieval-32M"),
    "meaning": os.environ.get("MEANING_MODEL", "minishlab/potion-base-8M"),
}
RERANK_MODEL = os.environ.get("RERANK_MODEL", "BAAI/bge-small-en-v1.5")


class EmbedRequest(BaseModel):
    texts: list[str] = Field(min_length=1, max_length=512)
    model: str = "search"


class RerankRequest(BaseModel):
    query: str = Field(min_length=1, max_length=2000)
    texts: list[str] = Field(min_length=1, max_length=100)


def create_app() -> FastAPI:
    # orjson writes the float32 arrays directly: 5,000 vectors of 512 numbers in milliseconds, not seconds
    app = FastAPI(title="synapsis-embedder", version="1.2.0", default_response_class=ORJSONResponse)
    embedders = {alias: StaticEmbedder(model_id) for alias, model_id in MODELS.items()}
    reranker = Reranker(RERANK_MODEL)

    @app.get("/health")
    def health():
        return {"status": "ok"}

    @app.get("/models")
    def models():
        return {"embed": {alias: {"model": e.model_id, "dims": e.dims} for alias, e in embedders.items()},
                "rerank": {"model": RERANK_MODEL}}

    @app.post("/embed")
    def embed(req: EmbedRequest):
        if req.model not in embedders:
            raise HTTPException(404, f"unknown model '{req.model}'; known: {sorted(embedders)}")
        if any(len(t) > 20000 for t in req.texts):
            raise HTTPException(413, "texts are limited to 20,000 characters")
        e = embedders[req.model]
        # returned as a response, so FastAPI hands the float32 array straight to orjson
        return ORJSONResponse({"model": e.model_id, "dims": e.dims, "vectors": e.embed(req.texts).astype("float32")})

    @app.post("/rerank")
    def rerank(req: RerankRequest):
        return {"model": RERANK_MODEL, "scores": reranker.scores(req.query, [t[:4000] for t in req.texts])}

    return app
