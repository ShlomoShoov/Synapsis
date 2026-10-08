"""What Reddit and Hacker News share: CVE mentions, the domain of a link, and the engagement observation."""
from __future__ import annotations

import re
from urllib.parse import urlparse

from ..sil import entity, event, logical, measures, participant, time_spec

CVE = re.compile(r"CVE-\d{4}-\d{4,7}", re.I)


def cve_mentions(ctx, src: dict, text: str, known_at) -> tuple[list[dict], list[dict]]:
    """(entities to send first, participants) for every CVE id written in the text."""
    ents, parts = [], []
    for cve in sorted({m.upper() for m in CVE.findall(text or "")}):
        eid = f"ent:cve:{cve}"
        if ctx.once(f"{eid}|{src['id']}"):
            ents.append(entity(eid, "Vulnerability", cve, src, known_at, "published"))
        parts.append(participant(eid, "mentions"))
    return ents, parts


def domain_of(url: str | None) -> str | None:
    if not url or not url.startswith("http"):
        return None
    host = urlparse(url).netloc.lower()
    return host[4:] if host.startswith("www.") else host or None


def engagement(src: dict, post_id: str, forum: tuple[str, str], fetched_at, score, comments) -> dict | None:
    m = measures(("score", score, "count"), ("engagement", comments, "count"))
    if not m:
        return None
    key = post_id.split(":", 2)[2]
    ns = post_id.split(":", 2)[1]
    return event(f"evt:{ns}:{key}@{fetched_at:%Y%m%d}", "TXT.POST.ENGAGEMENT", src, time_spec(fetched_at, "day"),
                 logical("social.forum", *forum), fetched_at, "observed", measures=m,
                 attributes={"post": post_id})
