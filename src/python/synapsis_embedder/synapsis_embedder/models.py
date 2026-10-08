"""The local models.

StaticEmbedder  Model2Vec static models: a table of token vectors, averaged and normalised; thousands of texts per
                second on one CPU core. 'search' is potion-retrieval-32M (512 numbers), used to index records and embed
                questions; 'meaning' is potion-base-8M (256 numbers), the model the frozen meaning cells were built on.
Reranker        bge-small-en-v1.5 through ONNX Runtime (fastembed): slower and more precise. Used only to re-score
                the top hits of a search when the caller asks for it.

Every text is cleaned first (clean_text), the same way for records and questions."""
from __future__ import annotations

import os
import re
import threading
import unicodedata

import numpy as np

_ZERO_WIDTH = {"\u200b", "\u200c", "\u200d", "\u2060", "\ufeff"} | {chr(c) for c in range(0xFE00, 0xFE10)}
_SPACES = re.compile(r"\s+")


def clean_text(text: str) -> str:
    """What the tokenizer can read. NFKC folds styled letters (mathematical bold, full-width) into plain ones; symbols
    such as emoji and bullets, and invisible format characters, are removed so they no longer glue to the next word or
    split one ('MUE\u25cfRTO'); control characters become spaces. Without this, a headline in bold Unicode letters or
    '\U0001F7E5Niger' has no known token and gets a vector of zeros."""
    out = []
    for ch in unicodedata.normalize("NFKC", text or ""):
        if ch in _ZERO_WIDTH:
            continue
        cat = unicodedata.category(ch)
        if cat[0] == "S" or cat in ("Cf", "Co", "Cs", "Cn"):
            continue
        out.append(" " if cat == "Cc" else ch)
    return _SPACES.sub(" ", "".join(out)).strip()


class StaticEmbedder:
    def __init__(self, model_id: str):
        from model2vec import StaticModel
        self.model_id = model_id
        self.model = StaticModel.from_pretrained(model_id)
        self.dims = int(self.model.dim)

    def embed(self, texts: list[str]) -> np.ndarray:
        """Unit vectors; a text with no known token (after cleaning) gets a vector of zeros, which callers skip."""
        v = self.model.encode([clean_text(t) or " " for t in texts], batch_size=1024)
        n = np.linalg.norm(v, axis=1, keepdims=True)
        return v / np.maximum(n, 1e-9)


class Reranker:
    """Loaded on first use, so the service starts fast and works without it."""

    def __init__(self, model_id: str):
        self.model_id = model_id
        self._model = None
        self._lock = threading.Lock()

    def _get(self):
        with self._lock:
            if self._model is None:
                from fastembed import TextEmbedding
                self._model = TextEmbedding(self.model_id, threads=int(os.environ.get("RERANK_THREADS", "2")))
            return self._model

    def scores(self, query: str, texts: list[str]) -> list[float]:
        model = self._get()
        q = np.array(list(model.query_embed([clean_text(query) or " "]))[0])
        d = np.array(list(model.passage_embed([clean_text(t) or " " for t in texts])))
        q /= max(np.linalg.norm(q), 1e-9)
        d /= np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-9)
        return (d @ q).round(6).tolist()
