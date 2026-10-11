"""SynapsisApi: the feeders' only connection to Synapsis: send a batch (and ask whether a record exists).

Sending is idempotent, so every failure that is not the feeder's fault is retried: the same records sent again come
back as "unchanged"."""
from __future__ import annotations

import gzip
import json
import logging
import time
from urllib.parse import quote

import httpx

from .config import Settings

log = logging.getLogger("feeder.api")


class ApiRefused(RuntimeError):
    """The API refused the request for a reason retrying cannot fix (a malformed batch)."""

    def __init__(self, status: int, body: dict | str):
        self.status, self.body = status, body
        detail = body.get("title") if isinstance(body, dict) else ""
        super().__init__(f"HTTP {status} {detail}: {json.dumps(body)[:1500] if isinstance(body, dict) else body[:500]}")


class SynapsisApi:
    def __init__(self, settings: Settings):
        headers = {"User-Agent": settings.user_agent}
        # trust_env=False: the API is a local or internal address and must not go through an outbound proxy
        self.http = httpx.Client(base_url=settings.api_url, headers=headers, timeout=300, trust_env=False)
        self.url = settings.api_url

    def _call(self, method: str, path: str, *, attempts: int = 10, **kw) -> httpx.Response:
        delay = 2.0
        for attempt in range(1, attempts + 1):
            try:
                r = self.http.request(method, path, **kw)
            except httpx.TransportError as exc:
                if attempt == attempts:
                    raise
                log.warning("API %s unreachable (%s); retry %d/%d in %.0fs", self.url, type(exc).__name__, attempt, attempts, delay)
                time.sleep(delay)
                delay = min(delay * 2, 60)
                continue
            if r.status_code in (429, 500, 502, 503, 504) and attempt < attempts:
                log.warning("API answered %d; retry %d/%d in %.0fs", r.status_code, attempt, attempts, delay)
                time.sleep(delay)
                delay = min(delay * 2, 60)
                continue
            return r
        raise RuntimeError("unreachable")

    def record_exists(self, record_id: str) -> bool:
        """True when Synapsis holds the record (any source, any version known now)."""
        r = self._call("GET", f"/records/{quote(record_id, safe=':/@')}")
        if r.status_code == 404:
            return False
        if r.status_code != 200:
            raise ApiRefused(r.status_code, _body(r))
        return True

    def ingest(self, bundle: dict) -> dict:
        """POST one batch (gzip). Returns the batch summary. Raises ApiRefused on 4xx: the batch itself is wrong."""
        payload = gzip.compress(json.dumps(bundle, separators=(",", ":"), ensure_ascii=False).encode("utf-8"), 6)
        r = self._call("POST", "/ingest", content=payload,
                       headers={"Content-Type": "application/json", "Content-Encoding": "gzip"})
        if r.status_code != 200:
            raise ApiRefused(r.status_code, _body(r))
        return r.json()


def _body(r: httpx.Response) -> dict | str:
    try:
        return r.json()
    except ValueError:
        return r.text
