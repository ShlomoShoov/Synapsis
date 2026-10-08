"""RegistryStore: schema packages in MongoDB.

Collections: schema_packages (every version, immutable), schema_heads (current version per package),
registry_entries (one document per definition, `_id` = '<kind>:<code>', which enforces one owner per code),
registry_state (the global revision). A package write is one transaction that also appends a 'registry.changed'
entry to the outbox, so other services learn about it through the same relay as records."""
from __future__ import annotations

import glob
import os
from datetime import datetime, timezone

from pymongo import ReturnDocument
from pymongo.database import Database
from pymongo.errors import DuplicateKeyError

from .errors import (ApiError, E_NOT_FOUND, E_SCHEMA_BREAKING, E_SCHEMA_OWNERSHIP, E_SCHEMA_PACKAGE,
                     E_SCHEMA_REF, E_SCHEMA_VERSION)
from .packages import (PackageSchema, check_references, content_hash, definitions, deprecated_keys, diff,
                       parse_package)
from .registry import RegistrySnapshot


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


class RegistryStore:
    def __init__(self, db: Database, package_schema: PackageSchema):
        self.db = db
        self.schema = package_schema

    # ------------------------------------------------------------------------------------------------- reading
    def ensure_indexes(self) -> None:
        self.db.schema_packages.create_index([("package", 1), ("version", -1)])
        self.db.registry_entries.create_index("owner")

    def current_revision(self) -> int:
        doc = self.db.registry_state.find_one({"_id": "current"})
        return int(doc["revision"]) if doc else 0

    def load_snapshot(self) -> RegistrySnapshot:
        revision = self.current_revision()
        return RegistrySnapshot(list(self.db.registry_entries.find({})), revision)

    def list_packages(self) -> list[dict]:
        out = []
        for head in self.db.schema_heads.find({}).sort("_id", 1):
            n = self.db.registry_entries.count_documents({"owner": head["_id"]})
            out.append({"package": head["_id"], "version": head["version"], "hash": head["hash"],
                        "updated_at": head["updated_at"], "definitions": n})
        return out

    def get_package(self, name: str, version: int | None = None) -> dict:
        if version is None:
            head = self.db.schema_heads.find_one({"_id": name})
            if not head:
                raise ApiError(404, E_NOT_FOUND, f"No package '{name}'.")
            version = head["version"]
        doc = self.db.schema_packages.find_one({"_id": f"{name}@{version}"})
        if not doc:
            raise ApiError(404, E_NOT_FOUND, f"No version {version} of package '{name}'.")
        return {"package": name, "version": version, "hash": doc["hash"], "created_at": doc["created_at"],
                "created_by": doc["created_by"], "body": doc["body"]}

    def versions(self, name: str) -> list[dict]:
        docs = self.db.schema_packages.find({"package": name}, {"body": 0}).sort("version", 1)
        out = [{"version": d["version"], "hash": d["hash"], "created_at": d["created_at"], "created_by": d["created_by"],
                "changes": d.get("changes", [])} for d in docs]
        if not out:
            raise ApiError(404, E_NOT_FOUND, f"No package '{name}'.")
        return out

    def registry(self) -> dict:
        entries = list(self.db.registry_entries.find({}).sort("_id", 1))
        return {"revision": self.current_revision(),
                "definitions": [{"key": e["_id"], "owner": e["owner"], "since_version": e["since_version"],
                                 "deprecated": bool(e.get("deprecated")), "definition": e["def"]} for e in entries]}

    # ------------------------------------------------------------------------------------------------- writing
    def put_package(self, name: str, raw: bytes | str | dict, content_type: str | None, actor: str,
                    if_match: int | None = None, dry_run: bool = False) -> tuple[int, dict]:
        """Register a new package or a new version of one. Returns (HTTP status, result)."""
        pkg = raw if isinstance(raw, dict) else parse_package(raw, content_type)
        self.schema.check(pkg)
        if pkg["package"] != name:
            raise ApiError(422, E_SCHEMA_PACKAGE, f"The body declares package '{pkg['package']}' but the URL says '{name}'.")
        new_hash = content_hash(pkg)
        head = self.db.schema_heads.find_one({"_id": name})
        if if_match is not None and (head is None or head["version"] != if_match):
            raise ApiError(409, E_SCHEMA_VERSION, f"If-Match {if_match} does not match the current version "
                                                  f"{head['version'] if head else 'none'}.")
        if head and head["hash"] == new_hash:
            return 200, {"status": "unchanged", "package": name, "version": head["version"], "hash": new_hash,
                         "revision": self.current_revision(), "changes": []}

        old_pkg = self.db.schema_packages.find_one({"_id": f"{name}@{head['version']}"})["body"] if head else None
        new_defs = definitions(pkg)
        old_defs = definitions(old_pkg) if old_pkg else {}
        dep_new, dep_old = deprecated_keys(pkg), deprecated_keys(old_pkg) if old_pkg else set()
        foreign = [k for k in dep_new if k not in new_defs]
        if foreign:
            raise ApiError(422, E_SCHEMA_PACKAGE, "A package may only deprecate definitions it owns.",
                           [{"message": k} for k in foreign])

        owned = list(self.db.registry_entries.find({"_id": {"$in": list(new_defs)}, "owner": {"$ne": name}},
                                                   {"_id": 1, "owner": 1}))
        if owned:
            raise ApiError(409, E_SCHEMA_OWNERSHIP, f"{len(owned)} definition(s) belong to other packages.",
                           [{"key": d["_id"], "owner": d["owner"]} for d in owned])

        changes, breaks = diff(old_defs, new_defs)
        changes += [{"op": "deprecate", "key": k} for k in sorted(dep_new - dep_old)]
        changes += [{"op": "undeprecate", "key": k} for k in sorted(dep_old - dep_new)]
        if breaks:
            raise ApiError(409, E_SCHEMA_BREAKING, f"{len(breaks)} change(s) would invalidate stored records.",
                           [{"message": b} for b in breaks])

        prospective = {e["_id"]: e["def"] for e in self.db.registry_entries.find({"owner": {"$ne": name}})}
        prospective.update(new_defs)
        check_references(prospective)

        version = head["version"] + 1 if head else 1
        status = 201 if head is None else 200
        result = {"status": "created" if head is None else "updated", "package": name, "version": version,
                  "hash": new_hash, "changes": changes}
        if dry_run:
            result["status"] = "would_" + ("create" if head is None else "update")
            return 200, result

        now = utcnow()

        def write(session):
            db = self.db
            db.schema_packages.insert_one({"_id": f"{name}@{version}", "package": name, "version": version,
                                           "hash": new_hash, "body": pkg, "changes": changes, "created_at": now,
                                           "created_by": actor}, session=session)
            existing = {e["_id"]: e for e in db.registry_entries.find({"owner": name}, session=session)}
            for key, d in new_defs.items():
                since = existing[key]["since_version"] if key in existing else version
                db.registry_entries.replace_one({"_id": key}, {"_id": key, "owner": name, "since_version": since,
                                                               "updated_version": version, "def": d,
                                                               "deprecated": key in dep_new}, upsert=True,
                                                session=session)
            if head is None:
                db.schema_heads.insert_one({"_id": name, "version": version, "hash": new_hash, "updated_at": now},
                                           session=session)
            else:
                res = db.schema_heads.update_one({"_id": name, "version": head["version"]},
                                                 {"$set": {"version": version, "hash": new_hash, "updated_at": now}},
                                                 session=session)
                if res.matched_count != 1:
                    raise ApiError(409, E_SCHEMA_VERSION, "Another writer changed this package first.")
            state = db.registry_state.find_one_and_update({"_id": "current"},
                                                          {"$inc": {"revision": 1}, "$set": {"updated_at": now}},
                                                          upsert=True, return_document=ReturnDocument.AFTER,
                                                          session=session)
            db.outbox.insert_one({"_id": f"registry:{name}@{version}", "topic": "registry.changed", "package": name,
                                  "version": version, "revision": state["revision"], "created_at": now,
                                  "sent_at": None}, session=session)
            return state["revision"]

        try:
            with self.db.client.start_session() as session:
                revision = session.with_transaction(write)
        except DuplicateKeyError as exc:
            raise ApiError(409, E_SCHEMA_VERSION, "Another writer registered this version first.") from exc
        result["revision"] = revision
        return status, result

    # ------------------------------------------------------------------------------------------------- seeding
    def seed(self, seed_dir: str, actor: str = "seed") -> list[dict]:
        """Register every seed package that MongoDB does not have yet. Never overwrites a package that exists:
        after the first start, packages change only through the API. Core goes first; packages whose references
        are not there yet are retried after the others."""
        files = sorted(glob.glob(os.path.join(seed_dir, "*.yaml")), key=lambda f: (os.path.basename(f) != "core.yaml", f))
        results, pending = [], files
        for _ in range(3):
            retry = []
            for path in pending:
                with open(path, "rb") as fh:
                    raw = fh.read()
                pkg = parse_package(raw)
                name = pkg.get("package", os.path.basename(path)[:-5])
                if self.db.schema_heads.find_one({"_id": name}):
                    results.append({"package": name, "status": "present"})
                    continue
                try:
                    _, res = self.put_package(name, pkg, None, actor)
                    results.append({"package": name, "status": res["status"], "version": res["version"]})
                except ApiError as err:
                    if err.code == E_SCHEMA_REF:
                        retry.append(path)
                    else:
                        results.append({"package": name, "status": "refused", "code": err.code, "details": err.details[:5]})
            if not retry:
                break
            pending = retry
        else:
            results += [{"package": os.path.basename(p)[:-5], "status": "refused", "code": E_SCHEMA_REF} for p in pending]
        return results
