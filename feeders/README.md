# Synapsis feeders

One Python package with one feeder per source. A feeder registers its schema package with the API, fetches from the
live source, maps each row to a SIL record, sends batches of up to 5,000 records to `SYNAPSIS_API_URL`, and saves a
cursor so the next run continues where this one stopped. Any feeder can run once, for any date range (`run`), or keep
running as a service (`serve`).

Each feeder owns its schema package, in `packages/<package>.yaml`. The API seeds only the core vocabulary; a feeder
registers its own package (and the shared `health` or `social` it uses) when it starts.

## Running

In Docker Compose, every feeder runs as a service of the feeders deployment (`deploy/feeders/`):

```bash
docker compose -f deploy/feeders/compose.yml up -d --build      # every feeder, as a service
docker kill --signal SIGUSR1 synapsis-feeders-feeder-kev-1       # kev looks for new data now
docker compose -f deploy/feeders/compose.yml logs -f feeder-kev
```

On your machine:

```bash
pip install -r feeders/requirements.txt
cd feeders
export SYNAPSIS_API_URL=http://localhost:8080 FEEDER_CONTACT=you@example.org
python -m synapsis_feeders list
python -m synapsis_feeders run usgs --since 2024-01-01 --until 2024-03-31
python -m synapsis_feeders run usgs --dry-run /tmp/usgs          # write the batches to files, send nothing
python -m synapsis_feeders serve usgs --every 1h                  # keep running; SIGUSR1 triggers a run now
```

| Option | Meaning |
| --- | --- |
| `--since`, `--until` | A backfill range (dates). Default: continue from the cursor, or the source's own start |
| `serve <feeder>` | The service: register the schema packages, run, then wait for a trigger and run again until stopped |
| `serve ... --every 6h` | The timer trigger: run again on this interval. Without it, a service waits only for a manual trigger (SIGUSR1). A failed run is retried after 1, 2, 4... minutes |
| `--limit N` | Stop after N records (smoke tests) |
| `--dry-run DIR` | Write the batches to DIR instead of sending them; leaves no state behind (`run` and `run-all` only) |
| `--reset` | Forget the cursor and the memo and start from the beginning; records already held come back `unchanged` |
| `--set key=value` | A feeder option, for example `--set sample=weekly` for `gdelt` (each feeder's docstring lists its options) |

## Environment

| Variable | Default | Meaning |
| --- | --- | --- |
| `SYNAPSIS_API_URL` | `http://localhost:8080` | The API every feeder sends to |
| `FEEDER_CONTACT` | none | A URL or mailbox for the User-Agent; Wikipedia refuses clients without one |
| `FEEDER_STATE_DIR` | `./.feeder-state` | Cursors, memos and logs, one folder per feeder |
| `FEEDER_CACHE_DIR` | `./.feeder-cache` | Downloaded archives, so a re-run does not fetch them again |
| `FEEDER_BATCH_SIZE` | `5000` | Records per request |
| `FEEDER_PACKAGES_DIR` | `feeders/packages` | Where the schema packages are read from |
| `FIRMS_MAP_KEY`, `GFW_TOKEN`, `FRED_API_KEY` | none | Required by `firms`, `gfw`, `fred` |
| `NVD_API_KEY` | none | Optional; raises NVD's rate limit |
| `GTD_URL` | the round-2 mirror | Optional; your own copy of the GTD file (checked by SHA-256 unless `--set sha256=any`) |

Keys are read from the environment only and never written to logs: URLs are logged with keys removed.

## What a feeder remembers

```
$FEEDER_STATE_DIR/<feeder>/
  cursor.json        where the next run starts; saved only after the API acknowledged the records before it
  memo/              what else the feeder must remember (cleared by --reset)
  observed/          the feeder's own observations the source cannot give again (kept by --reset; back it up)
  runs.jsonl         one line per run: counts (new, versioned, unchanged, rejected), codes, duration, outcome
  rejections.jsonl   every rejected record: batch, index, id, code, path, message
```

A lost state folder costs time, never correctness: a re-run sends the same ids and the API answers `unchanged`.

## The feeders

| Feeder | Source | Key | Compose trigger |
| --- | --- | --- | --- |
| `kev` | CISA Known Exploited Vulnerabilities | | 24 h |
| `kev-classes` | the 720 round-3 product labels in `data/` (waits for `kev`) | | at start-up, then manual |
| `ransomware-live` | ransomware.live victim posts | | 6 h |
| `ucdp` | UCDP GED, annual and monthly candidate files | | 7 days |
| `gdelt` | GDELT 1.0 daily event exports (Sudan, Ukraine, South Sudan) | | 24 h |
| `firms` | NASA FIRMS VIIRS active fires (Sudan, Ukraine) | `FIRMS_MAP_KEY` | 6 h |
| `fdic` | FDIC BankFind quarterly financials and failures | | 7 days |
| `delphi` | Delphi Epidata COVID-19 signals, US states | | at start-up, then manual |
| `jhu-covid` | Johns Hopkins CSSE confirmed cases | | at start-up, then manual |
| `reddit` | Reddit posts from the Arctic Shift archive | | 24 h |
| `hackernews` | Hacker News security stories (Algolia) | | 6 h |
| `wikipedia-elections` | US presidential state polls and results, 2016, 2020, 2024 | | 7 days |
| `epss` | EPSS scores, the first of each month since 2022 (`--set every=day` for all days) | | 24 h |
| `nvd` | NVD CVE API 2.0 | `NVD_API_KEY` (optional) | 6 h |
| `opensanctions` | OpenSanctions maritime list | | 24 h |
| `gfw` | Global Fishing Watch loitering and port visits for 140 sampled vessels | `GFW_TOKEN` | 7 days |
| `polymarket` | Polymarket 2024 state presidential markets, hourly prices | | 24 h |
| `usgs` | USGS ComCat earthquakes, magnitude 5.5 and above since 1965 | | 1 h |
| `gtd` | Global Terrorism Database, 1970 to 1994 | | at start-up, then manual |
| `xz-git` | xz-utils git history (commit metadata only) | | 24 h |
| `fred` | FRED: federal funds rate, 2- and 10-year yields, their spread | `FRED_API_KEY` | 24 h |

## Adding a source

1. Write `packages/<package>.yaml`: the source with its reliability, basis and `known_at` rule, then the types,
   roles, measures, attributes and spaces it needs. Copy the closest existing package.
2. Copy `synapsis_feeders/sources/_template.py` to `sources/<name>.py`, follow its checklist, decorate the class with
   `@register` and import the module in `sources/__init__.py`.
3. `python -m synapsis_feeders run <name> --dry-run /tmp/<name> --limit 500` and read the batches.
4. `python -m synapsis_feeders run <name> --limit 500` against the API; every rejection in `rejections.jsonl` names its
   rule and path. Fix the mapping or the package and run again; records already accepted come back `unchanged`.
5. Add the service to `deploy/feeders/compose.yml` (`<<: *feeder`, a `serve` command with `--every`, and its key
   if it needs one, mapped from `FEEDERS_<KEY>`; put `FEEDERS_<KEY>=` in `deploy/feeders/.env.example`).

An AI coding assistant can draft steps 1 and 2 from one real sample of the source, the template and a similar
package; the API's rejections make its mistakes visible.

## Tests

```bash
pip install pytest && cd feeders && pytest -q
```
