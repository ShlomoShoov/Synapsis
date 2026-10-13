"""The engine's DuckDB file (volume engine-data): the first state of every event, its participants and measures."""
import json

import duckdb
import h3
import pandas as pd

from . import config

EVENT_COLUMNS = ["source", "id", "type", "state", "known_at", "t_start", "prec", "prank", "lon", "lat", "unc", "lspace",
                 "lpath", "lkey", "lkey2", "country", "subdivision", "p0", "has_m", "attrs"]
SCHEMA = {
    "ev_stage": """source VARCHAR, id VARCHAR, type VARCHAR, state INTEGER, known_at TIMESTAMP, t_start TIMESTAMP,
                   prec VARCHAR, prank INTEGER, lon DOUBLE, lat DOUBLE, unc DOUBLE, lspace VARCHAR, lpath VARCHAR,
                   lkey VARCHAR, lkey2 VARCHAR, country VARCHAR, subdivision VARCHAR, p0 VARCHAR, has_m BOOLEAN,
                   attrs VARCHAR""",
    "p_stage": "source VARCHAR, id VARCHAR, state INTEGER, ord INTEGER, entity VARCHAR, role VARCHAR",
    "m_stage": "source VARCHAR, id VARCHAR, state INTEGER, name VARCHAR, value DOUBLE, unit VARCHAR",
}
KEEP_ATTRIBUTES = ("POL.", "MKT.")   # attributes are kept only where an operator reads them (elections, markets)


def _parse(docs):
    ev, parts, meas = [], [], []
    for d in docs:
        r = d.get("record") or {}
        src, rid, st = d["source"], d["id"], int(d.get("state") or 1)
        t = r.get("time") or {}
        pl = r.get("place") or {}
        lg = pl.get("logical") or {}
        ad = pl.get("admin") or {}
        pt = pl.get("point")
        lon, lat = (pt[0], pt[1]) if isinstance(pt, list) and len(pt) == 2 else (None, None)
        space, path = lg.get("space"), [str(p) for p in (lg.get("path") or [])]
        lkey = (space + "/" + "/".join(path) if path else space) if space else None
        lkey2 = (space + "/" + "/".join(path[:2]) if path else space) if space else None
        ps = [p for p in (r.get("participants") or []) if isinstance(p, dict) and p.get("entity")]
        ms = [m for m in (r.get("measures") or []) if isinstance(m, dict) and isinstance(m.get("value"), (int, float))
              and not isinstance(m.get("value"), bool) and m.get("name")]
        typ = d.get("type") or r.get("type")
        attrs = r.get("attributes") if typ and typ.startswith(KEEP_ATTRIBUTES) else None
        prec = t.get("precision")
        ev.append((src, rid, typ, st, d.get("known_at"), t.get("start"), prec, config.RANK.get(prec, 99), lon, lat,
                   pl.get("uncertainty_km"), space, json.dumps(path) if space else None, lkey, lkey2, ad.get("country"),
                   ad.get("subdivision"), ps[0]["entity"] if ps else None, bool(ms),
                   json.dumps(attrs, sort_keys=True) if attrs else None))
        parts += [(src, rid, st, i, p["entity"], p.get("role")) for i, p in enumerate(ps)]
        meas += [(src, rid, st, m["name"], float(m["value"]), m.get("unit")) for m in ms]
    ev = pd.DataFrame(ev, columns=EVENT_COLUMNS)
    ev["t_start"] = pd.to_datetime(ev.t_start, utc=True, format="ISO8601", errors="coerce").dt.tz_convert(None)
    ev["known_at"] = pd.to_datetime(ev.known_at, errors="coerce")
    for c in ("lon", "lat", "unc"):
        ev[c] = pd.to_numeric(ev[c], errors="coerce")
    return (ev, pd.DataFrame(parts, columns=["source", "id", "state", "ord", "entity", "role"]),
            pd.DataFrame(meas, columns=["source", "id", "state", "name", "value", "unit"]))


