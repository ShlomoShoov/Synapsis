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
**Superseded by §12 ("The read API, explained"), which describes what is built.** Kept for the history of the plan.

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
- **Stage 2.1**: `GET /map/cells` (§12). CORS also allows the UI dev server, http://localhost:5173.
- **Stage 2.2**: `GET /map/points` (§12).
- **Stage 2.3**: `GET /graph` (§12): one Cypher statement. Ytshak's `/analyze/graph` is still there until Stage 3.
- **Stage 2.4**: `GET /logical` (§12).

## 11. How the system looks now

**Services and ports** (`docker compose -f deploy/core/compose.yml up -d --build --wait`):

| Service | Port | What it does |
| --- | --- | --- |
| `mongo` | 27017 | MongoDB, the truth: collection `records` (every state of every record), database `synapsis_v2` |
| `elasticsearch` | 9200 | Search: index `synapsis-records` (one document per state of an entity, event or conclusion; relations are not in it), `synapsis-ingest-log` |
| `neo4j` | 7474 (browser), 7687 (bolt) | The graph: nodes `Entity`, `Event`, `Conclusion`; links `PARTICIPATES`, `RELATED`, `ABOUT`, `PREMISE` |
| `kafka` | 9094 (host) | Topic `ingest.accepted`: every new record state |
| `setup` | — | Creates the indexes, constraints and topic once, then exits |
| `api` | 8080 | C#: `POST /ingest`, `GET /records/{id}`, `GET /catalog`, `GET /health`, and the read routes of §12 |
| `kafka-relay` | — | MongoDB change stream → Kafka |
| `elasticsearch-indexer` | — | Kafka → embedder → Elasticsearch |
| `neo4j-projector` | — | Kafka → Neo4j |
| `embedder` | 8090 | Turns text into a vector of 512 numbers (`POST /embed`), for semantic search |
| `ui` | 1897 | The built UI. Development server: 5173 (see §8) |

**The path of a record**: a feeder sends it (`POST /ingest`) → the API checks it and stores a new state in MongoDB →
the relay sees the insert (change stream) and puts it on Kafka → the indexer writes a search document to
Elasticsearch, and the projector writes nodes and links to Neo4j → the read routes (§12) query Elasticsearch and Neo4j
→ the UI draws the answer.

**Look into each store by hand**:
- MongoDB: `docker exec -it synapsis-core-mongo-1 mongosh synapsis_v2 --quiet --eval 'db.records.countDocuments()'` (8,606 now)
- Elasticsearch: `curl -s localhost:9200/synapsis-records/_count` and any `_search` body from §12.
- Neo4j: `set -a; . deploy/core/.env; set +a; docker exec -it synapsis-core-neo4j-1 cypher-shell -u neo4j -p "$CORE_NEO4J_PASSWORD"`,
  or the browser at http://localhost:7474.
- Kafka: `docker exec synapsis-core-kafka-1 /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic ingest.accepted --from-beginning --max-messages 1`

**Words used below**:
- *document*: one entry in the Elasticsearch index. There is one per **state** of a record (a record re-sent with
  changed content gets state 2, a second document). So documents ≥ records.
- *aggregation*: a summary Elasticsearch computes over all matching documents instead of returning them (counts,
  groups). *terms aggregation*: groups documents by the value of a field; each group is a *bucket* with a `key` (the
  value) and a `doc_count` (its documents). *cardinality*: the number of distinct values of a field (here `id`, so it
  counts records, not states). It is exact up to about 3,000 distinct values and an estimate (within about 1 %) above.
- *H3*: a grid of hexagons covering the Earth, at several sizes (resolutions). Resolution 2 has 5,882 cells with an edge
  of about 158 km; 3: 60 km; 4: 23 km; 5: 8.5 km. A cell is named by a 15-character id such as `822d57fffffffff`.
  The indexer writes `h3_r2` ... `h3_r5` only when the point's uncertainty is smaller than the cell's edge.
