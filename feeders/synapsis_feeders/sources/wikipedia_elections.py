"""US presidential state polls and results from English Wikipedia (2016, 2020, 2024 by default).

Pages are read through the MediaWiki parse API. Polls: the statewide-polling page for 2016 and 2024, and the 51
per-state pages for 2020 (where that year keeps its tables). Results: each election's main page. The table parsers
are round 3's (case_f6), kept NaN-safe; a table without both main candidates is skipped, never guessed."""
from __future__ import annotations

import io
import re
from datetime import timedelta

import pandas as pd
from bs4 import BeautifulSoup

from ..base import Checkpoint, Feeder, register
from ..geo import US_STATES, us_subdivision
from ..sil import admin, attributes, entity, event, measures, participant, slug, time_spec, utc

API = "https://en.wikipedia.org/w/api.php"
STATES = [s for s in US_STATES if s not in ("Puerto Rico", "Guam", "Virgin Islands", "American Samoa", "Northern Mariana Islands")]
CANDIDATES = {2016: ("Trump", "Clinton"), 2020: ("Trump", "Biden"), 2024: ("Trump", "Harris")}
ELECTION_DAY = {2016: "2016-11-08", 2020: "2020-11-03", 2024: "2024-11-05"}
MON = {m: i for i, m in enumerate(["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"], 1)}


def end_date(s, year):
    if not isinstance(s, str):
        return None
    cur, last = None, None
    for mo, d1, d2 in re.findall(r"([A-Z][a-z]{2,8})\.?\s+(\d{1,2})|[–\-]\s*(\d{1,2})\b", s):
        if mo and mo[:3].lower() in MON:
            cur = MON[mo[:3].lower()]
            last = (cur, int(d1))
        elif d2 and cur:
            last = (cur, int(d2))
    years = re.findall(r"(20\d\d)", s)
    y = int(years[-1]) if years else year
    try:
        return pd.Timestamp(y, last[0], last[1]) if last else None
    except ValueError:
        return None


def pct(series):
    return pd.to_numeric(series.fillna("").astype(str).str.extract(r"(\d+(?:\.\d+)?)")[0], errors="coerce")


def flat_columns(df):
    return [" ".join(dict.fromkeys(map(str, c))) if isinstance(c, tuple) else str(c) for c in df.columns]


def parse_poll_table(tab, year):
    try:
        df = pd.read_html(io.StringIO(str(tab)))[0]
    except ValueError:
        return None
    cols = flat_columns(df)
    df.columns = cols
    if any("aggregation" in c.lower() or "updated" in c.lower() for c in cols):
        return None                                   # polling averages, not polls
    date_cols = [c for c in cols if "date" in c.lower()]
    if not date_cols:
        return None
    rep, dem = CANDIDATES[year]
    rc, dc = [c for c in cols if rep in c], [c for c in cols if dem in c]
    if rc and dc:
        R, D = df[rc[0]], df[dc[0]]
    elif "Democrat" in cols and "Republican" in cols:
        df = df[df["Democrat"].fillna("").astype(str).str.contains(dem) & df["Republican"].fillna("").astype(str).str.contains(rep)]
        R, D = df[cols[cols.index("Republican") + 1]], df[cols[cols.index("Democrat") + 1]]
    else:
        return None
    out = pd.DataFrame({"end": df[date_cols[0]].fillna("").astype(str).map(lambda x: end_date(x, year)).values,
                        "R": pct(R).values, "D": pct(D).values,
                        "pollster": df[cols[0]].fillna("").astype(str).str.replace(r"\[.*?\]", "", regex=True).str.strip().values})
    return out.dropna()


def polls_from(html, year, state=None):
    soup = BeautifulSoup(html, "lxml")
    rows, cur = [], state
    for el in soup.select("h2, h3, h4, table.wikitable"):
        if el.name in ("h2", "h3", "h4") and state is None:
            t = re.sub(r"\[.*?\]", "", el.get_text(" ", strip=True)).strip()
            if el.name == "h2":
                cur = t if t in STATES else None
            elif "district" in t.lower():
                cur = None if cur and t != cur else cur
        elif el.name == "table" and cur:
            p = parse_poll_table(el, year)
            if p is not None and len(p):
                rows.append(p.assign(state=cur))
    return pd.concat(rows) if rows else pd.DataFrame(columns=["end", "R", "D", "pollster", "state"])


def results_from(html, year):
    soup = BeautifulSoup(html, "lxml")
    rep, dem = CANDIDATES[year]
    for t in soup.select("table.wikitable"):
        txt = t.get_text()
        if "Wyoming" not in txt or "Alabama" not in txt:
            continue
        df = pd.read_html(io.StringIO(str(t)))[0]
        cols = flat_columns(df)
        df.columns = cols
        rc = [c for c in cols if rep in c and "%" in c]
        dc = [c for c in cols if dem in c and "%" in c]
        if not (rc and dc):
            continue
        names = df[cols[0]].fillna("").astype(str).str.replace(r"\[.*?\]|†|\*", "", regex=True).str.strip()
        names = names.replace({"D.C.": "District of Columbia", "Washington, D.C.": "District of Columbia", "DC": "District of Columbia"})
        state = names.map(lambda x: next((S for S in STATES if x == S or (x.startswith(S) and "at-l" in x.lower())), None))
        r = pd.DataFrame({"state": state, "R": pct(df[rc[0]]), "D": pct(df[dc[0]])}).dropna().drop_duplicates("state")
        if len(r) >= 45:
            return r
    return pd.DataFrame(columns=["state", "R", "D"])


