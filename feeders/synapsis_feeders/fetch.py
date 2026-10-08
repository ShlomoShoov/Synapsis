"""Fetching from sources: retries with backoff, polite pacing, and an optional on-disk cache for large archives.

Sources fail in ordinary ways (timeouts, 429, 502); a feeder must not lose a night's run to one of them. Errors that
retrying cannot fix (401, 403, 404) are raised at once with the URL, never with credentials in the message."""
from __future__ import annotations

import hashlib
import logging
import re
import time
from pathlib import Path

import httpx

log = logging.getLogger("feeder.http")
RETRY_STATUS = {408, 425, 429, 500, 502, 503, 504, 520, 522, 524}
SECRET = re.compile(r"(api_key|key|token|apikey|MAP_KEY)=[^&/]+|/api/area/csv/[^/]+/", re.I)


def redact(url: str) -> str:
    """A URL safe for logs: credentials in query strings or path segments are replaced."""
    return SECRET.sub(lambda m: m.group(0).split("=")[0] + "=***" if "=" in m.group(0) else "/api/area/csv/***/", url)


class SourceError(RuntimeError):
    pass


class Http:
    def __init__(self, user_agent: str, timeout: float, cache_dir: Path | None = None, min_interval: float = 0.0):
        # HTTP/2 when the server offers it: some public APIs (Wikimedia) refuse HTTP/1.1 clients that are not browsers
        self.client = httpx.Client(timeout=timeout, follow_redirects=True, headers={"User-Agent": user_agent}, trust_env=True,
                                   http2=True)
        self.cache_dir = cache_dir
        self.min_interval = min_interval
        self._last = 0.0

    def _pace(self) -> None:
        wait = self.min_interval - (time.monotonic() - self._last)
        if wait > 0:
            time.sleep(wait)
        self._last = time.monotonic()

    def get(self, url: str, *, params: dict | None = None, headers: dict | None = None, attempts: int = 6,
            ok_missing: bool = False, retry_on: frozenset = frozenset()) -> httpx.Response | None:
        """GET with retries. Returns None for 404 when ok_missing (an archive day that does not exist).
        retry_on adds source-specific retryable statuses (Arctic Shift answers 422 when it is busy)."""
        delay = 4.0
        for attempt in range(1, attempts + 1):
            self._pace()
            try:
                r = self.client.get(url, params=params, headers=headers)
            except (httpx.TransportError, httpx.TimeoutException) as exc:
                if attempt == attempts:
                    raise SourceError(f"{redact(url)}: {type(exc).__name__}") from exc
                log.warning("retry %s after %s (%d/%d)", redact(url), type(exc).__name__, attempt, attempts)
                time.sleep(delay)
                delay = min(delay * 2, 120)
                continue
            if r.status_code == 404 and ok_missing:
                return None
            if (r.status_code in RETRY_STATUS or r.status_code in retry_on) and attempt < attempts:
                retry_after = r.headers.get("Retry-After")
                pause = float(retry_after) if retry_after and retry_after.isdigit() else delay
                log.warning("retry %s after HTTP %d (%d/%d)", redact(url), r.status_code, attempt, attempts)
                time.sleep(min(pause, 300))
                delay = min(delay * 2, 120)
                continue
            if r.status_code >= 400:
                raise SourceError(f"{redact(str(r.url))}: HTTP {r.status_code} {r.text[:200]!r}")
            return r
        raise SourceError(f"{redact(url)}: gave up")

    def json(self, url: str, **kw):
        r = self.get(url, **kw)
        return None if r is None else r.json()

    def bytes(self, url: str, *, cache_name: str | None = None, **kw) -> bytes | None:
        """Body bytes; with cache_name, kept on disk so a re-run does not download an archive twice."""
        path = None
        if cache_name and self.cache_dir:
            path = self.cache_dir / cache_name
            if path.exists() and path.stat().st_size > 0:
                return path.read_bytes()
        r = self.get(url, **kw)
        if r is None:
            return None
        data = r.content
        if path is not None:
            path.parent.mkdir(parents=True, exist_ok=True)
            tmp = path.with_suffix(path.suffix + ".part")
            tmp.write_bytes(data)
            tmp.replace(path)
        return data


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()
