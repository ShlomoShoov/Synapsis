"""Settings, read once from the environment. Defaults suit a developer machine with the Compose stores running."""
from __future__ import annotations

import os
from dataclasses import dataclass


def _env(name: str, default: str | None = None) -> str | None:
    value = os.environ.get(name)
    return value if value not in (None, "") else default


@dataclass(frozen=True)
class Settings:
    mongo_url: str
    mongo_db: str
    es_url: str | None
    embedder_url: str | None
    seed_dir: str
    sil_schema_path: str
    package_schema_path: str
    api_key: str | None
    max_records: int
    max_body_bytes: int
    index_poll_seconds: float
    registry_poll_seconds: float

    @staticmethod
    def from_env() -> "Settings":
        root = _env("SYNAPSIS_SCHEMAS_DIR", "schemas")
        return Settings(
            mongo_url=_env("MONGO_URL", "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true"),
            mongo_db=_env("MONGO_DB", "synapsis"),
            es_url=_env("ES_URL"),
            embedder_url=_env("EMBEDDER_URL"),
            seed_dir=_env("SEED_DIR", os.path.join(root, "seed")),
            sil_schema_path=os.path.join(root, "sil", "sil-1.2.schema.json"),
            package_schema_path=os.path.join(root, "sil", "schema-package.schema.json"),
            api_key=_env("SYNAPSIS_API_KEY"),
            max_records=int(_env("MAX_RECORDS", "5000")),
            max_body_bytes=int(_env("MAX_BODY_MB", "64")) * 1024 * 1024,
            index_poll_seconds=float(_env("INDEX_POLL_SECONDS", "2")),
            registry_poll_seconds=float(_env("REGISTRY_POLL_SECONDS", "5")),
        )
