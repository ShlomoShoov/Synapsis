# Observe (web client) — status, map of the code, and what remains

Written at the end of the first build session (11 Oct 2026). Observe is the area of the web client for developers and
sysadmins, reached from the Home page ("Open Observe"), at `/observe`. Everything lives in `src/ui/src/observe/`;
no backend or core code was touched.

---

## 1. Where things stand

| Part | State | Notes |
|---|---|---|
| Home → Observe link | Done | The site header (`site/Header.tsx`) and the home page's contents (`home/Overview.tsx`) lead to `/observe` (lazy route in `router.ts`). |
| Page shell | Done | Night theme, sidebar (sections, nested anchors, data mode Live/Simulated, service health, time zone Local/UTC, shortcuts), keys 1/2/3, `[`, `?`. |
| 01 Live flow (3D) | Done | three.js scene: sources around the API core, arcs per batch (written/refused), live from Elasticsearch, time travel (1 h–30 d bar, replay ×1…×21600, step, return to live). |
| 02 Metrics | Done | 8 panels, filters in the URL, compare with previous period, auto refresh, Customize (order, width, visibility). |
| 03 Schema map | Usable first version | Map with pan/zoom/fit, cards, threads, spotlight, search, inspector, live source status. See §5 for what is missing. |
| Tests | 73 passing (16 files) | `npm test`. Pure modules are tested; components are verified by screenshots. |
| Lint / types / format / build | Clean | `npx oxlint`, `npx tsc -b`, `npx prettier --check "src/observe/**/*.{ts,tsx,css}"`, `npm run build`. |

---

## 2. How to run it

```bash
cd src/ui
npm install
npm run dev            # http://localhost:5173/observe
```

- **Live data** (default): the dev server proxies `/es` → `http://localhost:9200` (Elasticsearch) and `/api` →
  `http://localhost:8080` (the C# API) — see `coreProxy()` in `vite.config.ts`. Outside dev, set `VITE_ES_URL` and
  `VITE_API_URL` (`src/observe/config.ts`). Elasticsearch must allow the browser (CORS) or sit behind the same origin.
- **Simulated data**: `/observe?data=simulated`, or the sidebar's Data switch. A deterministic simulator (30 days of
  history, then 3 busy hours that go on live) and a snapshot of the 24 schema packages; always labelled "Simulated",
  never chosen silently.
- The index read is `synapsis-ingest-log` (`config.ingestLogIndex`); documents as in `contracts/` and
  `IngestLogRepository.cs`.

---

## 3. Map of the code (`src/ui/src/observe/`)

```
ObservePage.tsx        page: theme, providers, sidebar, the three sections, global keys
config.ts env.d.ts     addresses, timings
domains.ts outcomes.ts the domains (validated palette) and the record outcomes
theme/                 night tokens (night.css), type styles, fonts, palette helpers
lib/                   format, time, clock (useNow), storage, keyboard, time zone, element size, in-view, reduced motion
data/                  the data layer — swap sources here, nothing else changes
  contracts.ts model.ts      wire types (ES document, schema package) and the app's model + service interfaces
  elastic/                   ES queries (queries.ts) and answer parsing (parse.ts) → ElasticIngestLog
  api/ApiSchemaRegistry.ts   GET /schemas, /schemas/{package} (refetches only changed fingerprints)
  simulation/                seeded simulator + SimulatedIngestLog / SimulatedSchemaRegistry + demo-packages.json
  ObserveDataProvider.tsx    mode, services, health checks, schema packages (refresh 60 s), catalog
  useResource.ts livePoller.ts catalog.ts errorCodes.ts http.ts
shell/                 sidebar, scroll spy, section header, shortcuts dialog
ui/                    Icon, Segmented, Switch, Popover, Hint, Kbd, light dismiss
shared/                outcome bar, domain dot, batch sheet (a batch with its refused records)
flow/                  01: FlowSection, FlowCanvas, hud, Timeline, playback, replay, director, engine/ (three.js)
metrics/               02: MetricsSection, FilterBar, filters, layout (Customize), freshness, charts/, panels/
schemas/               03: SchemaSection, SchemaMap, PackageCard, Threads, Inspector, KindGlyph
                           graph.ts (model) · layout.ts (geometry) · camera.ts · focus.ts · search.ts (all tested)
```