@register
class WikipediaElectionsFeeder(Feeder):
    name = "wikipedia-elections"
    package = "wikipedia-elections"
    title = "US presidential state polls and results (Wikipedia)"
    min_interval = 0.7

    def page(self, title: str) -> str:
        body = self.ctx.http.json(API, params={"action": "parse", "page": title, "prop": "text|revid", "format": "json",
                                               "formatversion": 2, "redirects": 1})
        if "error" in body:
            raise RuntimeError(f"Wikipedia: {title}: {body['error'].get('info')}")
        self.revisions[title] = body["parse"]["revid"]
        return body["parse"]["text"]

    def records(self):
        ctx = self.ctx
        self.revisions = {}
        polls_src, results_src = ctx.source("src:wikipedia:polls"), ctx.source("src:wikipedia:results")
        years = sorted(int(y) for y in ctx.options.get("years", "2016,2020,2024").split(","))
        names: dict[str, str] = ctx.load_memo("pollster_names", {})
        loaded = set(ctx.cursor.get("years_loaded", []))
        for year in years:
            rep, dem = CANDIDATES[year]
            eday = utc(ELECTION_DAY[year])
            if year == 2020:
                frames = []
                for st in STATES:
                    name = ("the_" if st == "District of Columbia" else "") + st.replace(" ", "_")
                    frames.append(polls_from(self.page(f"2020_United_States_presidential_election_in_{name}"), year, state=st))
                polls = pd.concat(frames)
            else:
                polls = polls_from(self.page(f"Statewide_opinion_polling_for_the_{year}_United_States_presidential_election"), year)
            polls = polls[(polls.end < eday.replace(tzinfo=None)) & polls.R.between(1, 99) & polls.D.between(1, 99)]
            # Oldest first, so a pollster entity is known from its first poll; a total, stable order, so two polls by
            # one pollster in one state on one day get the same -2 suffix on every run.
            polls = polls.drop_duplicates(["state", "pollster", "end", "R", "D"]).sort_values(
                ["end", "state", "pollster", "R", "D"], kind="mergesort")
            seen_keys: dict[str, int] = {}
            for r in polls.itertuples(index=False):
                sub = us_subdivision(r.state)
                end = utc(r.end.to_pydatetime())
                known = end + timedelta(days=polls_src["publication_lag_days"])
                if known > ctx.started_at:
                    continue
                yield from self._state_entity(sub, r.state, polls_src)
                pid = f"ent:pollster:{slug(r.pollster, 80)}"
                if ctx.once(f"{pid}"):
                    # Pages spell some pollsters differently ("HighGround", "Highground"); the first spelling stays,
                    # so re-reading another year's page never creates a version that only changes the spelling.
                    name = names.setdefault(pid, r.pollster[:300])
                    yield entity(pid, "Pollster", name, polls_src, known, "proxy_publication_lag")
                base = f"poll/{year}/{sub}/{slug(r.pollster, 60)}/{end:%Y%m%d}"
                n = seen_keys[base] = seen_keys.get(base, 0) + 1
                yield event(f"evt:wikipedia-elections:{base}" + (f"-{n}" if n > 1 else ""), "POL.POLL.STATE", polls_src,
                            time_spec(end, "day"), admin("US", sub), known, "proxy_publication_lag",
                            participants=[participant(pid, "publisher"), participant(f"ent:subdivision:{sub}", "about")],
                            measures=measures(("margin", round(r.R - r.D, 2), "points"), ("share_r", r.R, "percent"), ("share_d", r.D, "percent")),
                            attributes=attributes(election_year=year, republican=rep, democrat=dem))
            results = results_from(self.page(f"{year}_United_States_presidential_election"), year)
            for r in results.itertuples(index=False):
                sub = us_subdivision(r.state)
                yield from self._state_entity(sub, r.state, results_src)
                yield event(f"evt:wikipedia-elections:result/{year}/{sub}", "POL.ELECTION.RESULT", results_src, time_spec(eday, "day"),
                            admin("US", sub), eday + timedelta(days=1), "proxy_publication_lag",
                            participants=[participant(f"ent:subdivision:{sub}", "about")],
                            measures=measures(("margin", round(r.R - r.D, 2), "points"), ("share_r", r.R, "percent"), ("share_d", r.D, "percent")),
                            attributes=attributes(election_year=year, republican=rep, democrat=dem))
            loaded.add(year)
            yield Checkpoint({"years_loaded": sorted(loaded), "revisions": dict(self.revisions)})
            ctx.save_memo("pollster_names", names)

    def _state_entity(self, sub: str, name: str, src: dict):
        if self.ctx.once(f"ent:subdivision:{sub}|{src['id']}"):
            yield entity(f"ent:subdivision:{sub}", "Subdivision", name, src, "2000-01-01T00:00:00Z", "published")
