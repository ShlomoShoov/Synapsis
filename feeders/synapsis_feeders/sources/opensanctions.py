"""OpenSanctions maritime: the latest export (about 23,000 rows), read when OpenSanctions publishes a new version.

Rows with the same IMO number describe one ship: it becomes one Vessel entity (shared namespace imo) whose aliases are
every name it is listed under. Each row is one listing event. The export has no listing dates, so the first export
in which the feeder saw each row is kept in the feeder's observed/ folder (kept by --reset), and a listing keeps that
date across runs."""
from __future__ import annotations

import io
import json
import re
from collections import Counter

import pandas as pd
import pycountry

from ..base import Checkpoint, Feeder, register
from ..sil import admin, attributes, entity, event, participant, place, time_spec, utc

INDEX = "https://data.opensanctions.org/datasets/latest/maritime/index.json"
IMO = re.compile(r"^IMO\d{7}$")


def iso2(code: str) -> str | None:
    c = (code or "").strip().upper()
    if len(c) == 2 and pycountry.countries.get(alpha_2=c):
        return c
    if len(c) == 3:
        found = pycountry.countries.get(alpha_3=c)
        return found.alpha_2 if found else None
    return None


def split(v: str) -> list[str]:
    return [x.strip() for x in (v or "").split(";") if x.strip()]


@register
class OpenSanctionsFeeder(Feeder):
    name = "opensanctions"
    package = "opensanctions"
    title = "OpenSanctions maritime risk list"

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:opensanctions:maritime")
        idx = ctx.http.json(INDEX)
        version = idx["version"]
        if ctx.cursor.get("version") == version and ctx.since is None:
            ctx.log.info("version %s already loaded", version)
            return
        exported = utc(idx.get("updated_at") or idx["last_export"])
        url = next(r["url"] for r in idx["resources"] if r["name"] == "maritime.csv")
        df = pd.read_csv(io.BytesIO(ctx.http.bytes(url)), dtype=str).fillna("")
        first_seen = ctx.load_memo("first_seen", None, survives_reset=True)
        if first_seen is None:          # the cache folder held it before the observed/ folder existed
            old = ctx.settings.cache_dir / self.name / "first_seen.json"
            first_seen = json.loads(old.read_text()) if old.exists() else {}
        today = exported.date().isoformat()
        for os_id in df.id:
            first_seen.setdefault(os_id, today)

        subject = {}
        vessels = df[(df.type == "VESSEL") & df.imo.str.match(IMO)]
        for imo, g in vessels.groupby("imo", sort=True):
            names = Counter(g.caption)
            name = names.most_common(1)[0][0]
            aliases = sorted({*g.caption, *[a for v in g.aliases for a in split(v)]} - {name})
            flag = next((iso2(f) for f in g.flag if iso2(f)), None)
            mmsi = next((m for m in g.mmsi if re.fullmatch(r"\d{9}", m)), None)
            known = utc(min(first_seen[i] for i in g.id))
            yield entity(f"ent:imo:{imo}", "Vessel", name, src, known, "published", aliases=aliases,
                         attributes=attributes(flag=flag, mmsi=mmsi))
            for i in g.id:
                subject[i] = f"ent:imo:{imo}"
        for r in df[~df.id.isin(subject.keys())].itertuples(index=False):
            kind = "Vessel" if r.type == "VESSEL" else "Organization"
            eid = f"ent:opensanctions:{r.id}"
            yield entity(eid, kind, r.caption or r.id, src, utc(first_seen[r.id]), "published",
                         aliases=split(r.aliases), attributes=attributes(flag=iso2(r.flag), mmsi=r.mmsi if re.fullmatch(r"\d{9}", r.mmsi) else None)
                         if kind == "Vessel" else None)
            subject[r.id] = eid
        for r in df.sort_values("id").itertuples(index=False):
            flag = iso2(r.flag)
            yield event(f"evt:opensanctions:{r.id}", "RISK.LIST.ENTRY", src, time_spec(first_seen[r.id], "day"),
                        place(admin(flag)) if flag else {"unplaced": True}, max(exported, utc(first_seen[r.id])), "published",
                        participants=[participant(subject[r.id], "subject")],
                        attributes=attributes(risk_tags=split(r.risk), datasets=split(r.datasets)[:100],
                                              countries=split(r.countries)[:50], listed_name=r.caption[:500] or None,
                                              opensanctions_id=r.id),
                        evidence={"url": r.url} if r.url else None)
        yield Checkpoint({"version": version, "rows": int(len(df))})
        ctx.save_memo("first_seen", first_seen, survives_reset=True)
