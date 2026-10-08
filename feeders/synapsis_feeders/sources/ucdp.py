"""UCDP GED: the annual release (zip of one CSV, about 400,000 events since 1989) and every monthly candidate file.

The downloads page is read on each run, so a new annual version or a new candidate month is picked up without a code
change. Each file is sent once; its name goes into the cursor after its last batch is acknowledged."""
from __future__ import annotations

import io
import re
import zipfile
from datetime import timedelta

import pandas as pd

from ..base import Checkpoint, Feeder, register
from ..geo import country_iso2
from ..sil import admin, attributes, entity, event, finite, measures, participant, place, point, text, time_spec, utc

DOWNLOADS = "https://ucdp.uu.se/downloads/"
TYPES = {1: "SEC.CONFLICT.STATE_BASED", 2: "SEC.CONFLICT.NON_STATE", 3: "SEC.CONFLICT.ONE_SIDED"}
UNCERTAINTY_KM = {1: 1.0, 2: 25.0, 3: 50.0, 4: 150.0, 5: 300.0}      # where_prec 6 (country) and 7 (sea, air): no point
LAG = timedelta(days=28)


def _int(v):
    f = finite(v)
    return int(f) if f is not None else None


def _str(v, n=300):
    if v is None or (isinstance(v, float) and pd.isna(v)):
        return None
    s = str(v).strip()
    return s[:n] if s and s.lower() != "nan" else None


@register
class UcdpFeeder(Feeder):
    name = "ucdp"
    package = "ucdp"
    title = "UCDP Georeferenced Event Dataset (annual and candidate)"
    min_interval = 1.0

    def records(self):
        ctx = self.ctx
        page = ctx.http.get(DOWNLOADS).text
        annual = sorted(set(re.findall(r"https://ucdp\.uu\.se/downloads/ged/ged(\d+)-csv\.zip", page)), key=int)
        candidates = sorted(set(re.findall(r"https://ucdp\.uu\.se/downloads/candidateged/(GEDEvent_v[\w.]+?\.csv)", page)))
        done = set(ctx.cursor.get("files", []))
        files = []
        if annual and ctx.options.get("annual", "yes") != "no":
            files.append(("src:ucdp:ged", f"ged{annual[-1]}-csv.zip", f"https://ucdp.uu.se/downloads/ged/ged{annual[-1]}-csv.zip", annual[-1]))
        if ctx.options.get("candidates", "yes") != "no":
            files += [("src:ucdp:candidate", c, f"https://ucdp.uu.se/downloads/candidateged/{c}", c[len("GEDEvent_v"):-4]) for c in candidates]
        for source_id, fname, url, release in files:
            if fname in done and ctx.since is None:
                continue
            ctx.log.info("reading %s", fname)
            raw = ctx.http.bytes(url, cache_name=fname)
            if fname.endswith(".zip"):
                z = zipfile.ZipFile(io.BytesIO(raw))
                df = pd.read_csv(z.open([n for n in z.namelist() if n.endswith(".csv")][0]), low_memory=False)
            else:
                df = pd.read_csv(io.BytesIO(raw), low_memory=False)
            yield from self._file(df, ctx.source(source_id), url, release)
            done.add(fname)
            yield Checkpoint({"files": sorted(done)})

    def _file(self, df: pd.DataFrame, src: dict, url: str, release: str):
        ctx = self.ctx
        df = df.copy()
        df["t0"] = pd.to_datetime(df["date_start"], errors="coerce", utc=True)
        df["t1"] = pd.to_datetime(df["date_end"], errors="coerce", utc=True).fillna(df["t0"])
        df = df.dropna(subset=["t0"])
        if ctx.since is not None:
            df = df[df.t0.dt.date >= ctx.since]
        if ctx.until is not None:
            df = df[df.t0.dt.date <= ctx.until]
        df = df.sort_values(["t0", "id"])
        # an actor is known from the first event that names it (known_at of that event)
        first_known = {}
        for side_id, side_name, t1 in zip(pd.concat([df.side_a_new_id, df.side_b_new_id]), pd.concat([df.side_a, df.side_b]),
                                          pd.concat([df.t1, df.t1])):
            if pd.notna(side_id):
                k = int(side_id)
                if k not in first_known or t1 < first_known[k][0]:
                    first_known[k] = (t1, str(side_name))
        for r in df.itertuples(index=False):
            vtype = TYPES.get(_int(r.type_of_violence))
            if vtype is None:
                continue
            start, end = r.t0.to_pydatetime(), r.t1.to_pydatetime()
            known = end + LAG
            parts = []
            sides = [("side_a", r.side_a_new_id)] + ([("side_b", r.side_b_new_id)] if vtype != "SEC.CONFLICT.ONE_SIDED" else [])
            for role, sid in sides:
                if pd.isna(sid):
                    continue
                aid = f"ent:ucdp-actor:{int(sid)}"
                if ctx.once(aid):
                    t_first, name = first_known[int(sid)]
                    yield entity(aid, "ArmedActor", name, src, t_first.to_pydatetime() + LAG, "proxy_publication_lag",
                                 attributes=None)
                parts.append(participant(aid, role))
            prec = _int(r.where_prec)
            lon, lat = finite(r.longitude), finite(r.latitude)
            iso2 = country_iso2(_str(r.country))
            pl = place(point(lon, lat, UNCERTAINTY_KM[prec]) if prec in UNCERTAINTY_KM and lon is not None and lat is not None else None,
                       admin(iso2, name=_str(r.adm_2) or _str(r.adm_1)) if iso2 else None)
            yield event(
                f"evt:ucdp:{int(r.id)}", vtype, src, time_spec(start, "day", end + timedelta(days=1) if end.date() > start.date() else None),
                pl or {"unplaced": True}, known, "proxy_publication_lag",
                participants=parts or None,
                measures=measures(("deaths_best", r.best, "count"), ("deaths_low", r.low, "count"), ("deaths_high", r.high, "count"),
                                  ("deaths_civilians", r.deaths_civilians, "count"), ("deaths_a", r.deaths_a, "count"),
                                  ("deaths_b", r.deaths_b, "count"), ("deaths_unknown", r.deaths_unknown, "count")),
                text=text(_str(r.source_headline, 1000)),
                attributes=attributes(conflict_name=_str(r.conflict_name), dyad_name=_str(r.dyad_name),
                                      where_description=_str(r.where_description, 1000), adm_1=_str(r.adm_1), adm_2=_str(r.adm_2),
                                      where_prec=prec, date_prec=_int(r.date_prec), event_clarity=_int(r.event_clarity),
                                      number_of_sources=_int(r.number_of_sources), code_status=_str(r.code_status, 60),
                                      release=release),
                evidence={"url": url, "ref": f"id {int(r.id)}"})