- *bbox*: the map's box, `minLon,minLat,maxLon,maxLat`. When `minLon > maxLon` the box crosses the 180th meridian.
- *knn*: "k nearest neighbours": the k documents whose vectors are closest to the question's vector.
- *collapse on id*: Elasticsearch keeps one hit per record id (the first by the sort), so a record with 3 states is
  listed once.

**Which UI file calls which route**: filled in by Stage 4.

## 12. The read API, explained

Every route below answers on `http://localhost:8080` and passes the store's answer through: Elasticsearch documents
(`_source`) and buckets, Neo4j nodes and links. The UI does all the shaping. Code: `src/dotnet/Synapsis.Api/Controllers/ReadController.cs`
(the routes), `Repositories/SearchRepository.cs` (Elasticsearch), `Repositories/GraphRepository.cs` (Neo4j).

**Shared filters** (Elasticsearch routes, all optional; `SearchRepository.Filters`, `SearchRepository.cs:93`):

| Parameter | Type | Filters |
| --- | --- | --- |
| `from` | date or date-time (`1965-01-01`, `1965-01-01T12:00:00Z`; a date alone is midnight UTC) | `time_start >= from` |
| `to` | same | `time_start < to` (excluded) |
| `source` | text, repeatable (`source=a&source=b`) | any of them |
| `type` | text, repeatable | any of them |

A filter on `from`/`to` drops entities (they have no `time_start`). A wrong date or number answers 400.

### GET /map/cells

**Receives**:

| Parameter | Type | Default | |
| --- | --- | --- | --- |
| `resolution` | 2, 3, 4 or 5 | required (400 without it) | the H3 size; the UI chooses it from the zoom |
| `bbox` | `minLon,minLat,maxLon,maxLat` | none (the whole Earth) | only points inside; across 180° when `minLon > maxLon` |
| `kind` | `event`, `conclusion`, `entity` | `event` | |
| `from`, `to`, `source`, `type` | shared filters | | |

**Returns** the cells that hold matching documents, the most records first, at most 10,000:
```json
{ "records": 670, "sum_other_doc_count": 0,
  "buckets": [ { "key": "821e67fffffffff", "doc_count": 64, "records": { "value": 64 } },
               { "key": "826b6ffffffffff", "doc_count": 46, "records": { "value": 46 } } ] }
```
`records` = distinct records in all the cells; each bucket: `key` = the H3 cell, `doc_count` = its documents (states),
`records.value` = its distinct records (show this one). `sum_other_doc_count` = documents in cells beyond the
10,000 returned (0 = nothing cut). No centre or boundary: the UI draws the hexagon from the key with h3-js
(`cellToBoundary`, `cellToLatLng`).

**Sends to Elasticsearch** (`SearchRepository.MapCellsAsync`, `SearchRepository.cs:136`; the request
`resolution=3&bbox=170,45,-170,55&from=1965-01-01&to=1965-04-01`):
```bash
curl -s localhost:9200/synapsis-records/_search -H 'content-type: application/json' -d '{
  "size": 0,
  "query": { "bool": { "filter": [
    { "range": { "time_start": { "gte": "1965-01-01T00:00:00Z", "lt": "1965-04-01T00:00:00Z" } } },
    { "term": { "kind": "event" } },
    { "geo_bounding_box": { "location": { "top_left": { "lat": 55, "lon": 170 }, "bottom_right": { "lat": 45, "lon": -170 } } } },
    { "exists": { "field": "h3_r3" } } ] } },
  "aggs": {
    "cells": { "terms": { "field": "h3_r3", "size": 10000, "order": { "records": "desc" } },
               "aggs": { "records": { "cardinality": { "field": "id" } } } },
    "records": { "cardinality": { "field": "id" } } } }'
```
`"size": 0` = return no documents, only the aggregations. `geo_bounding_box` handles the 180th meridian itself: this
box gives 75 documents, the same as two boxes (170..180 and −180..−170) and the same as a count by hand.

**Counts**: distinct records (`cardinality` of `id`): exact up to about 3,000 per cell, an estimate above.

