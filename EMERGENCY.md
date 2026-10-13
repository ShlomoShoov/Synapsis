# EMERGENCY.md: the only guide until the product ships

> **From now on (14 Oct 2026) everything is tested against ALL the records, not the local sample (8,606 records).**
> NVD about 1.2 M, FDIC about 549 k, UCDP about 434 k, EPSS about 14 M, KEV, GDELT and the rest: millions in all.
> Counting happens in the stores; every list carries the total that matched; no answer grows with the data. §13 lists
> what can be slow, approximate or cut at that scale.

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
| Read mock `Synapsis.MockApi` (port 8081) | **Deleted** (Stage 3, see §10). |
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
- **Stage 2.5**: `GET /search` (§12): words, meaning, or the filtered list. The `api` service now gets the embedder's address (`Embedder__Url`) and waits for it. **Stage 2 done.**
- **Stage 3**: deleted every old read class, the mock and their tests (list below). The `/analyze/*` routes are gone. Build: 0 warnings.

**Deleted in Stage 3** (lines per file):

| File | Lines |
| --- | --- |
| `src/dotnet/Synapsis.Api/Controllers/AnalyzeController.cs` | 67 |
| `src/dotnet/Synapsis.Api/Services/AnalyzeService.cs` | 421 |
| `src/dotnet/Synapsis.Api/Services/MapLayers.cs` | 63 |
| `src/dotnet/Synapsis.Api/Services/FilterParser.cs` | 138 |
| `src/dotnet/Synapsis.Api/Services/GraphSummaryBuilder.cs` | 245 |
| `src/dotnet/Synapsis.Api/Models/Results/CellCounts.cs` | 16 |
| `src/dotnet/Synapsis.Api/Models/Results/GraphConnectionCandidate.cs` | 34 |
| `src/dotnet/Synapsis.Api/Models/Dto/MapAnswer.cs` | 39 |
| `src/dotnet/Synapsis.Api/Models/Dto/MapCell.cs` | 22 |
| `src/dotnet/Synapsis.Api/Models/Dto/OmittedCount.cs` | 14 |
| `src/dotnet/Synapsis.Api/Models/Dto/RecordSummary.cs` | 68 |
| `src/dotnet/Synapsis.Api/Models/Dto/ConnectedRecord.cs` | 14 |
| `src/dotnet/Synapsis.Api/Models/Dto/ConnectionGroup.cs` | 32 |
| `src/dotnet/Synapsis.Api/Models/Dto/EntityLabel.cs` | 11 |
| `src/dotnet/Synapsis.Api/Models/Dto/GraphAnswer.cs` | 24 |
| `src/dotnet/Synapsis.Api/Models/Requests/AnalyzeFilters.cs` | 79 |
| `src/dotnet/Synapsis.Api/Models/Requests/MapRequest.cs` | 32 |
| `src/dotnet/Synapsis.Api/Models/Requests/GraphRequest.cs` | 29 |
| `src/dotnet/Synapsis.Api/Models/Requests/BoundingBox.cs` | 17 |
| `src/dotnet/Synapsis.Api/Models/Requests/MeasureCondition.cs` | 14 |
| `src/dotnet/Synapsis.Api/Repositories/SearchFilters.cs` | 165 |
| `src/dotnet/Synapsis.Api/Repositories/GraphFilters.cs` | 94 |
| `tests/dotnet/Synapsis.Tests/Integration/GraphQueryIntegrationTests.cs` | 460 |
| `tests/dotnet/Synapsis.Tests/Unit/GraphFilterTests.cs` | 85 |
| `tests/dotnet/Synapsis.Tests/Unit/GraphQueryRequestTests.cs` | 71 |
| `tests/dotnet/Synapsis.Tests/Unit/GraphAnswerContractTests.cs` | 104 |
| `src/dotnet/Synapsis.MockApi/` (36 files: project, sample `Sample/records.json`, services, models) | 3,987 |
| `tests/dotnet/Synapsis.Tests/Integration/MapCellsTests.cs` (local, git-ignored) | 95 |
| Old methods in `SearchRepository.cs` (cells with omitted counts), `GraphRepository.cs` (Ytshak's three connection queries), `RecordRepository.cs` (`AsOfManyAsync`, the Mongo check of graph candidates) | 104 + 136 + 47 |
| `deploy/core/compose.yml` (`mock-api`, profile `mock`), `README.md` (the mock section), `Synapsis.sln` (the mock entry) | 11 + 12 + 14 |

In all about 6,760 lines. The C# H3 code of the API went with `MapLayers.cs`; `Synapsis.Data/Services/LatticeKeys.cs`
stays (the indexer computes the `h3_r*` fields with it). `RecordAnswer` stays (`GET /records/{id}`).
`contracts/synapsis-api.yaml` no longer mentions the mock (it is frozen). Nothing references port 8081 except the UI's
`config.ts` and one UI test, changed in Stage 4.

Side effect found and fixed: Ytshak's branch commits files under `tests/dotnet/` (a git-ignored folder), and the
merge replaced the local `Synapsis.Tests.csproj` with his. The local tests lost `Mvc.Testing`, `Data`,
`Elasticsearch` and `Setup`. They are restored, the file is committed, and `dotnet test` gives 115 of 116. The one
failure is the setup test, which connects to Neo4j without the password (it failed before too).

- **Stage 4**: the UI's Analyze reads the new routes (the look is unchanged). Files: `analyze/config.ts` (default
  8080), `analyze/api/contract.ts`, `adapt.ts`, `client.ts`, `query.ts`, `model.ts`, `analyze/map/useEvents.ts`, `AnalyzePage.tsx`
  (no `as_of` sent: the API has none; the "seen as of" control stays but does nothing for the map), and their tests.
  New dependency: `h3-js` ^4.2.1. Checked in Docker: `npm install`, `build`, `lint` (0 warnings), `test` 112 of 112,
  Prettier clean. In the browser (dev server 5173, API 8080), `/analyze?from=1965-01-01&to=1965-04-01`: points
  draw (163 events, `/map/cells` then `/map/points`, both 200, no console error). Cells draw too: with the threshold
  set to 50 for the check, 56 records in view gave the warm cell lights and the "each light gathers..." key. The
  threshold is back at 5,000. The sample never holds more than 5,000 records in view, so cells appear only with full
  data.