Principles kept throughout (keep them):
- **Pure core, thin views.** Queries, parsing, simulation, freshness, filters, graph, layout, camera, focus and search
  are pure and unit-tested; components only draw and wire.
- **One question per screen state.** Metrics ask the log once per period (the snapshot), so every number agrees.
- **No silent fallbacks.** Errors say what failed and offer "Use simulated data"; simulated data is always labelled.
- **Data-viz rules** (validated palette, one axis, hairline grids, text in text colours, a table view for every chart,
  status colours only with an icon and a word).
- **Accessibility**: native controls (radios, range input, buttons), keyboard paths for everything, reduced motion.
- **Performance**: the 3D scene and the timeline stop when off screen; nothing writes the DOM every frame without need.

---

## 4. Verification done

- Real Elasticsearch 8 in Docker, seeded with the simulator (`scratchpad` tools, not in the repo): every query checked
  (snapshot, paging with `search_after`, prefix filters, nested error codes, activity, windows, history, ping).
  Found and fixed: ES rejects fractional `epoch_millis` → every time is rounded.
- The API's Docker build was refused in this environment (it copied the proxy certificate into the image), so the
  schema endpoints were served by a small mock with the real package files; the client code follows the contract.
- Screenshots at 1440 × 900 of each section; replay, customize (drag and keyboard), search and inspector exercised
  with Playwright.
- Idle cost measured: ~1 layout/s and a few ms of script per second with refresh off.

---

## 5. What remains (in priority order)

### Schema map (03) — to finish
1. **Hint and controls with the inspector open**: the "Drag to move…" hint can sit under the inspector; centre it in
   the free part (or hide it once a row is selected).
2. **List view** (accessible alternative to the canvas): packages by domain → definitions, using the same inspector.
3. **Domain filter chips** in the toolbar: `focus.ts` already supports lighting chosen domains (`highlightOf(…, domains)`).
4. **Minimap** (bottom-left): bounds, cards, the viewport rectangle; click/drag to move. Styles exist (`.minimap*`).
5. **"Changed / New" badges** on cards when a package's fingerprint changes while the page is open (`PackageCard`
   already has a `badge` prop; keep the first-seen fingerprints in state, not in a ref read during render).
6. **Sidebar marker** for section 03 sits between items (the section has no nested anchors); give it anchors or skip
   the anchor list for it in `Sidebar.tsx`.
7. Safari trackpad pinch (`gesturechange`) — Chrome/Firefox use Ctrl + wheel, which works.
8. "See its metrics" from a source in the inspector (set the metrics filter to that source and scroll there).

### Across Observe
- **Bundle size**: `ObservePage` is ~820 kB (three.js). Split the flow engine into its own lazy chunk.
- **Component tests** for the sections (Testing Library) — today only pure modules are unit-tested.
- **Real API run**: point at the C# API once it builds here, and check `/schemas` end to end.
- **Production CORS**: Elasticsearch is read from the browser; in production put it behind the same origin (nginx
  location) or allow the client's origin. This is a deployment decision for the core team.
- Areas "Getting started" and "Analyze" are marked "Soon" on Home and in the sidebar.

---

## 6. Notes for the repository

- `.gitignore` ignores `.claude/` at every level. These notes were added with `git add -f` so they survive; if the
  team prefers, move them to `docs/ui/` instead.
- Files outside `src/observe/` that changed: `src/router.ts`, `src/pages/HomePage.tsx` (+ `.module.css`),
  `vite.config.ts` (dev proxy), `package.json`/`package-lock.json` (three, d3-scale, d3-shape, d3-time-format,
  fonts), `.prettierignore` (the demo packages snapshot).
- Work was done on the local branch `ui/observe` and merged into `dev`.