**Checked** (13 Oct, sample): `resolution=2` → records 670, 179 cells, the cells' records add up to 670; by hand
(Python over all 7,819 documents: distinct event ids with `h3_r2`) 670 in 179 cells; ES `_count` of the same filter
670 documents. `resolution=3&bbox=170,45,-170,55&from=1965-01-01&to=1965-04-01` (Rat Islands) → records 71, 15 cells;
by hand 71 in 15 cells.

**Limits at full scale**: 10,000 cells per answer (resolution 2 has 5,882 on the whole Earth; at 3–5 the UI must send a
bbox, or `sum_other_doc_count` > 0 says cells were cut). Ordering by `records` sorts every cell of the index's one
shard: fine at these sizes. Counts above 3,000 per cell are estimates.

### GET /map/points

**Receives**:

| Parameter | Type | Default | |
| --- | --- | --- | --- |
| `bbox` | `minLon,minLat,maxLon,maxLat` | required (400 without it) | across 180° when `minLon > maxLon` |
| `kind` | `event`, `conclusion`, `entity` | `event` | |
| `limit` | 1–5,000 | 5,000 (400 above) | documents returned |
| `from`, `to`, `source`, `type` | shared filters | | |

**Returns** the matching documents with a point in the box, one per record (its latest state), as stored in
Elasticsearch (`_source`; `text_vector` is never in it):
```json
{ "total": 71,
  "items": [ { "id": "evt:usgs:ushis2739", "record_key": "evt:usgs:ushis2739|src:usgs:comcat", "state": 1,
               "kind": "event", "type": "GEO.EARTHQUAKE", "source": "src:usgs:comcat",
               "known_at": "1965-01-30T05:37:21Z", "time_start": "1965-01-30T04:37:21Z", "time_precision": "second",
               "week": "1965-01-25T00:00:00Z", "location": [-179.67, 51.68],
               "h3_r2": "82225ffffffffff", "h3_r3": "83225bfffffffff", "logical_prefixes": [],
               "measures": { "magnitude": [5.6], "depth_km": [88.0] }, "entities": [] } ] }
```
`total` = distinct records that match (all of them, not only the ones returned); `items` holds at most `limit`. When
`total > items.length`, the list was cut: the UI should show cells instead. Fields a record does not have are absent
(`title`, `name`, `body`, `country`, `h3_r4`...).

