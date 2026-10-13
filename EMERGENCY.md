# EMERGENCY.md: the only guide until the product ships

Written 13 Oct 2026, about 15:45. **About 6 hours left.** This file replaces every earlier document, plan and coding
rule. History, if ever needed: `docs/HISTORY.md`. Archived originals: `~/synapsis-docs-archive-2026-10-13/` (outside
the repo). Update this file whenever a task is done or a decision changes. Keep it short.

## 0. Emergency rules
1. **A working, clear product beats everything.** Anything that can delay us is deleted, even if it is already built.
2. **The API is minimal; the UI does the rest** (labels, grouping, trees, layer choice, colours, story presets).
3. **No new abstractions.** Plain code that works. No summaries on every method, no interface per class, no ceremony.
   Tests only where they save time.
4. **One branch: `dev`.** Small commits, push often, merge immediately. Nothing waits on a review.
5. Still kept, because breaking them costs more than it saves: keys only in `.env` files; never invent data values.

## 1. The goal (in this order)
1. **A basic API, with the UI completing everything else.**
2. **A basic analysis engine that starts streaming conclusions** into the system.
3. **Everything verified end to end**: feeders → API → MongoDB → Kafka → Elasticsearch + Neo4j → API → UI, plus the
   engine → API → the same path → UI.

## 2. Where we stand (checked 13 Oct, 15:30)

| Part | State |
| --- | --- |
| Feeders (21 sources, Python) | Built and run before. Packages in `feeders/packages/`. |
| Ingest: `POST /ingest`, `GET /records/{id}`, `GET /catalog`, `GET /health` | Built, merged in `dev`. |
| MongoDB `synapsis_v2` | Running, **8,606 records** (KEV in full, about 200 from each other source). |
| Kafka relay, Elasticsearch indexer, Neo4j projector, Setup | Built, merged in `dev`. **Not running right now.** ES holds 7,819 documents, so they did run. |
| Elasticsearch | Running: `synapsis-records` 7,819 docs, `synapsis-ingest-log` 31. |
| Neo4j, Kafka | Running (healthy). |
| `dotnet build` of the solution | Succeeds, 0 warnings (with the uncommitted work below). |
| `/analyze/map` cells layer (Elasticsearch) | Committed and merged into `dev` (Stage 1, 16:15). Replaced in Stage 2 by `/map/cells`. |
| `/analyze/map` points layer | Answers 501. |
| `/analyze/graph` | **Built by Ytshak** on `origin/feature/analyze-graph-endpoint` (commit `dbe71c8`, 15:12): Neo4j (relations, events, entities that appear together) + MongoDB (verifying the records, building summaries), grouped and paged. About 830 lines of code + 1,050 lines of tests. Merges cleanly with `ui/analyze` HEAD; it will conflict only with the uncommitted `AnalyzeService.cs`. Not yet run against the stack. |
| Read mock `Synapsis.MockApi` (port 8081) | Running. **To be deleted** (§7). |
| UI: home, Observe | Built. Observe reads the ingest log from Elasticsearch directly. |
| UI: Analyze | Part 1 on `ui/analyze`: the 3D globe, the time window, cells and points drawn, reading **the mock**. Clicking does nothing yet. No search, no tree. **Bug:** the UI never sends `layer`, so the API (where `layer` is now required) answers 400. **Problem:** it opens on the last month, where the sample has nothing to show. |
| Engine | **Nothing.** |

## 3. The minimal API (the target)
The read API is six routes. List answers share one shape, `{ "total": N, "items": [ ... ] }`. A route never answers
two different things: one thing per route. Names are plain, with no `/analyze` prefix.

**Shared filters** (all optional): `from`, `to` (dates; event start time), `source` (repeat), `type` (repeat).
Nothing else: no `as_of`, `country`, `subdivision`, `cell`, `measure`, `entity_match`, `cursor`, `zoom` or `layer`.