- **Stage 5**: the full-data review (§13) and the rule at the top of this file. Nothing in the code changed.
- **Stage 6**: the prompt for the UI agent (§14). The file:line references in §12 are updated after Stage 3's deletions.
- **Final round, 1 (readable queries)**: each route is one method whose body (or Cypher) is written as it is sent, a
  raw JSON string with the values put in; the controller only reads parameters and calls it; the embedding moved into
  `SearchRepository.SearchAsync`. No behaviour change: the 23 check requests (§12 and the errors) give byte-identical
  answers before and after; quotes and backslashes in the input answer 200. One message changed: an embedder refusal
  still answers 400, now with "The embedder refused the question." instead of the embedder's own text.
- **Final round, 2 (a bare id)**: `id_key` in `SearchDocument`, `SearchDocumentMapper` (`IdKeyOf`), the mapping
  (`CreateRecordsIndexStep`) and the word search. Not re-indexed: live only after tonight's load from empty stores
  (§12, `/search`). Until then `q=CVE-2021-44228` still misses `evt:kev:CVE-2021-44228`. Tests: 119 of 120 (the old
  Neo4j-password setup test). The 23 check requests still give identical answers on the live index.
- **Final round, 3**: no `precision_threshold` (decided); counts above 3,000 stay estimates (§13, row 1).
- **Conclusions in the read routes (13 Oct, branch `feat/conclusions-in-read-routes`, not merged)**: a conclusion sent
  through `POST /ingest` now shows on the map, by entity, by date and in the graph, through the existing routes (§12,
  "Conclusions in the read routes"). No route, project, class, option, dependency or mapping changed. Live only after
  tonight's load from empty stores (documents already indexed and nodes already projected are not redone).

  | File:line | What | Why |
  | --- | --- | --- |
  | `src/dotnet/Synapsis.Data/Services/LatticeKeys.cs:4` | `using H3.Extensions;` | for `GetParentForResolution` |
  | `LatticeKeys.cs:45-74` | new `CellCentreAndParents(cell)`: the cell's centre `[lon, lat]` and the cell named at resolutions 2–5 (itself at its own resolution, its parents at the coarser ones, nothing finer); null when the text is not a valid H3 cell | a conclusion about a cell gets a point and the `h3_r*` fields, from the same H3 library as events |
  | `src/dotnet/Synapsis.Elasticsearch/Services/SearchDocumentMapper.cs:1,3` | `using System;`, `using System.Text.Json;` | for the new methods |
  | `SearchDocumentMapper.cs:17-18` | `CellPrefixes`: `ent:h3:` and `subject:h3:` | the two ways about can name a cell |
  | `SearchDocumentMapper.cs:44-46` | step 2's comment says what a conclusion adds | the comment matched the old code |
  | `SearchDocumentMapper.cs:61-64` | the `ConclusionRecord` case calls the four methods below | the goal |
  | `SearchDocumentMapper.cs:169` `AddAbout` | `entities` ← every id in about, each once; the first valid cell → `location` (its centre) and `h3_r2..h3_r5` | `/search?entity=`, `/map/points`, `/map/cells` |
  | `SearchDocumentMapper.cs:205` `AddPeriod` | `time_start` ← `attributes.period_start` (an ISO time, without an offset read as UTC), else `known_at`; `time_precision` ← `attributes.period_precision`, else `day`; `week` through `LatticeKeys.Week` | `from`/`to` on every route |
  | `SearchDocumentMapper.cs:231` `AddStatementMeasures` | `measures` ← each numeric field of `statement.value` (a bare number → `value`) and `confidence`; other fields left out (through the existing `AddMeasures`) | the UI shows the numbers; the mapping already takes any `measures.*` as a double |
  | `SearchDocumentMapper.cs:257` `AddLogical` | `logical_prefixes` ← `LatticeKeys.LogicalPrefixes(attributes.logical)` when given; the path ends at its first step that is not text | `/logical`, `/search?logical=` |
  | `src/dotnet/Synapsis.Neo4j/Services/GraphRecordRules.cs:7-10` | class summary | the rule changed |
  | `GraphRecordRules.cs:13-17` | `TypesKeptOut` = { `CYB.VULN.EPSS_SCORE` } | about 12 M monthly scores would become nodes |
  | `GraphRecordRules.cs:19-23` | `LabelByPrefix`: `evt:` Event, `ent:` Entity, `con:` Conclusion | the label of a node created from an id alone |
  | `GraphRecordRules.cs:25-35` | `ParticipantsOfConnectingEvent`: summary; a type kept out answers empty | |
  | `GraphRecordRules.cs:52-53` | `entities.Count < 2` → `< 1` | an event with one participant (an FDIC report or failure and its bank) becomes a node linked to it |
  | `GraphRecordRules.cs:60-87` | new `TargetsByLabel(ids)`: the ids grouped by that label, each once; other ids (`rel:`, `subject:...`) left out | one statement per label in the repository |
  | `src/dotnet/Synapsis.Neo4j/Repositories/Neo4jRepository.cs:131` | `StoreConclusionAsync` summary | |
  | `Neo4jRepository.cs:144` | one `LinkAboutAsync` call (was one per label, Entity and Event) | the label now comes from each id |
  | `Neo4jRepository.cs:148-161` `LinkAboutAsync`, `:163-175` `LinkPremisesAsync` | `UNWIND` the targets of each label and `MERGE` the target by id (was `MATCH`); premises may now be any record id (was Event only) | a conclusion links to targets not in the graph yet (an earthquake has no participant, so it was never a node); the record with that id later `MERGE`s into the same node and fills it |
  | `tests/dotnet/Synapsis.Tests/Unit/GraphRecordRulesTests.cs` | the one-entity test now expects a node; new: no participant, a type kept out, `TargetsByLabel` | the rule changed |
  | `tests/dotnet/Synapsis.Tests/Integration/Neo4jRepositoryTests.cs` | the one-participant test now expects a node; the late-targets test expects 2 links at once and the entity filled later; new: a kept-out type, an earthquake premise as an id-only node | the rule changed |
  | `tests/dotnet/Synapsis.Tests/Unit/SearchDocumentMapperTests.cs` (new, force-added: the folder is git-ignored) | 9 tests of the mapper rules and `CellCentreAndParents` | |

  `LinkLateAboutAsync` and `LinkLatePremisesAsync` are unchanged: a known risk (§13, row 14).
  **Checked** (13 Oct) in a cloud sandbox, not on the machine with the sample: `dotnet build` 0 warnings; tests 27 of
  27, including the 11 Neo4j integration tests against a Neo4j 5.26 container. Core stack up from empty stores
  (UI not started); replayed the 2,024 records of 12 Oct kept in git (the deleted mock's `records.json`: KEV, USGS,
  GTD, UCDP, EPSS, HN, the KEV classes). There is no FDIC data in that sandbox, so (b) used three test records shaped
  like FDIC's (`ent:fdic-test:cert-1`, a report and a failure, no measures). Results: `/map/points?bbox=170,45,-170,55&kind=conclusion`
  → 1 item, location `[174.62, 51.39]` (the centre of `8316edfffffffff`), measures observed/expected/s/confidence;
  `/map/cells?resolution=2&kind=conclusion` → `8216effffffffff`, 1 record; `/search?kind=conclusion&from=1965-02-01&to=1965-02-08`
  → 1; `/search?entity=ent:fdic-test:cert-1` → the conclusion and both events; `/graph` of the spark → 1 ABOUT (the
  cell) + 5 PREMISE; `/graph` of the bank → ABOUT from the conclusion, PARTICIPATES to the failure and the report;
  `/graph` of a premise earthquake → 200 (its id only, and the PREMISE link); the same batches again → `unchanged`, no
  new document. EPSS events stay out of the graph (0 nodes). `/search?entity=ent:kev-product:apache/log4j2` → 3 (the
  KEV class conclusion is now listed). Run twice, the second time from empty stores on the final code: the same
  answers, no warning from the indexer or the projector. **To do on the sample**: send (b) about a real FDIC bank
  with a failure.

