"""Global Terrorism Database, 1970-1994 release (gtd_70to94_0617dist.xlsx), the file round 2 used.

GTD is published by START under its terms of use; set GTD_URL to the copy you obtained from START. The default is the
public mirror round 2 read, and the feeder refuses a file whose SHA-256 differs from round 2's unless
--set sha256=any is given."""
from __future__ import annotations

import io
from datetime import date, timedelta

import pandas as pd

from ..base import Checkpoint, Feeder, register
from ..fetch import sha256
from ..geo import country_iso2
from ..sil import admin, attributes, entity, event, finite, measures, participant, place, point, slug, text, time_spec, utc

MIRROR = "https://raw.githubusercontent.com/BeatriceVince/GTD_prediction_model/master/gtd_70to94_0617dist.xlsx"
SHA256 = "48b4a31b0753d7c1d0c3f025a5106e04b211e5b5f75f336abce060637292b7c0"
TYPES = {1: "SEC.ATTACK.ASSASSINATION", 2: "SEC.ATTACK.ARMED_ASSAULT", 3: "SEC.ATTACK.BOMBING", 4: "SEC.ATTACK.HIJACKING",
         5: "SEC.ATTACK.HOSTAGE_BARRICADE", 6: "SEC.ATTACK.KIDNAPPING", 7: "SEC.ATTACK.FACILITY", 8: "SEC.ATTACK.UNARMED_ASSAULT",
         9: "SEC.ATTACK.UNKNOWN"}
UNCERTAINTY_KM = {1: 5.0, 2: 50.0, 3: 50.0, 4: 200.0}     # GTD 'specificity'; 5 = no usable coordinates


def _s(v, n=300):
    return str(v).strip()[:n] if isinstance(v, str) and v.strip() else None


@register
class GtdFeeder(Feeder):
    name = "gtd"
    package = "gtd"
    title = "Global Terrorism Database 1970-1994 (START)"

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:start:gtd")
        url = ctx.settings.key("GTD_URL") or MIRROR
        raw = ctx.http.bytes(url, cache_name="gtd_70to94_0617dist.xlsx")
        digest = sha256(raw)
        if digest != SHA256 and ctx.options.get("sha256") != "any":
            raise RuntimeError(f"GTD file hash {digest} differs from the release round 2 used; check the file or pass --set sha256=any")
        df = pd.read_excel(io.BytesIO(raw), engine="openpyxl")
        for r in df.sort_values("eventid").itertuples(index=False):
            y, m, d = int(r.iyear), int(r.imonth or 0), int(r.iday or 0)
            precision = "day" if m and d else "month" if m else "year"
            try:
                start = date(y, m or 1, d or 1)
            except ValueError:
                start, precision = date(y, m or 1, 1), "month" if m else "year"
            if (ctx.since and start < ctx.since) or (ctx.until and start > ctx.until):
                continue
            end = (start + timedelta(days=1) if precision == "day" else
                   date(y + (start.month == 12), start.month % 12 + 1, 1) if precision == "month" else date(y + 1, 1, 1))
            spec = int(r.specificity) if finite(r.specificity) is not None else None
            lon, lat = finite(r.longitude), finite(r.latitude)
            iso2 = country_iso2(_s(r.country_txt))
            pl = place(point(lon, lat, UNCERTAINTY_KM[spec]) if spec in UNCERTAINTY_KM and lon is not None and lat is not None else None,
                       admin(iso2, name=_s(r.city) or _s(r.provstate)) if iso2 else None)
            if not pl:
                continue
            parts = []
            group = _s(r.gname, 200)
            if group and group.lower() != "unknown":
                gid = f"ent:gtd-group:{slug(group, 100)}"
                if ctx.once(gid):
                    yield entity(gid, "ArmedActor", group, src, utc(end), "proxy_event_end")
                parts.append(participant(gid, "actor"))
            kind = TYPES.get(int(r.attacktype1) if finite(r.attacktype1) is not None else 9, "SEC.ATTACK.UNKNOWN")
            yield event(f"evt:gtd:{int(r.eventid)}", kind, src, time_spec(start, precision), pl, utc(end), "proxy_event_end",
                        participants=parts or None,
                        measures=measures(("killed", r.nkill, "count"), ("wounded", r.nwound, "count")),
                        text=text(None, _s(getattr(r, "summary", None), 20000)),
                        attributes=attributes(gtd_eventid=str(int(r.eventid)), target_type=_s(r.targtype1_txt, 100),
                                              weapon_type=_s(r.weaptype1_txt, 100), region=_s(r.region_txt, 100),
                                              success=bool(r.success) if finite(r.success) is not None else None,
                                              suicide=bool(r.suicide) if finite(r.suicide) is not None else None,
                                              specificity=spec))
        yield Checkpoint({"file_sha256": digest, "rows": int(len(df))})
