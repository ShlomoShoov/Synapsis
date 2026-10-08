"""ransomware.live: victims posted on leak sites, one month per request (api.ransomware.live/v2/victims/YYYY/MM).

Each run re-reads the last two months, because posts for a month keep arriving after it ends; the API's versioning
turns re-reads into 'unchanged'. A victim posted twice by the same group is one event (round-3 rule)."""
from __future__ import annotations

import hashlib
import re
from datetime import date

from ..base import Checkpoint, Feeder, register
from ..sil import admin, attributes, entity, event, logical, participant, place, slug, text, time_spec, utc

API = "https://api.ransomware.live/v2/victims/{y}/{m:02d}"
FIRST_MONTH = date(2020, 1, 1)


def normalise_group(name: str) -> str:
    g = re.sub(r"\s+", " ", (name or "").strip().lower())
    return "lockbit" if g.startswith("lockbit") else g


def months(start: date, end: date):
    y, m = start.year, start.month
    while (y, m) <= (end.year, end.month):
        yield y, m
        y, m = (y + 1, 1) if m == 12 else (y, m + 1)


@register
class RansomwareLiveFeeder(Feeder):
    name = "ransomware-live"
    package = "ransomware-live"
    title = "Ransomware leak-site victim posts (ransomware.live)"
    min_interval = 1.2

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:ransomware-live:victims")
        today = ctx.started_at.date()
        if ctx.since:
            start = ctx.since.replace(day=1)
        elif ctx.cursor.get("last_month"):
            y, m = map(int, ctx.cursor["last_month"].split("-"))
            start = date(y - 1, 12, 1) if m == 1 else date(y, m - 1, 1)
        else:
            start = FIRST_MONTH
        end = ctx.until or today
        for y, m in months(start, end):
            rows = ctx.http.json(API.format(y=y, m=m)) or []
            for v in sorted(rows, key=lambda r: (r.get("discovered") or "", r.get("victim") or "")):
                rec = self._map(v, src)
                if rec:
                    yield from rec
            yield Checkpoint({"last_month": f"{y}-{m:02d}"})

    def _map(self, v: dict, src: dict):
        ctx = self.ctx
        group = normalise_group(v.get("group"))
        victim = (v.get("victim") or v.get("domain") or "").strip()
        if not group or not victim or not v.get("discovered"):
            return None
        seen = utc(v["discovered"])
        gid = f"ent:rgroup:{slug(group)}"
        out = []
        if ctx.once(gid):
            out.append(entity(gid, "RansomwareGroup", group, src, seen, "observed"))
        digest = hashlib.sha1(f"{group}|{victim.lower()}".encode("utf-8")).hexdigest()[:10]
        eid = f"evt:ransomware-live:{slug(group, 60)}/{slug(victim, 80)}-{digest}"
        if not ctx.once(eid):
            return out          # the same victim posted again by the same group
        country = (v.get("country") or "").strip().upper()
        attack = (v.get("attackdate") or "")[:10]
        press = v.get("press") if isinstance(v.get("press"), dict) else {}
        out.append(event(
            eid, "CYB.RANSOM.VICTIM_POSTED", src, time_spec(seen, "day"),
            place(logical("cyber.ransom_group", group), admin(country) if re.fullmatch(r"[A-Z]{2}", country) else None),
            seen, "observed",
            participants=[participant(gid, "actor")],
            text=text(victim, v.get("description"), lang=None),
            attributes=attributes(victim=victim[:500], domain=(v.get("domain") or "")[:300] or None,
                                  sector=(v.get("activity") or "")[:200] or None,
                                  attack_date=attack if re.fullmatch(r"\d{4}-\d{2}-\d{2}", attack) and attack <= seen.date().isoformat() else None,
                                  group_as_listed=(v.get("group") or "")[:200], press_source=(press.get("source") or "")[:2000] or None)))
        return out
