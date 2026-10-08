# Synapsis

A predictive knowledge engine: records from many sources, each with the time it happened and the time it became
known, in one language (SIL 1.2), so that every question can be asked "as of" any date without later knowledge
leaking into it.

This repository holds what runs today and the plan for the rest:

| Part | Folder | State |
| --- | --- | --- |
| Record language: envelope and package format | `schemas/sil/` | built |
| Schema packages for every source (seeded into the database, changed through the API) | `schemas/seed/` | built |
| Feeders: one per source, 21 in all | `feeders/` | built |
| Python stand-in for the API: schemas, ingest, records, semantic search | `src/python/synapsis_reference/` | built (development) |
| Embedder: local text models behind HTTP | `src/python/synapsis_embedder/` | built |
| Conformance cases every API must pass | `tests/conformance/` | built |
| Docker Compose for all of it | `docker-compose.yml` | built |
| C# API, projector and indexer | `src/dotnet/` | to build (guide, sections 6 and 8) |
| Python engine | `src/python/synapsis_engine/` | to build (guide, section 7) |
| React client | `src/ui/` | to build (guide, section 12; Doc 2) |

The implementation guide (Claude Doc "Synapsis Implementation Guide") describes every folder, file, class and flow.

## Quick start

```bash
cp .env.example .env                                   # set FEEDER_CONTACT and the source keys you have
docker compose --profile dev --profile feeders build
docker compose --profile dev up -d                     # stores, embedder and the stand-in API on :8080
curl -s localhost:8080/v1/health

docker compose run --rm feeder-kev run kev             # one source, once
curl -s 'localhost:8080/v1/search/semantic?q=remote+code+execution+in+a+VPN+appliance&k=5'

docker compose --profile dev --profile feeders up -d   # every source, on its schedule
```

Requirements: Docker 24 or later with the Compose v2 plugin, and at least 8 GB of memory for Docker.

## Everyday commands

| Task | Command |
| --- | --- |
| One feeder, any range | `docker compose run --rm feeder-ucdp run ucdp --since 2020-01-01` |
| A feeder's log | `docker compose logs -f feeder-nvd` |
| What a run did | `docker compose run --rm --entrypoint cat feeder-kev /state/kev/runs.jsonl` |
| Rejected records | `docker compose run --rm --entrypoint cat feeder-fdic /state/fdic/rejections.jsonl` |
| A record as known at a date | `curl 'localhost:8080/v1/records/evt:kev:CVE-2023-34362?as_of=2023-06-30'` |
| Counts per source and type | `curl localhost:8080/v1/stats` |
| Schema packages | `curl localhost:8080/v1/schemas` |
| Try a package change | `curl -X PUT -H 'Content-Type: application/yaml' --data-binary @schemas/seed/usgs.yaml 'localhost:8080/v1/schemas/usgs?dry_run=true'` |
| Conformance against an API | `python tests/conformance/run.py --api http://localhost:8080` |
| Feeder tests | `cd feeders && pip install -r requirements.txt pytest && pytest -q` |

## Configuration

Every variable is listed in `.env.example`. The one every feeder shares is `SYNAPSIS_API_URL`:
`http://api-dev:8080` for the stand-in (default), `http://api:8080` for the C# API once it runs in Compose,
`http://host.docker.internal:<port>` for an API on your machine.

Keys go only into `.env`, which Git and the Docker build context ignore; Compose passes each key only to the feeder
that needs it. On a network that inspects TLS, put the proxy's CA certificate (`.crt`) in `docker/certs/` before
building.

## Data

`feeders/README.md` lists the 21 sources, their keys and schedules. Every record carries its source, the source's
reliability and its basis, the time it happened (with its precision) and the time it became known (with how that time
was established). Values a source does not give are left out, never sent as zero. Values are sent as the source
published them and never corrected silently: one that breaks its package's rules is refused with a code and listed in
the feeder's `rejections.jsonl` (negative daily case counts, for example, which are the source's revisions).
