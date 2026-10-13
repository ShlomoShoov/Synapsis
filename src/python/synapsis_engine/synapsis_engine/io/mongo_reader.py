"""Records from MongoDB (synapsis_v2.records), in chunks. Every state is read; the workspace keeps the first."""
from pymongo import MongoClient

from .. import config

EVENT_FIELDS = {"_id": 0, "id": 1, "source": 1, "type": 1, "state": 1, "known_at": 1, "record.time": 1,
                "record.place": 1, "record.participants": 1, "record.measures": 1, "record.attributes": 1}


def _records(url=None, db=None):
    return MongoClient(url or config.mongo_url(), tz_aware=False)[db or config.mongo_db()]["records"]


def events(chunk=20000, url=None):
    """Event states, every source except the engine's own, as lists of documents."""
    cur = _records(url).find({"kind": "event", "source": {"$nin": config.IGNORED}}, EVENT_FIELDS, batch_size=chunk)
    buf = []
    for d in cur:
        buf.append(d)
        if len(buf) >= chunk:
            yield buf
            buf = []
    if buf:
        yield buf


def entity_names(url=None):
    """(id, state, name) of every entity state."""
    for d in _records(url).find({"kind": "entity"}, {"_id": 0, "id": 1, "state": 1, "record.name": 1}, batch_size=20000):
        yield d["id"], d.get("state", 1), (d.get("record") or {}).get("name")


def first_state_counts(url=None):
    """Events per source in MongoDB: distinct record ids (kind event, the engine's sources excluded)."""
    rows = _records(url).aggregate([
        {"$match": {"kind": "event", "source": {"$nin": config.IGNORED}}},
        {"$group": {"_id": {"s": "$source", "i": "$id"}}},
        {"$group": {"_id": "$_id.s", "n": {"$sum": 1}}}], allowDiskUse=True)
    return {r["_id"]: r["n"] for r in rows}