### Engine agent: stage log (branch `feat/engine`)
- **Stage A**: `src/python/synapsis_engine/` (service `engine`, profile `engine`; run with
  `docker compose -f deploy/core/compose.yml --profile engine run --rm --no-deps engine backfill|verify|coverage`).
  Reads the first state of every event from MongoDB into DuckDB (volume `engine-data`), picks each source's shape from
  the data (measurement / occurrence, subject, bucket, lag = p99 of known_at after the bucket's end), fits M3 on the
  first 60% of buckets (pins in `engine.yaml` for USGS, UCDP, GTD), and writes sparks, forecasts (top 20 per bucket,
  last 52 buckets and demo dates) and `forecast_score` through `POST /ingest` as `src:synapsis:engine`. An event counts
  only if it was known when its bucket closed (end + lag); the model card is frozen when the calibration span closes.
  `verify` on the sample: same maths 0 difference, determinism (same sha256, second POST all unchanged), no hindsight
  (truncated reruns identical), placebo, pipeline counts, coverage: all pass; golden values pending the full load.
  UCDP's lag is pinned to 4 weeks (its measured p99 follows month-long events). New conclusion type `forecast_score`.
- **Stage B**: measurements: per subject and measure, the change against its own previous 52 changes (robust z,
  spark at |z| >= 4 within the 0.5% budget). FDIC lenses (A run risk, B credit stress): robust z over all banks of the
  quarter, joint = the minimum, top 50 per lens and quarter as `joint_anomaly`, year-end scores when the failures
  cover the year after. On the sample: **SVB rank 5 of 4,683, joint z 3.60** (golden), Signature 324 (golden).
- **Stage C**: the source-bias routine (elections), as `case_f6_elections.py` ("routine_bias+prior_blend"):
  `calibrated_estimate` per state and election {poll, bias, corrected, probability}, and a `forecast_score` per
  election {mae, winners, of, brier, brier_market}. Dated no earlier than the first result of the year (the election
  day is read from the results). The sample has no `POL.ELECTION.RESULT` yet: pending; tested on synthetic data.
- **Stage D**: similar patterns: the 10 nearest past 8-bucket windows of the same source (log counts: shape, then half
  the level), only windows whose next 8 buckets were known at the issue, continued and rescaled; `similar_pattern` at
  each spark and on the demo dates. Per source, the MAE of the next-8 total against the routine and M3 run forward
  is in the per-source `forecast_score` and in the report, which says per source whether patterns beat them.
  **After the full load**: `backfill` (stops above 200,000 records without `--force`), then `verify`; the report
  (`/data/engine-report.md` in the volume, copy in `src/python/synapsis_engine/engine-report.md`) fills the goldens.

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

**Which UI file calls which route** (`src/ui/src/`):

| UI file | Route |
| --- | --- |
| `analyze/api/client.ts` | Every read route, one function each: `fetchCells` → `/map/cells`, `fetchPoints` → `/map/points`, `fetchSearch` → `/search`, `fetchLogical` → `/logical`, `fetchGraph` → `/graph`, `fetchRecord` → `/records/{id}`. The queries are built in `analyze/api/query.ts`; the answers' shapes are in `analyze/api/contract.ts`; `analyze/api/adapt.ts` turns documents and buckets into the map's items and hexagons. |
| `analyze/map/useEvents.ts` | Calls `fetchCells`, then `fetchPoints` (the map) |
| `analyze/config.ts` | The API address: `VITE_ANALYZE_API_URL`, default `http://localhost:8080` |
| `observe/config.ts`, `observe/data/*` | Observe: Elasticsearch `synapsis-ingest-log` directly (port 9200, `/es` in development), and the API's `GET /health` and `GET /catalog` |

Search, the tree, the graph and the record panel have their functions in `client.ts` but no screen yet (§14).

**How the map chooses cells or points** (`analyze/map/useEvents.ts`):
1. The resolution comes from the zoom: 0–3 → 2, 4–5 → 3, 6–7 → 4, 8 and above → 5 (`resolutionOf`, `analyze/api/query.ts`).
   Below zoom 3 the map asks about the whole Earth (no bbox). From zoom 3 up it sends the box in view, widened by half
   on each side and snapped to a grid so a small pan asks the same question (`analyze/map/viewport.ts`).
2. It asks `/map/cells` and adds up `records.value` over the cells: the records in view.
3. 5,000 or fewer: it asks `/map/points` for the same box (the whole Earth when there is none), limit 5,000. If the
   points come back cut (`total` above the number of items; possible because points too imprecise for a cell are not
   in the cells' sum), it keeps the cells. Otherwise it draws the points.
4. More than 5,000: it draws the cells. Each hexagon's centre and corners come from its key with h3-js
   (`cellToLatLng`, `cellToBoundary`, in `adapt.ts`); the count shown is `records.value`.
Answers are kept in memory (160 questions), so going back to a view draws at once.

## 12. The read API, explained

Every route below answers on `http://localhost:8080` and passes the store's answer through: Elasticsearch documents
(`_source`) and buckets, Neo4j nodes and links. The UI does all the shaping. Code: `src/dotnet/Synapsis.Api/Controllers/ReadController.cs`
(the routes: each action reads its parameters and calls one method), `Repositories/SearchRepository.cs` (Elasticsearch: one
method per route, which lists the conditions and then writes the whole body as it is sent), `Repositories/GraphRepository.cs`
(Neo4j: the Cypher statement written in the method).

**Shared filters** (Elasticsearch routes, all optional; `SearchRepository.SharedFilters`, `SearchRepository.cs:217`):

| Parameter | Type | Filters |
| --- | --- | --- |
| `from` | date or date-time (`1965-01-01`, `1965-01-01T12:00:00Z`; a date alone is midnight UTC) | `time_start >= from` |
| `to` | same | `time_start < to` (excluded) |
| `source` | text, repeatable (`source=a&source=b`) | any of them |
| `type` | text, repeatable | any of them |

A filter on `from`/`to` drops entities (they have no `time_start`). A wrong date or number answers 400.
As sent, each one condition: `{ "range": { "time_start": { "gte": "<from>" } } }`, `{ "range": { "time_start": { "lt": "<to>" } } }`,
`{ "terms": { "source": [...] } }`, `{ "terms": { "type": [...] } }`. Every text a user typed goes into the body as a
JSON string (quoted and escaped), so quotes or backslashes in it cannot break the query.

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

**Sends to Elasticsearch** (`SearchRepository.MapCellsAsync`, `SearchRepository.cs:39`; route `ReadController.cs:30`; the request
`resolution=3&bbox=170,45,-170,55&from=1965-01-01&to=1965-04-01`):
```bash
curl -s localhost:9200/synapsis-records/_search -H 'content-type: application/json' -d '{
  "size": 0,
  "query": { "bool": { "filter": [
    { "range": { "time_start": { "gte": "1965-01-01T00:00:00Z" } } },
    { "range": { "time_start": { "lt": "1965-04-01T00:00:00Z" } } },
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

**Sends to Elasticsearch** (`SearchRepository.MapPointsAsync`, `SearchRepository.cs:76`; route `ReadController.cs:47`;
the request `bbox=170,45,-170,55&from=1965-01-01&to=1965-04-01`):
```bash
curl -s localhost:9200/synapsis-records/_search -H 'content-type: application/json' -d '{
  "size": 5000,
  "query": { "bool": { "filter": [
    { "range": { "time_start": { "gte": "1965-01-01T00:00:00Z" } } },
    { "range": { "time_start": { "lt": "1965-04-01T00:00:00Z" } } },
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

**Sends to Neo4j** one statement (written in `GraphRepository.NeighboursAsync`, `GraphRepository.cs:26`; route
`ReadController.cs:64`). The label comes from the id's prefix
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

**Sends to Elasticsearch** (`SearchRepository.LogicalAsync`, `SearchRepository.cs:100`; route `ReadController.cs:84`;
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

### GET /search

**Receives**:

| Parameter | Type | Default | |
| --- | --- | --- | --- |
| `q` | text | none | the words; none = just the filtered list |
| `semantic` | `true` / `false` | `false` | `true` = by meaning (needs `q`; 400 without it) |
| `kind` | `event`, `entity`, `conclusion` | all | |
| `logical` | a logical key (`cyber.vendor_product/Apache`) | none | records filed at or below it |
| `entity` | an entity id | none | events in which it takes part |
| `limit` | 1–500 | 50 (400 outside) | items returned |
| `from`, `to`, `source`, `type` | shared filters | | |

**Returns** `{ "total", "items" }` like `/map/points`: one `_source` per record. `q=log4j`:
```json
{ "total": 3,
  "items": [ { "id": "ent:kev-product:apache/log4j2", "kind": "entity", "type": "Product", "name": "Log4j2", ... },
             { "id": "evt:kev:CVE-2021-44228", "kind": "event", "type": "CYB.VULN.KNOWN_EXPLOITED",
               "source": "src:cisa:kev", "title": "Apache Log4j2 Remote Code Execution Vulnerability",
               "body": "Apache Log4j2 contains a vulnerability where JNDI features ...", "time_start": "2021-12-10T00:00:00Z",
               "logical_prefixes": ["cyber.vendor_product", "cyber.vendor_product/Apache", "cyber.vendor_product/Apache/Log4j2"],
               "entities": ["ent:kev-vendor:apache", "ent:kev-product:apache/log4j2", "ent:cve:CVE-2021-44228"], ... },
             { "id": "evt:kev:CVE-2021-45046", ... } ] }
```
Order: with `q`, the best match first (Elasticsearch's score); semantic, the nearest first; without `q`, the newest
`time_start` first (records without one, such as entities, last).

**Three modes, three bodies**, all in `SearchRepository.SearchAsync` (`SearchRepository.cs:134`; route `ReadController.cs:94`):

1. Words (step 2b, `SearchRepository.cs:167`). Each word must be found as the start
   of a word in `name`, `title` or `body` (`phrase_prefix` cuts the text into words the way the index does, so
   "log4j" finds "Log4j2"), or as the start of the `id`, or as the start of `id_key` (the id after its namespace:
   "CVE-2021-44228" for `evt:kev:CVE-2021-44228`, so a bare id typed in finds the record):
   ```bash
   curl -s localhost:9200/synapsis-records/_search -H 'content-type: application/json' -d '{
     "size": 50,
     "query": { "bool": { "filter": [],
       "must": [ { "bool": { "should": [
         { "multi_match": { "query": "log4j", "type": "phrase_prefix", "fields": ["name", "title", "body"] } },
         { "prefix": { "id": { "value": "log4j", "case_insensitive": true } } },
         { "prefix": { "id_key": { "value": "log4j", "case_insensitive": true } } } ] } } ] } },
     "collapse": { "field": "id" },
     "aggs": { "records": { "cardinality": { "field": "id" } } } }'
   ```
   (`must` holds one `should` block per word, so every word must match; `filter` holds the filters.)
2. Meaning (step 2c, `SearchRepository.cs:191`). The API asks the embedder for the question's vector
   (`curl -s localhost:8090/embed -H 'content-type: application/json' -d '{"texts":["remote code execution"],"model":"search"}'`),
   then:
   ```json
   { "size": 50,
     "knn": { "field": "text_vector", "query_vector": [ 512 numbers ], "k": 50, "num_candidates": 500, "filter": [] },
     "collapse": { "field": "id" },
     "aggs": { "records": { "cardinality": { "field": "id" } } } }
   ```
   *knn* compares the question's vector with the stored ones: it looks at the 500 best candidates and keeps the `k`
   nearest. The filters go inside `knn.filter`, so they apply before the nearest are chosen. A question with no known
   word answers `{"total": 0, "items": []}`.
3. No `q` (step 2a, `SearchRepository.cs:152`; like `/map/points`), sorted
   `[{"time_start": {"order": "desc", "missing": "_last"}}, {"state": "desc"}]`.

`logical` adds `{"term": {"logical_prefixes": "<key>"}}`, `entity` adds `{"term": {"entities": "<id>"}}` to the filters.

**Counts**: `total` = distinct records that match (cardinality of `id`; an estimate above about 3,000). In meaning
mode only the `k` nearest match, so `total` ≤ `limit` (there is no "all that match" by meaning).

**Checked** (13 Oct, sample):

| Request | API `total` / items | Independent |
| --- | --- | --- |
| `q=log4j` | 3 / 3 (Log4j2, CVE-2021-44228, CVE-2021-45046) | by hand (every word the start of a word in name/title/body, or of the id): 3 |
| `q=remote code execution&semantic=true` | 50 / 50, all distinct; first: "Microsoft Office Remote Code Execution Vulnerability" | k = 50 |
| `entity=ent:kev-product:apache/log4j2` | 2 / 2 | ES `_count` 2; by hand 2 |
| `logical=cyber.vendor_product/Apache` | 41 / 41 | ES `_count` 41; by hand 41 |
| no `q`, `limit=3` | 7,771 / 3 | **by hand 7,786 distinct ids** (7,819 documents): the default cardinality is 0.2 % low above 3,000; with `"precision_threshold": 40000` it gives 7,786 exactly (§13) |

**A bare id (`q=CVE-2021-44228`)**: fixed in the code (final round, item 2). The indexer now writes `id_key` (the id
after its namespace: `SearchDocumentMapper.IdKeyOf`), the index mapping has it as a keyword, and the word search
matches it by prefix. **It takes effect only with tonight's full load: until then this case still misses** on the
live index (it has no `id_key`; the search answers `ent:cve:CVE-2021-44228` and `evt:kev:CVE-2021-45046`, not
`evt:kev:CVE-2021-44228`). Checked on a scratch copy of the index with `id_key` (deleted after): `CVE-2021-44228` and
`cve-2021-44228` → 4 records including `evt:kev:CVE-2021-44228` and `evt:epss:CVE-2021-44228-20220201`;
`CVE-2021-442` → 6; `log4j` → the same 3 as before. **Tonight's load must start from empty stores** for every
document to get `id_key`: setup creates the index (with `id_key`) only when it does not exist, and a record already in
MongoDB is re-sent as `unchanged` and never indexed again. Before `up --build`:
`docker compose -f deploy/core/compose.yml down && docker volume rm synapsis-core_mongo-data synapsis-core_es-data synapsis-core_kafka-data synapsis-core_neo4j-data`
(keep `synapsis-core_models`, the embedder's weights).

### Conclusions in the read routes

The engine writes conclusions through `POST /ingest` like any record; no route is added. **The contract** (what the
indexer and the projector read):
- `about`: record ids, and/or a map cell as `ent:h3:<cell>` (also `subject:h3:<cell>`).
- `statement.value`: an object of named numbers, such as `{"expected": 0.032, "observed": 85, "rank": 3, "of": 1257}`,
  or a bare number.
- `attributes.period_start` / `attributes.period_end` (ISO date-times), optional `attributes.period_precision`: the
  period the conclusion speaks about. (`period_end` is kept in the record; the index has no field for it.)
- optional `attributes.logical` = `{"space": "...", "path": ["..."]}` for a logical subject.

**What its search document gets** (`SearchDocumentMapper`, the `ConclusionRecord` case): `entities` = every id in
`about`; from the first valid cell, `location` = the cell's centre and `h3_r2..h3_r5` = the cell and its parents
(none finer than the cell); `time_start` = `period_start`, else `known_at`; `time_precision` = `period_precision`,
else `day`; `week`; `measures` = each number of `statement.value` (a bare number as `value`) and `confidence`;
`logical_prefixes` from `attributes.logical`.

**What each route now shows**:

| Route | A conclusion appears when |
| --- | --- |
| `/map/points?kind=conclusion` | its about names a cell (at the cell's centre) |
| `/map/cells?kind=conclusion` | its about names a cell of that resolution or finer |
| `/search?kind=conclusion&from=&to=` (and `from`/`to` on every route) | its period starts in the window (no period: its `known_at`) |
| `/search?entity=<id>` | the id is in its about (the KEV classes too: `entity=ent:kev-product:apache/log4j2` gives 3 after the load, not 2) |
| `/logical`, `/search?logical=` | it has `attributes.logical` |
| `/graph?id=<conclusion>` | always: ABOUT to each `ent:`/`evt:`/`con:` id of its about, PREMISE to each premise, even when that record is not in the graph (the node holds only its id until the record arrives; the UI reads details from `/records/{id}`) |
| `/graph?id=<record>` | the record is in its about or premises: an earthquake that is a premise answers 200, not 404 |

**The projector also changed**: an event becomes a node when it has at least one participant (was two), except
`CYB.VULN.EPSS_SCORE`. FDIC reports and failures are now nodes linked to their bank, so "bank flagged → bank failed"
shows in `/graph?id=<bank>`. GTD attacks with one actor, HN posts and NVD events (one participant, the CVE) become
nodes too.

**Side effect**: every conclusion now has a `time_start`, so the KEV classes (`known_at` 2 Oct 2026) sort near the top
of `/search` without `q` (newest first) and fall in any window that holds that date.

**Limits at full scale**: a short word
expands to at most 50 indexed words (`phrase_prefix`'s `max_expansions`), so "a" finds only some of the words starting
with "a". `num_candidates` 500 bounds the meaning search: at millions of vectors it is approximate (a near document can
be missed).

## 13. Full-data review: what can be slow, approximate or cut

Every read query, judged at millions of documents. "Fix" is what to change tomorrow if it bites, not done today.

| # | Where | What happens at full scale | Fix if needed |
| --- | --- | --- | --- |
| 1 | **cardinality** (every `records` / `total`) | Exact up to about 3,000 distinct ids, then an estimate. Measured on the sample: no-`q` search says **7,771**, the true number is **7,786** (0.2 % low). At millions the error stays around 1 %. A total near the UI's 5,000-points limit can be off by a few dozen. | **Decided: no `precision_threshold`** (final round): totals above 3,000 stay estimates. (For the record: `"precision_threshold": 40000` gave 7,786 exactly, about 320 kB per count.) |
| 2 | `/map/cells` **terms size 10,000** | Resolution 2 has 5,882 cells on the whole Earth: never cut. Resolution 3 has about 41,000, 4 about 288,000, 5 about 2 M: cut without a bbox. The UI sends a bbox from zoom 3 up, so a view holds far fewer cells. `sum_other_doc_count` > 0 says when cells were cut. | The UI shows "cells cut" when `sum_other_doc_count` > 0. |
| 3 | `/map/cells` **memory** | One cardinality per cell, ordered by it. At resolution 2 with millions of events, about 5,900 counts of up to about 24 kB each, so roughly 140 MB in one request. Elasticsearch has a **1 GB heap** (`CORE_ES_HEAP`); its request circuit breaker trips at 60 % (600 MB) and answers 500. | Set `CORE_ES_HEAP=4g` in `deploy/core/.env` for full data (the machine has 8 GB). If it still trips: order by `_count` (documents) instead of `records`. |
| 4 | `/logical` **terms size 1,000** | NVD files records under tens of thousands of vendors: `cyber.vendor_product` opens with the 1,000 largest. `sum_other_doc_count` says how many documents are in the rest. The first call after new data builds the field's *global ordinals* (a table of every distinct path) and can take seconds. | Raise `size` to 5,000, or add a search box for the level (`include` with the typed prefix). |
| 5 | `/search` meaning: **knn num_candidates 500** | The vector index (HNSW, `int8_hnsw`: one byte per number) finds the nearest *approximately*: it looks at 500 candidates, so a close document can be missed at millions of vectors. Memory: about 512 bytes per vector, so 1.2 M NVD vectors ≈ 600 MB on disk and in the page cache, not in the heap. `total` is never more than `k`. | Raise `num_candidates` (1,000–2,000) for better recall, at some speed cost. |
| 6 | `/search` words | `phrase_prefix` expands a short word to at most 50 indexed words ("a" finds only some). `prefix` on `id`, case-insensitive, walks the id terms that start with the word: fine for typed ids. A bare CVE id finds `evt:kev:CVE-…` through `id_key` (after tonight's load; §12). Collapse + sort: Elasticsearch reads every match to keep the best 50 records (linear, fine at millions). | Done in the code (`id_key`, §12); live after tonight's load from empty stores. |
| 7 | `/map/points` **collapse, size 5,000** | Collapse returns one document per record, at most 5,000 (below `max_result_window`, 10,000). With more than 5,000 records in the box, `total` > items: the UI then keeps the cells (it asks for points only when the cells say ≤ 5,000). | — |
| 8 | **hits.total** | Not used: with collapse it counts documents (states), not records. Every `total` comes from cardinality (row 1). | — |
| 9 | **geo_bounding_box across the 180th meridian** | Handled by Elasticsearch itself (`minLon > maxLon`): checked 75 = two boxes = by hand. A box covering the poles is clamped to ±90 by the UI. | — |
| 10 | `/graph` **hubs** | `COUNT { (c)--() }` reads the degree Neo4j keeps with the node: the same cost for 4 links or 40,000. `LIMIT` returns the first 200 (max 1,000) in storage order, not the newest or the most relevant. Microsoft has 460 links on the sample and tens of thousands with NVD. | `ORDER BY n.known_at DESC` before `LIMIT` (sorts all of the hub's links: fine up to about 100 k). |
| 11 | **States, not records** | Filters match any state of a record. A record whose older state matches `from`/`to`/`type` but whose latest state does not is still counted and listed (collapse shows its latest matching state). Rare: states change for revised data (UCDP, NVD scores). | Index only the latest state (the indexer deletes the older document), or add `"state"` filtering. A data-policy decision. |
| 12 | **The embedder** | `/search?semantic=true` needs the `embedder` service (the API waits for it at start-up). If it stops, semantic search answers 500; word search keeps working. | — |
| 13 | **Time filters on entities** | `from`/`to` drop every record without `time_start` (all entities): a search with a time window never lists entities. | The UI sends `from`/`to` to `/search` only when it wants events. |
| 14 | **Projector: late links** (`Neo4jRepository.LinkLateAboutAsync`, `LinkLatePremisesAsync`; known risk, left as it is) | On every entity, event, participant and relation write, each runs `MATCH (c:Conclusion) WHERE $id IN c.about` (or `c.premises`): a scan of every Conclusion node, because a list property has no index. Cheap with the sample's 63 conclusions; once the engine has written tens of thousands, every write of the full load pays for that scan and the projector falls behind. | Remove both calls: since a conclusion now `MERGE`s its targets (§12, "Conclusions in the read routes"), every link they would add already exists. |

## 14. Prompt for the UI agent

> You are the UI agent for Synapsis (React + TypeScript, `src/ui/`). Work on `dev` (it holds your `ui/analyze` up to
> `c4144c6` plus everything below); pull first, commit small, push often. Read `EMERGENCY.md` §0 (emergency rules), §11
> (the system) and §12 (the read API, explained) before you start; this prompt is enough to begin.
>
> **What changed.** The read API is now five routes on `http://localhost:8080`. They pass the stores' answers through,
> and the UI does all the shaping. The mock (port 8081) and every `/analyze/*` route are deleted. CORS allows
> `http://localhost:5173` and `http://localhost:1897`. Shared optional filters on every route except `/graph`:
> `from`, `to` (ISO dates on the event's `time_start`, `to` excluded), `source` and `type` (repeat the parameter for
> several). There is no `as_of`, `zoom`, `layer` or `cursor`.
>
> **The five answers, exactly:**
> 1. `GET /map/cells?resolution=2..5[&bbox=minLon,minLat,maxLon,maxLat][&kind=event]` →
>    `{"records": 670, "sum_other_doc_count": 0, "buckets": [{"key": "821e67fffffffff", "doc_count": 64, "records": {"value": 64}}, ...]}`
>    (`key` = H3 cell; show `records.value`; `sum_other_doc_count` > 0 = cells were cut).
> 2. `GET /map/points?bbox=...[&kind=event][&limit=1..5000]` → `{"total": 71, "items": [<document>, ...]}`.
> 3. `GET /search?[q=][&semantic=true][&kind=][&logical=<key>][&entity=<id>][&limit=1..500, default 50]` →
>    `{"total": 3, "items": [<document>, ...]}`. Words match as prefixes ("log4j" finds "Log4j2"); `semantic=true`
>    ranks by meaning (`total` ≤ `limit`); no `q` = the filtered list, newest first.
> 4. `GET /logical[?node=<key>]` → `{"sum_other_doc_count": 0, "buckets": [{"key": "cyber.vendor_product/Microsoft", "doc_count": 389, "records": {"value": 389}}, ...]}`:
>    one level only (the top-level spaces without `node`; the direct children of `node` with it), at most 1,000.
> 5. `GET /graph?id=<record id>[&limit=1..1000, default 200]` →
>    `{"center": {"id": ..., "labels": ["Entity"], ...properties}, "total": 4, "links": [{"type": "PARTICIPATES", "from": <id>, "to": <id>, "properties": {"role": ...}, "node": {"id": ..., "labels": ["Event"], ...}}, ...]}`;
>    404 for an unknown id.
>
> Plus `GET /records/{id}` (unchanged: each source's full record, as stored). A *document* is Elasticsearch's
> `_source`: `id, id_key (after tonight's load), kind, type, source, known_at, state`, and when the record has them `name, title, body, time_start,
> time_precision, location [lon, lat], h3_r2..h3_r5, country, subdivision, logical_prefixes[], measures {name: [values]},
> entities[]`. Lists hold one document per record; `total` counts distinct records (an estimate within about 1 % above
> 3,000).
>
> **What is already done in `src/ui/src/analyze`** (logic only; the look is unchanged):
> - `api/contract.ts` has the five shapes.
> - `api/query.ts` has `cellsQuery`, `pointsQuery`, `searchQuery`, `logicalQuery`, `graphQuery`, `recordPath`, and
>   `resolutionOf(zoom)`.
> - `api/client.ts` has `fetchCells`, `fetchPoints`, `fetchSearch`, `fetchLogical`, `fetchGraph` and `fetchRecord`.
> - `api/adapt.ts` turns documents into `Item`s (label: name, then title, then the start of the body, then
>   "type · where") and buckets into hexagons with h3-js.
> - `map/useEvents.ts` chooses the layer. It asks `/map/cells` at the zoom's resolution (0–3 → 2, 4–5 → 3,
>   6–7 → 4, 8+ → 5; with a bbox from zoom 3 up). Then it asks `/map/points` only when the cells in view hold 5,000
>   records or fewer, and keeps the cells if the points come back cut.
> - `config.ts` defaults to `http://localhost:8080`.
> - The "seen as of" control no longer affects anything (the API has no `as_of`): remove it or leave it, as you judge.
>
> **The full-data rule.** From now on everything is tested against all the records (millions: NVD 1.2 M, EPSS 14 M,
> FDIC, UCDP, GDELT...), not the 8,606-record sample. Never count or aggregate in the browser over a capped list. Always
> show "N of M" (`items.length` of `total`), and say when a level or the cells were cut (`sum_other_doc_count` > 0).
> Never ask for an unbounded list. Open a tree level by level. The sample never holds more than 5,000 records in a
> view, so you will see cells only with full data, or by lowering `mostPoints` in `useEvents.ts` for a moment.
>
> **What remains for you, in this order** (cut from the bottom if time runs out):
> 1. **Story presets** instead of "the last month" (each source sits in its own era): buttons that set the window and
>    the view: "Rat Islands earthquakes, Feb 1965" (`from=1965-01-25&to=1965-03-01`, Aleutians), "Attacks in the
>    US, 1970", "Conflict, Jan 1989", "Known exploited vulnerabilities (KEV)" (no points: it opens search with
>    `source=src:cisa:kev`).
> 2. **The panel**: a click on a point or a result opens a side panel with `/records/{id}` (the record as stored)
>    and `/graph?id=` (links grouped in the UI by `type` and by the other node's `labels`, with "N of total"). A click
>    on a neighbour moves the panel to it.
> 3. **The search box**: text by default, a toggle for meaning (`semantic=true`); a result list with "N of M"; a
>    click opens the panel and, when the result has a `location`, flies the map there.
> 4. **The logical tree**: `/logical` for the top level, `/logical?node=<key>` when a node opens (show
>    `records.value`); a click on a node lists `/search?logical=<key>`.
> 5. **Source and type filters**: chips sent as `source=` / `type=` on every call (`Filters` in `api/query.ts`). The
>    values come from `GET /catalog`.
>
> **Checks before every push** (no Node on the host), from `src/ui`:
> `docker run --rm -u $(id -u):$(id -g) -e HOME=/tmp -v "$PWD":/ui -w /ui node:24.21.0-alpine3.24 sh -c "npm install && npm run build && npm run lint && npm test"`.
> Dev server: `docker run --rm -it -u $(id -u):$(id -g) -e HOME=/tmp -v "$PWD":/ui -w /ui -p 5173:5173 node:24.21.0-alpine3.24 npm run dev -- --host 0.0.0.0`
> → `http://localhost:5173/analyze?from=1965-01-01&to=1965-04-01`. Write what you built into EMERGENCY.md.

## 15. UI agent: Analyze explore (branch `ui/analyze-explore`)

Four stages, each stopped for screenshots and feedback: (1) a click on a point brings up the object, (2) the
connections graph, (3) the logical dimension, (4) search and filtering. Nothing that existed changes look; only
additions. Development without the stores: `npm run dev:fixtures` (from `src/ui`; `VITE_ANALYZE_FIXTURES=1`) answers
the read API from `src/ui/fixtures/analyze/` (`analyze/api/fixtures.ts`): the map routes over `map-points-world.json`
with the API's own filters, the other routes with the saved file of that exact request, and anything else **501 "not in
the fixtures"** (never an empty answer). The production build leaves the fixtures out.

- **Stage 1 (13 Oct)**: a click on an event opens the **object panel** (`analyze/object/`), right, under the time card
  and as wide: the record's family colour, kind, title, type and id; its facts, text, measures, attributes and evidence
  from `GET /records/{id}` (one tab per source when several), and **the record as each source sent it** (JSON). Its
  **connections** (`GET /graph?id=`) are divided by kind ("Taking part", "Takes part in", "Conclusions about it",
  "Product of"...), with "N of total" and a note when the API sent fewer than the node has (the groups then count only
  those); 404 says "Not in the graph". A click on a connection moves the panel to it (a trail of the records looked at,
  with back). The globe marks the object with a ring and turns to it when it is hidden; the view buttons step left of
  the panel. A click on the map away from the lights, Esc, or close returns to the map.
- **Stage 2 (13 Oct)**: **GRAPH-D**, the connections graph (`analyze/graph/`, three.js, loaded when first opened). A
  switch at the top (Map | Graph) shows the open object in either dimension; the panel has "Open the connections
  graph". The object sits at the centre; each kind of connection gets its own direction and its nodes fan out in a
  cone (`layout.ts`, deterministic: nodes never move once placed). A click on a node opens it in the panel and grows
  its connections out of it (`GET /graph?id=`), so the investigation goes on from any node; the trail is lit. Top:
  "N of total connections of X", and "Load up to 1,000" when the API sent fewer (`limit=1000`). 404: the object alone,
  "Not in the graph". Bottom left: what is drawn, and a chip per kind of record and kind of link that hides it.
  Controls: closer, farther, back to the object, the whole graph; drag turns, right-drag moves, wheel zooms. "Show on
  map" in the panel returns to the globe at the object. The address can open an object: `&object=<id>`, and
  `&dimension=graph`.
- **Stage 3 (13 Oct)**: **the logical dimension** (`analyze/logical/`). The switch is now always at the top: Map |
  Graph (needs an open object) | Logical. The tree of logical places opens one level per click: `GET /logical`, then
  `GET /logical?node=<key>` for each node that opens (records per node = `records.value`, a light under each row
  sized against the largest of its level; "And more" when `sum_other_doc_count` > 0). The chosen node lists its records
  beside the tree: `GET /search?logical=<key>`, "N of M", "Show up to 500". With an object open, the tree opens along
  the object's own path and chooses its place (from its record's `place.logical`); an object with none says so. A
  click on a record opens it in the object panel; from there Map (it has a point) or Graph. The panel's "Filed under"
  has "Show in the tree". Closing the object keeps the tree; `&dimension=logical` opens it from the address.
- **Stage 4 (13 Oct)**: **search and filtering**, one action in two modes.
  - *The filter of the view* (`analyze/filter/`), left above the key (the query log stays above it and folds): the
    kind of record (Any, Events, Entities, Conclusions), and sources and types from `GET /catalog`, each a chip a click
    removes. What does not match disappears: the map sends `source`, `type` and `kind` with the time card's window to
    `/map/cells` and `/map/points` (the only filters those routes take); the tree sends `source` and `type`; the graph
    hides the drawn nodes that do not match (the object stays). A click on a field in a record's details adds it: the
    source, the type (under the title), the kind, the logical place, and the date (it sets the time card's window to
    that day, month or year; the ruler follows).
  - *The search window* (`analyze/search/`, the Search button beside the switch, or "/"): every parameter of
    `GET /search` (words or meaning, kind, sources, types, from, to, logical place, entity, how many), starting from the
    view's filter. Results as the API ranks them, "N of M", re-orderable (newest, oldest, name: only the ones
    received). A click on a result: on the map when it has a place, else in the tree when it is filed there; its
    details and graph are always open in the panel.
