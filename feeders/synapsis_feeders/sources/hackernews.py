"""Hacker News stories matching security terms, from the Algolia search API, one month per query.

Option --set queries=CVE,vulnerability,... (default: CVE, vulnerability, ransomware, exploit, zero-day, breach).
A story found by several queries is sent once."""
from __future__ import annotations

from datetime import date, datetime, timezone

from ..base import Checkpoint, Feeder, register
from ..sil import attributes, event, logical, text, time_spec
from .social_common import cve_mentions, domain_of, engagement

API = "https://hn.algolia.com/api/v1/search_by_date"
QUERIES = "CVE,vulnerability,ransomware,exploit,zero-day,breach"
FIRST = date(2021, 11, 1)


def month_starts(start: date, end: date):
    d = date(start.year, start.month, 1)
    while d <= end:
        yield d
        d = date(d.year + (d.month == 12), d.month % 12 + 1, 1)


@register
class HackerNewsFeeder(Feeder):
    name = "hackernews"
    package = "hackernews"
    requires = ("social",)
    title = "Hacker News security stories (Algolia)"
    min_interval = 0.25

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:hn:algolia")
        last = ctx.cursor.get("last_month")
        start = ctx.since or (date.fromisoformat(last) if last else FIRST)
        end = ctx.until or ctx.started_at.date()
        for m0 in month_starts(start, end):
            m1 = date(m0.year + (m0.month == 12), m0.month % 12 + 1, 1)
            t0 = int(datetime.combine(m0, datetime.min.time(), timezone.utc).timestamp())
            t1 = int(datetime.combine(m1, datetime.min.time(), timezone.utc).timestamp())
            for q in ctx.options.get("queries", QUERIES).split(","):
                lo = t0
                while True:     # Algolia returns at most 1,000 hits per query; walk forward in time when a month is busier
                    body = ctx.http.json(API, params={"tags": "story", "query": q, "hitsPerPage": 1000, "typoTolerance": "false",
                                                      "numericFilters": f"created_at_i>={lo},created_at_i<{t1}"})
                    hits = sorted(body.get("hits", []), key=lambda h: h["created_at_i"])
                    for h in hits:
                        yield from self._story(h, src)
                    if len(hits) < 1000:
                        break
                    lo = hits[-1]["created_at_i"] + 1
            yield Checkpoint({"last_month": m0.isoformat()})

    def _story(self, h: dict, src: dict):
        ctx = self.ctx
        if not h.get("title"):
            return
        pid = f"evt:hackernews:{h['objectID']}"
        if not ctx.once(pid):
            return
        created = datetime.fromtimestamp(int(h["created_at_i"]), timezone.utc)
        url = h.get("url")
        ents, parts = cve_mentions(ctx, src, f"{h['title']} {url or ''}", created)
        yield from ents
        yield event(pid, "TXT.POST", src, time_spec(created, "second"), logical("social.forum", "hackernews", "story"), created,
                    "published", participants=parts or None, text=text(h["title"], lang=None),
                    attributes=attributes(url=(url or "")[:2000] or None, domain=domain_of(url)),
                    evidence={"url": f"https://news.ycombinator.com/item?id={h['objectID']}"})
        e = engagement(src, pid, ("hackernews", "story"), ctx.started_at, h.get("points"), h.get("num_comments"))
        if e:
            yield e
