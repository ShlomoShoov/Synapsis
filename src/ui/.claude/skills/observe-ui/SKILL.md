---
name: observe-ui
description: How to work on Observe, the monitoring area of the Synapsis web client (src/ui/src/observe) — conventions, checks, and how to verify a change with real or simulated data.
---

# Working on Observe (src/ui/src/observe)

Read `src/ui/.claude/observe-ui-status.md` first: what exists, the map of the code, and what remains.

## Rules of the area
- Stay inside `src/ui`. Never change the backend or core code (C#, feeders, contracts); if the UI needs something
  there, ask for approval and describe the change.
- Keep logic in pure modules with unit tests (`*.test.ts` beside them); components draw and wire only.
- Data comes only through `data/` (`IngestLog`, `SchemaRegistry`); a new number means a new query in
  `data/elastic/queries.ts` + parsing in `parse.ts` + the same in `data/simulation/` (both modes must agree).
- Times sent to Elasticsearch are whole milliseconds (it rejects fractional `epoch_millis`).
- Colours: domains and outcomes from `domains.ts` / `outcomes.ts` (a validated palette; change only together and
  re-validate). Text uses text tokens, never a series colour. Status colours always with an icon and a word.
- React Compiler lint rules apply: no `Date.now()` in render (use `useNow`), no setState in effects for derived
  values, no ref reads/writes during render. jsx-a11y: native elements first (buttons, radios, range inputs).
- CSS Modules: `composes` only on single-class selectors.
- Desktop only, night theme; keep motion compositor-only (transform/opacity) and pause work off screen.

## Checks before a commit
```bash
cd src/ui
npx prettier --write "src/observe/**/*.{ts,tsx,css}"
npx tsc -b && npx oxlint && npx vitest run && npm run build
```

## Seeing it
- `npm run dev`, then `/observe` (live: proxies `/es` → :9200, `/api` → :8080) or `/observe?data=simulated`.
- Screenshots with Playwright (Chromium is preinstalled in the cloud container). The WebGL scene is slow in software
  rendering: wait ~12 s for flow shots, or launch with `--disable-webgl` for shots of the other sections. Scroll with
  `behavior: 'instant'` (the page scrolls smoothly) and clip to the viewport instead of full-page shots.
