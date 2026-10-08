"""Version planning: whether an accepted record is new, a new version, or already known.

The C# VersionPlanner keeps the same rules (implementation guide, section 6, Versions):

  1. No stored version of (id, source): version 1, known_at as sent.
  2. The same content as the latest version: unchanged, whatever known_at says (a stored past is never rewritten).
  3. A known_at earlier than the latest version's is a re-read of the past (a backfill run again, an older page read
     again). It is compared with the version that was current at that known_at: the same content is unchanged, because
     the source only repeated what was already known then.
  4. Otherwise the content changed. An immutable type refuses it (E-ID-CONFLICT). A versioned type stores version n+1,
     known at the sent known_at when that is later than the latest version's and not after the receive time, and else
     known at the receive time (basis 'ingested'): what Synapsis learned now cannot be placed in its past.

Rule 3 is what keeps a partial re-run honest. Without it, re-reading 2025 after 2026 was loaded would send each renamed
bank's 2025 name again, and that old name would become the bank's latest version."""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone


@dataclass(frozen=True)
class Decision:
    outcome: str                      # "new", "versioned", "unchanged" or "conflict"
    version: int | None = None
    known_at: datetime | None = None
    ingested: bool = False            # True when known_at became the receive time


def _utc(t: datetime) -> datetime:
    """MongoDB returns its own UTC tzinfo (or none): compare everything as aware UTC datetimes."""
    return t.replace(tzinfo=timezone.utc) if t.tzinfo is None else t.astimezone(timezone.utc)


def current_at(history: list[dict], at: datetime) -> dict | None:
    """The version that was the latest known at `at`: greatest known_at not after it, then highest version."""
    best = None
    for h in history:
        if _utc(h["known_at"]) <= at and (best is None or (_utc(h["known_at"]), h["version"]) > (_utc(best["known_at"]), best["version"])):
            best = h
    return best


def plan(head: dict | None, digest: str, known_at: datetime, received_at: datetime, immutable: bool) -> Decision:
    """`head`: the latest stored version {version, content, known_at, history: [{version, content, known_at}, ...]}."""
    if head is None:
        return Decision("new", 1, known_at)
    if head["content"] == digest:
        return Decision("unchanged")
    head_known = _utc(head["known_at"])
    if known_at < head_known:
        then = current_at(head.get("history") or [], known_at)
        if then is not None and then["content"] == digest:
            return Decision("unchanged")
    if immutable:
        return Decision("conflict")
    if head_known < known_at <= received_at:
        return Decision("versioned", head["version"] + 1, known_at)
    return Decision("versioned", head["version"] + 1, received_at, ingested=True)