class Workspace:
    def __init__(self, path=None):
        self.path = str(path or config.data_dir() / "workspace.duckdb")
        self.con = duckdb.connect(self.path)
        self.con.execute(f"SET temp_directory = '{config.data_dir() / 'duckdb-tmp'}'")
        self.con.execute("SET memory_limit = '2GB'")
        self.names = {}

    def load(self, chunks, names=()):
        """Replace the workspace with what MongoDB holds now: per (source, id) the first state."""
        for t, cols in SCHEMA.items():
            self.con.execute(f"CREATE OR REPLACE TABLE {t} ({cols})")
        n = 0
        for docs in chunks:
            for t, df in zip(SCHEMA, _parse(docs)):
                if len(df):
                    self.con.register("chunk_df", df)
                    self.con.execute(f"INSERT INTO {t} SELECT * FROM chunk_df")
                    self.con.unregister("chunk_df")
            n += len(docs)
        self.con.execute("""CREATE OR REPLACE TABLE events AS SELECT * EXCLUDE (rn) FROM (
            SELECT *, row_number() OVER (PARTITION BY source, id ORDER BY state) rn FROM ev_stage) WHERE rn = 1""")
        self.con.execute("""CREATE OR REPLACE TABLE participants AS
            SELECT p.source, p.id, p.ord, p.entity, p.role FROM p_stage p JOIN events e USING (source, id, state)""")
        self.con.execute("""CREATE OR REPLACE TABLE measures AS
            SELECT m.source, m.id, m.name, m.value, m.unit FROM m_stage m JOIN events e USING (source, id, state)""")
        for t in SCHEMA:
            self.con.execute(f"DROP TABLE {t}")
        first = {}
        for rid, st, name in names:
            if name and (rid not in first or st < first[rid][0]):
                first[rid] = (st, name)
        self.con.execute("CREATE OR REPLACE TABLE entities (id VARCHAR, name VARCHAR)")
        if first:
            df = pd.DataFrame([(k, v[1]) for k, v in sorted(first.items())], columns=["id", "name"])
            self.con.register("names_df", df)
            self.con.execute("INSERT INTO entities SELECT * FROM names_df")
            self.con.unregister("names_df")
        self.con.execute("CHECKPOINT")
        return n

    def name(self, entity_id):
        if not self.names:
            self.names = dict(self.con.execute("SELECT id, name FROM entities").fetchall())
        return self.names.get(entity_id)

    def counts(self):
        return dict(self.con.execute("SELECT source, count(*) FROM events GROUP BY 1 ORDER BY 1").fetchall())

    def occurrences(self, source, types, bucket, subject, resolution=None, as_of=None):
        """Events that fill a bucket (precision = the bucket or finer), with their bucket and subject, ordered by id."""
        q = (f"SELECT id, known_at, t_start, lon, lat, unc, lkey2, p0 FROM events WHERE source = ? AND type IN "
             f"({', '.join('?' * len(types))}) AND prank <= ? AND t_start IS NOT NULL AND known_at IS NOT NULL")
        args = [source, *types, config.RANK[bucket]]
        if as_of is not None:
            q += " AND known_at <= ?"
            args.append(as_of)
        df = self.con.execute(q + " ORDER BY id", args).df()
        df["bucket"] = config.bucket_start(df.t_start, bucket)
        if subject == "cell":
            ok = (df.unc < config.EDGE_KM[resolution]) & df.lat.between(-90, 90) & df.lon.between(-180, 180)
            df["subject"] = None
            df.loc[ok, "subject"] = [h3.latlng_to_cell(a, b, resolution) for a, b in zip(df.lat[ok], df.lon[ok])]
        else:
            df["subject"] = df["lkey2" if subject == "logical" else "p0"]
        return df.loc[df.subject.notna(), ["id", "known_at", "bucket", "subject"]].reset_index(drop=True)

    def measure_names(self, source, types):
        return [r[0] for r in self.con.execute(
            f"SELECT DISTINCT m.name FROM measures m JOIN events e USING (source, id) WHERE e.source = ? AND e.type IN "
            f"({', '.join('?' * len(types))}) ORDER BY 1", [source, *types]).fetchall()]

    def measurement_rows(self, source, types, bucket, as_of=None, name=None):
        """Measurement records that fill a bucket, with their subject (participant entity, else logical path, else
        admin subdivision, else country), its kind, and one row per measure."""
        q = (f"""SELECT e.id, e.known_at, e.t_start, e.prec, coalesce(e.p0, e.lkey, e.subdivision, e.country) subject,
                       CASE WHEN e.p0 IS NOT NULL THEN 'entity' WHEN e.lkey IS NOT NULL THEN 'logical'
                            WHEN e.subdivision IS NOT NULL THEN 'subdivision' ELSE 'country' END skind,
                       m.name, m.value FROM events e JOIN measures m USING (source, id)
                 WHERE e.source = ? AND e.type IN ({', '.join('?' * len(types))}) AND e.prank <= ?
                   AND e.t_start IS NOT NULL AND e.known_at IS NOT NULL""")
        args = [source, *types, config.RANK[bucket]]
        if name is not None:
            q += " AND m.name = ?"
            args.append(name)
        if as_of is not None:
            q += " AND e.known_at <= ?"
            args.append(as_of)
        df = self.con.execute(q + " ORDER BY e.id, m.name", args).df()
        return df[df.subject.notna()].reset_index(drop=True)

    def wide(self, source, types, names, as_of=None):
        """One row per record: id, subject (first participant), known_at, t_start and the named measures."""
        cols = ", ".join(f"max(CASE WHEN m.name = '{n}' THEN m.value END) AS \"{n}\"" for n in names)
        q = (f"""SELECT e.id, e.p0 subject, e.known_at, e.t_start, {cols} FROM events e
                 LEFT JOIN measures m USING (source, id)
                 WHERE e.source = ? AND e.type IN ({', '.join('?' * len(types))}) AND e.p0 IS NOT NULL
                   AND e.t_start IS NOT NULL AND e.known_at IS NOT NULL""")
        args = [source, *types]
        if as_of is not None:
            q += " AND e.known_at <= ?"
            args.append(as_of)
        return self.con.execute(q + " GROUP BY e.id, e.p0, e.known_at, e.t_start ORDER BY e.id", args).df()

    def of_type(self, typ, as_of=None):
        """id, subject (first participant), time and known_at of every event of a type (any source)."""
        q = "SELECT id, p0 subject, t_start, known_at FROM events WHERE type = ? AND t_start IS NOT NULL"
        args = [typ]
        if as_of is not None:
            q += " AND known_at <= ?"
            args.append(as_of)
        return self.con.execute(q + " ORDER BY id", args).df()

    def max_known(self, source):
        return self.con.execute("SELECT max(known_at) FROM events WHERE source = ?", [source]).fetchone()[0]