| Route | Extra parameters | Answer | Source |
| --- | --- | --- | --- |
| `GET /map/cells` | `resolution` (2–5, chosen by the UI from the zoom), `bbox` (optional) | items `{cell, count, center, boundary}` | ES terms aggregation on `h3_r{res}` (already built; keep center/boundary, since the UI already reads them) |
| `GET /map/points` | `bbox`, `kind` (`event` by default; `conclusion` for the engine's sparks), `limit` (≤ 5000) | items = **Hit** | ES search, documents with a `location` |
| `GET /search` | `q` (optional: with no `q` it is a filtered list), `kind`, `logical` (a prefix), `entity`, `limit` | items = **Hit** | ES search: each word of `q` matched as a prefix on name, title, body, id (plain matching misses "log4j" in "Log4j2"); newest first |
| `GET /logical` | none beyond the shared filters | items `{path, count}`; the UI builds the tree by splitting on `/` | ES terms aggregation on `logical_prefixes` |
| `GET /graph?id=` | (the UI sends only `id`) | Ytshak's answer as built: `center` + `connections` groups | Neo4j + MongoDB |
| `GET /records/{id}` | — | each source's full record, as stored | MongoDB (already built) |

**Hit** (the one item shape in lists): `{id, kind, type, source, time, point?, title}`, where `title` = the record's
title, or else its name, or else nothing. The UI makes a label from `type` + `source` + `time` when there is no title.
Fields are copied from the ES document as they are: no `RecordSummary`, no labels on the server.

How the UI uses it: zoomed out, `/map/cells`; zoomed in, `/map/points`; a click opens `/records/{id}` + `/graph?id=`;
the search box calls `/search?q=`; a tree node calls `/search?logical=`; an entity's events come from `/search?entity=`
(this is where KEV appears: it has no points on the map).

## 4. The basic engine (the target)
One Python file, `src/python/synapsis_engine/engine.py` (dependency: `requests`; `scipy` is optional, the Poisson tail
fits in a few lines). It runs as a script, and becomes a Compose service only if there is time.

1. **Read** every event with a cell from ES (`kind=event`, `h3_r3` exists): `id, source, time_start, h3_r3`.
2. **Count** per (source, cell, ISO week starting Monday).
3. **Routine** per (source, cell) at week t: μ = (sum of earlier weekly counts + κ·g) / (earlier weeks + κ), with
   κ = 8 and g = the source's mean count per cell-week. Earlier weeks run from the source's first week.
4. **Surprise**: s = −log10 P(N ≥ n | μ), Poisson. **Spark** when s ≥ 3 and n ≥ 3.
5. **Write** each spark as a conclusion through `POST /ingest` (batches ≤ 5000, `producer: "synapsis-engine"`):
   ```json
   {"kind":"conclusion","id":"con:engine:spark:<source-key>:<cell>:<yyyy-mm-dd>","type":"spark",
    "source":"src:synapsis:engine","reliability":70,"reliability_basis":"model",
    "known_at":"<week end, …Z>","known_at_basis":"proxy_event_end",
    "about":["subject:h3:<cell>"],"statement":{"predicate":"anomaly","value":<s>},
    "premises":["<up to 50 event ids>"],"method":{"by":"system","name":"poisson-surprise","version":"1"},
    "confidence":<min(99, round(20·s))>,
    "text":{"title":"<n> <source> events in a week where <μ> were expected"}}
   ```
   Ids are deterministic, so a re-run answers `unchanged`.
6. **Indexer change** (`SearchDocumentMapper`, a few lines) so sparks reach the map: for a conclusion whose `about`
   holds `subject:h3:<cell>`, set that resolution's `h3_r*` field, `location` = the cell centre, and `time_start` =
   `known_at`. Then `/map/points?kind=conclusion` and `/search?kind=conclusion` show them with no new route.

Expected on the sample: the Rat Islands aftershocks (USGS, February 1965) and bursts of GTD attacks in 1970 should
spark. Not in scope: self-excitation, neighbours, peers, similar windows, forecasts, bias.

## 5. The UI (the target)
- Read the real API (`VITE_ANALYZE_API_URL=http://localhost:8080`). The mock is gone.
- The map: cells below zoom 6, points from zoom 6 up (fixes the `layer` bug); the routes are `/map/cells` and
  `/map/points`.
- **Story presets** instead of "the last month": buttons that set the window and the view, such as "Rat Islands
  earthquakes, Feb 1965", "Attacks in the US, 1970", "Conflict, Jan 1989", "News, Dec 2020", "Known exploited
  vulnerabilities (KEV)".
- **A click** on a point: a side panel with `/records/{id}` (as stored) and `/graph?id=` (connections; a click on a
  connection moves to it).
- **A search box** (`/search?q=`) with a result list; a click on a result opens the same panel and, when the result
  has a point, flies the map there.
- **A logical tree** (`/logical`, built in the UI); a click on a node lists `/search?logical=`.
- **A sparks layer**: `/map/points?kind=conclusion` in a distinct colour; the panel shows the statement, the
  confidence and the premises (each one clickable).
- Filters: source and type chips, sent as `source=` / `type=` on every call.

## 6. What remains (in order; start times from now)

| # | Task | Owner (proposed) | Time | Done when |
| --- | --- | --- | --- | --- |
| 1 | **Integrate**: commit the cells work; merge `ui/analyze` and `origin/feature/analyze-graph-endpoint` into `dev` (resolve `AnalyzeService.cs`); build; `up` the whole core stack | Shlomo | 0:00–0:30 | `dev` builds; `curl localhost:8080/health` OK; relay, indexer and projector running |
| 2 | **Minimal API** (§3): `/map/cells`, `/map/points`, `/search`, `/logical`; move the graph to `/graph`; delete the mock, `FilterParser`/`MapRequest` complexity and unused DTOs | Shlomo + Claude | 0:30–2:00 | the curl examples of §8 answer on real data |
| 3 | **The graph against the stack**: run `/graph?id=` on KEV, UCDP and GTD ids; fix what breaks | Ytshak | 0:30–1:15 | the connections answer for an event and for an entity |
| 4 | **The engine** (§4) + the indexer change | Ytshak (after 3) | 1:15–3:30 | sparks are in MongoDB and ES; `/map/points?kind=conclusion` shows them |
| 5 | **The UI** (§5), against the mock's shapes first, then the real API as soon as task 2 lands | UI agent (Claude) | 0:00–4:00 | every item of §5 works in the browser |
| 6 | **End to end** (§8 checklist), the demo script, update README.md (remove the mock section) | everyone | 4:00–5:00 | the checklist passes twice in a row |
| 7 | Buffer | — | 5:00–6:00 | — |

If time runs short, cut in this order: the sparks layer in the UI (show sparks only in search) → the logical tree →
the type chips → the engine's Compose service. **Never cut** tasks 1, 2, 3 or 6.

## 7. Deleted / not doing (do not bring back)
- `Synapsis.MockApi` (project, Compose service `mock-api`, its `.sln` entry, the README section), and the test that
  compares it with the API.
- The 10-route read plan, the shared filter language, `as_of` on read routes, paging cursors, facets, completion,
  meaning search, nearby, track, timeline counts, `omitted` reasons, server-side labels.
- `contracts/synapsis-api.yaml`: frozen, no longer updated. Swagger (`/swagger`) is the contract.
- Loading full data. Redis, MinIO, DuckDB. The engine beyond §4.
- All earlier documents and the coding skill (archived; see the top of this file).

## 8. Run and check

```bash
docker compose -f deploy/core/compose.yml up -d --build --wait     # setup, api 8080, relay, indexer, projector, ui 1897
curl -s localhost:8080/health
curl -s "localhost:8080/map/cells?resolution=2"
curl -s "localhost:8080/map/points?bbox=170,45,-170,55&from=1965-01-01&to=1965-04-01"
curl -s "localhost:8080/search?q=log4j2"
curl -s "localhost:8080/logical?source=src:cisa:kev"
curl -sG localhost:8080/graph --data-urlencode "id=ent:kev-product:apache/log4j2"
curl -s "localhost:8080/search?kind=conclusion"                     # the engine's sparks
python3 src/python/synapsis_engine/engine.py --es http://localhost:9200 --api http://localhost:8080
```
UI in development (from `src/ui`, no Node needed):
`docker run --rm -it -u $(id -u):$(id -g) -e HOME=/tmp -e VITE_ANALYZE_API_URL=http://localhost:8080 -v "$PWD":/ui -w /ui -p 5173:5173 node:24.21.0-alpine3.24 npm run dev -- --host 0.0.0.0`
→ http://localhost:5173/analyze. Deployed UI: http://localhost:1897.

**End-to-end checklist** (task 6):
1. The core stack is up; every service is healthy or has finished (setup).
2. One feeder run adds or re-sends records: ingest answers; ES and Neo4j counts follow (new records) or stay the same
   (`unchanged`).
3. The engine runs; sparks appear in `/search?kind=conclusion` and on the map.
4. In the UI: a story preset → cells → zoom to points → a click → the record and its graph → a click on a connection
   → search "log4j" → KEV results (Log4j2) → the tree → a spark and its premises.

## 9. Gotchas
- MongoDB needs `?replicaSet=rs0`. Never `pkill -f` with a pattern that can match your own shell.
- `CORE_NEO4J_PASSWORD` must be set in `deploy/core/.env`, or the stack will not start.
- The indexer needs the embedder (it vectorises text); it starts with the core stack.
- The sample's sources each sit in their own era: always open the map with a story preset, never "now".

## 10. API agent: stage log
- **Stage 1 (16:15)**: branch `emergency/minimal-api` = the carried work (map cells, this file, removed docs) +
  `origin/ui/analyze` (`c4144c6`) + `origin/feature/analyze-graph-endpoint` (`dbe71c8`) + `origin/dev`; one conflict
  (`AnalyzeService.cs`) resolved. Merged into `dev` and pushed. Core stack up and healthy (setup, api 8080, relay,
  indexer, projector, embedder, ui 1897). ES `synapsis-records`: 7,819 documents, 7,771 distinct ids. Neo4j: 5,539
  nodes; relationships PARTICIPATES 5,673, RELATED 787, ABOUT 198, PREMISE 198.
