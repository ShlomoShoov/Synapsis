"""Feeder settings, read from the environment. The one variable every feeder shares is SYNAPSIS_API_URL."""
from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


def _env(name: str, default: str | None = None) -> str | None:
    value = os.environ.get(name)
    return value if value not in (None, "") else default


def _schemas_dir() -> Path:
    """schemas/seed lives at the repository root; the Docker image copies it to /app/schemas."""
    configured = _env("SYNAPSIS_SCHEMAS_DIR")
    if configured:
        return Path(configured)
    here = Path(__file__).resolve()
    for parent in here.parents:
        if (parent / "schemas" / "seed").is_dir():
            return parent / "schemas"
    return Path("/app/schemas")


def _user_agent(contact: str | None) -> str:
    """Wikimedia and other public APIs ask for a contact in the User-Agent; set FEEDER_CONTACT to a URL or mailbox
    of the team that runs the feeders."""
    import httpx
    return f"SynapsisFeeders/1.0 ({contact or 'contact: set FEEDER_CONTACT'}) python-httpx/{httpx.__version__}"


@dataclass(frozen=True)
class Settings:
    api_url: str
    api_key: str | None
    state_dir: Path
    cache_dir: Path
    schemas_dir: Path
    batch_size: int
    user_agent: str
    http_timeout: float

    @staticmethod
    def from_env() -> "Settings":
        return Settings(
            api_url=_env("SYNAPSIS_API_URL", "http://localhost:8080").rstrip("/"),
            api_key=_env("SYNAPSIS_API_KEY"),
            state_dir=Path(_env("FEEDER_STATE_DIR", "./.feeder-state")),
            cache_dir=Path(_env("FEEDER_CACHE_DIR", "./.feeder-cache")),
            schemas_dir=_schemas_dir(),
            batch_size=int(_env("FEEDER_BATCH_SIZE", "5000")),
            user_agent=_env("FEEDER_USER_AGENT") or _user_agent(_env("FEEDER_CONTACT")),
            http_timeout=float(_env("FEEDER_HTTP_TIMEOUT", "180")),
        )

    def key(self, name: str) -> str | None:
        """A source credential from the environment. Values are never logged."""
        return _env(name)
