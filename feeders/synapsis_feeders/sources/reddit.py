"""Reddit via the Arctic Shift archive: posts of chosen communities, oldest first, one quarter at a time.

Option --set subreddits=NAME:START[:END],... (default COVID19positive:2020-07-01:2023-03-01,netsec:2021-11-01).
Only id, time, title, link and flair are requested; authors are never fetched."""
from __future__ import annotations

from datetime import date, datetime, timezone

from ..base import Checkpoint, Feeder, register
from ..sil import attributes, event, logical, text, time_spec
from .social_common import cve_mentions, domain_of, engagement

API = "https://arctic-shift.photon-reddit.com/api/posts/search"
FIELDS = "id,created_utc,title,url,score,num_comments,link_flair_text"
DEFAULT = "COVID19positive:2020-07-01:2023-03-01,netsec:2021-11-01"


@register
class RedditFeeder(Feeder):
    name = "reddit"
    package = "reddit"
    requires = ("social",)
    title = "Reddit posts (Arctic Shift archive)"
    min_interval = 0.4

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:reddit:arcticshift")
        cursor = dict(ctx.cursor)
        for spec in ctx.options.get("subreddits", DEFAULT).split(","):
            parts = spec.split(":")
            sub, first = parts[0], date.fromisoformat(parts[1])
            last = date.fromisoformat(parts[2]) if len(parts) > 2 and parts[2] else ctx.started_at.date()
            if ctx.since:
                after = int(datetime.combine(ctx.since, datetime.min.time(), timezone.utc).timestamp())
            else:
                after = int(cursor.get(sub, 0)) or int(datetime.combine(first, datetime.min.time(), timezone.utc).timestamp())
            end_ts = int(datetime.combine(min(ctx.until or last, last), datetime.min.time(), timezone.utc).timestamp())
            while after < end_ts:
                page = (ctx.http.json(API, params={"subreddit": sub, "after": after, "before": end_ts, "limit": "auto",
                                                   "sort": "asc", "fields": FIELDS},
                                     retry_on=frozenset({422}), attempts=8) or {}).get("data") or []
                if not page:
                    break
                for p in page:
                    yield from self._post(p, sub, src)
                newest = int(page[-1]["created_utc"])
                if newest <= after:
                    break
                after = newest
                cursor[sub] = after
                yield Checkpoint(dict(cursor))

    def _post(self, p: dict, sub: str, src: dict):
        ctx = self.ctx
        if not p.get("title"):
            return
        created = datetime.fromtimestamp(int(p["created_utc"]), timezone.utc)
        pid = f"evt:reddit:{p['id']}"
        if not ctx.once(pid):
            return
        url = p.get("url") if isinstance(p.get("url"), str) else None
        ents, parts = cve_mentions(ctx, src, f"{p['title']} {url or ''}", created)
        yield from ents
        yield event(pid, "TXT.POST", src, time_spec(created, "second"), logical("social.forum", "reddit", sub), created, "published",
                    participants=parts or None, text=text(p["title"], lang=None),
                    attributes=attributes(url=url[:2000] if url and "reddit.com" not in url else None, domain=domain_of(url),
                                          flair=(p.get("link_flair_text") or "")[:200] or None),
                    evidence={"url": f"https://www.reddit.com/r/{sub}/comments/{p['id']}/"})
        e = engagement(src, pid, ("reddit", sub), ctx.started_at, p.get("score"), p.get("num_comments"))
        if e:
            yield e