**Sends to Elasticsearch** (`SearchRepository.ListAsync`, `SearchRepository.cs:160`; route `ReadController.cs:55`;
the request `bbox=170,45,-170,55&from=1965-01-01&to=1965-04-01`):
```bash
curl -s localhost:9200/synapsis-records/_search -H 'content-type: application/json' -d '{
  "size": 5000,
  "query": { "bool": { "filter": [
    { "range": { "time_start": { "gte": "1965-01-01T00:00:00Z", "lt": "1965-04-01T00:00:00Z" } } },
    { "term": { "kind": "event" } },
    { "geo_bounding_box": { "location": { "top_left": { "lat": 55, "lon": 170 }, "bottom_right": { "lat": 45, "lon": -170 } } } } ] } },
  "sort": [ { "state": "desc" } ],
  "collapse": { "field": "id" },
  "aggs": { "records": { "cardinality": { "field": "id" } } } }'
```
`collapse` keeps one document per `id`: the first by the sort, so the latest state. The total comes from the
`records` aggregation, not from `hits.total`: `hits.total` counts documents (states), and with `collapse` it would not
match the items (a deliberate change from the plan's `track_total_hits`).

**Counts**: distinct records (cardinality of `id`; exact up to about 3,000, an estimate above).

**Checked** (13 Oct, sample): Rat Islands (the request above) → total 71, 71 items, 71 distinct ids; by hand (Python
over all documents: events with a location in the box and `time_start` in the window) 71. Whole Earth
(`bbox=-180,-90,180,90`) → total 683, 683 items; ES `_count` of events with a location 683; by hand 683.
`limit=10` → total 683, 10 items. No bbox, or `limit=6000` → 400.

**Limits at full scale**: at most 5,000 items per answer, so the UI asks for points only when the cells in view hold
5,000 records or fewer. `collapse` with a large `size` costs more than a plain search (fine at 5,000). The total above
3,000 is an estimate, so near 5,000 a few points may be missing from a "complete" list: the UI compares `total` with
`items.length`.

### GET /graph

**Receives**: `id` (a record id, required; 400 without it), `limit` (1–1,000, default 200; 400 outside). No other
filter: the graph shows every link the node has.

**Returns** the node, its true number of links, and at most `limit` of them, each with the node at its other end.
Node properties are as the projector wrote them to Neo4j, plus `labels`; times are ISO text:
```json
{ "center": { "id": "ent:kev-product:apache/log4j2", "labels": ["Entity"], "name": "Log4j2", "type": "Product",
              "aliases": [], "sources": ["src:cisa:kev"], "known_at": "2021-12-11T00:00:00Z" },
  "total": 4,
  "links": [
    { "type": "ABOUT", "from": "con:kev-classes:apache/log4j2", "to": "ent:kev-product:apache/log4j2",
      "properties": { "known_at": "2026-10-02T00:00:00Z" },
      "node": { "id": "con:kev-classes:apache/log4j2", "labels": ["Conclusion"], "type": "classification",
                "predicate": "in_logical_space", "about": ["ent:kev-product:apache/log4j2"],
                "premises": ["evt:kev:CVE-2021-44228"], "confidence": 90, "known_at": "2026-10-02T00:00:00Z" } },
    { "type": "PARTICIPATES", "from": "ent:kev-product:apache/log4j2", "to": "evt:kev:CVE-2021-44228",
      "properties": { "role": "affected_product", "known_at": "2021-12-11T00:00:00Z" },
      "node": { "id": "evt:kev:CVE-2021-44228", "labels": ["Event"], "type": "CYB.VULN.KNOWN_EXPLOITED",
                "title": "Apache Log4j2 Remote Code Execution Vulnerability", "source": "src:cisa:kev",
                "time_start": "2021-12-10T00:00:00Z", "precision": "day", "known_at": "2021-12-11T00:00:00Z" } } ] }
```
`from`/`to` give the direction (an entity PARTICIPATES in an event; a conclusion is ABOUT a record and has a record
as PREMISE; RELATED links two entities and carries the relation's properties). `total` = all the node's links; when
`total > links.length` the list was cut. Unknown id, or an id that is not `ent:`/`evt:`/`con:` (relations are links,
not nodes): 404. The UI moves further by calling `/graph?id=` on a neighbour.

**Sends to Neo4j** one statement (`GraphRepository.NeighboursStatement`, `GraphRepository.cs:162`; run by
`NeighboursAsync`, `GraphRepository.cs:174`; route `ReadController.cs:75`). The label comes from the id's prefix
(`ent:` Entity, `evt:` Event, `con:` Conclusion), so Neo4j finds the node through its `id` index instead of scanning
every node:
```bash
set -a; . deploy/core/.env; set +a
docker exec synapsis-core-neo4j-1 cypher-shell -u neo4j -p "$CORE_NEO4J_PASSWORD" \
  "MATCH (c:Entity {id: 'ent:kev-product:apache/log4j2'})
   WITH c, COUNT { (c)--() } AS total
   OPTIONAL MATCH (c)-[r]-(n)
   WITH c, total, r, n LIMIT 200
   RETURN c {.*, labels: labels(c)} AS center, total,
          collect({type: type(r), from: startNode(r).id, to: endNode(r).id, properties: properties(r), node: n {.*, labels: labels(n)}}) AS links"
```
`COUNT { (c)--() }` is the node's degree, which Neo4j keeps with the node, so it costs the same for 4 links or 40,000.

**Counts**: links (relationships). No two nodes are linked twice in our data (checked: 0 pairs), so links =
neighbours. Exact.

**Checked** (13 Oct, sample): `ent:kev-product:apache/log4j2` → total 4, 4 links; Cypher `count(r)` 4.
`evt:kev:CVE-2021-44228` → total 4, 4 links; Cypher 4. The biggest hub, `ent:kev-vendor:microsoft` → total 460,
200 links; with `limit=1000`, 460 links. `ent:nope:x` → 404; no id → 400.

**Limits at full scale**: hubs (Microsoft with NVD loaded: tens of thousands of links) return only `limit` links,
chosen in no particular order (Neo4j's storage order); `total` stays true. Ordering them (newest first) would mean
sorting every link of the hub: add `ORDER BY n.known_at DESC` before `LIMIT` if needed.

### GET /logical

*Logical places* are paths a record is filed under when it has no map place, such as a vulnerability under
`cyber.vendor_product/Apache/Log4j2`. The indexer stores every prefix of each path in `logical_prefixes`
(`cyber.vendor_product`, `cyber.vendor_product/Apache`, `cyber.vendor_product/Apache/Log4j2`), so one level of the tree
is a group-by on that field.

**Receives**: `node` (optional: a key from a previous answer; none = the top level), and the shared filters `from`,
`to`, `source`, `type`. All kinds of record are counted.

**Returns** one level: the direct children of `node` (or the top-level spaces), the most documents first, at most
1,000, as Elasticsearch gives the buckets:
```json
{ "sum_other_doc_count": 0,
  "buckets": [ { "key": "cyber.vendor_product/Microsoft", "doc_count": 389, "records": { "value": 389 } },
               { "key": "cyber.vendor_product/Cisco", "doc_count": 100, "records": { "value": 100 } } ] }
```
`key` = the full path (send it back as `node` to open it, or as `/search?logical=` to list its records);
`doc_count` = documents (states); `records.value` = distinct records (show this one). `sum_other_doc_count` =
documents in children beyond the 1,000 returned (0 = nothing cut). A key with no children answers empty buckets.
(Changed from the plan, which returned a bare list: the object carries `sum_other_doc_count`, so nothing is cut
silently.)

**Sends to Elasticsearch** (`SearchRepository.LogicalAsync`, `SearchRepository.cs:223`; route `ReadController.cs:95`;
the request `node=cyber.vendor_product`):
```bash
curl -s localhost:9200/synapsis-records/_search -H 'content-type: application/json' -d '{
  "size": 0,
  "query": { "bool": { "filter": [] } },
  "aggs": { "level": {
    "terms": { "field": "logical_prefixes", "size": 1000, "include": "cyber\\.vendor_product\\/[^/]+" },
    "aggs": { "records": { "cardinality": { "field": "id" } } } } } }'
```
`include` is a regular expression (Lucene syntax) the key must match whole: the node, written literally (a backslash
before every character that is not a letter or digit), then `/`, then one segment with no `/`. With no node it is
`[^/]+` (keys with no `/`: the spaces).

**Counts**: `doc_count` documents (exact); `records.value` distinct records (cardinality; exact up to about 3,000).

**Checked** (13 Oct, sample): top level → 7 spaces (`cyber.vendor_product` 1,739, `social.forum` 386,
`health.region`, `health.signal`, `market.polymarket` 200 each, `code.repo` 198, `cyber.ransom_group` 176); by hand
(Python over all documents, distinct ids per key with no `/`) the same 7 with the same numbers.
`node=cyber.vendor_product` → 288 vendors, Microsoft 389, Cisco 100, Apple 95; by hand 288, 389, 100, 95. With
`size` 2 by hand, `sum_other_doc_count` = 1,250 = 1,739 − 389 − 100 (it counts only the children left out).

**Limits at full scale**: 1,000 children per level (NVD names tens of thousands of vendors: the vendor level will be
cut, and `sum_other_doc_count` says by how many documents). The terms aggregation reads every value of
`logical_prefixes` in the matching documents, then keeps the ones the regex accepts: with millions of documents this is
the slowest route; filters (`source`, `from`/`to`) make it faster.
