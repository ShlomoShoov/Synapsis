# Contract: the pipeline logs in Elasticsearch

Agreed on 13 Oct 2026. Writers: agent 2 (relay, indexer, projector, engine). Readers: the Observe screen (agent 4),
the night checks. Change it only by editing this file first.

The rule: each service writes the simplest possible log, a few counters, straight to Elasticsearch. No new library,
no new abstraction, no change to what the service does. Never one document per record: the indexer and the projector
are the bottleneck (about 60 records/s), so a log write per record would halve them.

## 1. `synapsis-ingest-log` (exists, written by the API, unchanged)
One document per POST /ingest batch, per source in the batch:
`timestamp` (date), `batch_id`, `producer` (for example "feeder:kev", "engine"), `source`, `received`, `new`,
`changed`, `unchanged`, `duplicates`, `rejected` (integers), `duration_ms`, `rejection_counts` [{code, count}],
`rejections` [the refused records]. This is the "API" point of the flow, and through `producer` also the feeders'.

## 2. `synapsis-pipeline-log` (new; the mapping is created by Setup, like the ingest log)
One document per service every 30 seconds while it runs, and one at start, at stop and on an error.

| Field | Type | Meaning |
| --- | --- | --- |
| `at` | date | when the document was written (UTC) |
| `service` | keyword | `relay`, `indexer`, `projector` or `engine` |
| `event` | keyword | `start`, `heartbeat`, `stop`, `error`, `run` (engine) |
| `processed` | long | messages or records handled since this service's previous document |
| `written` | long | what it wrote in that time: relay = messages published to Kafka; indexer = documents written to Elasticsearch; projector = records written to Neo4j; engine = conclusions sent |
| `skipped` | long | handled but nothing to write (for example a type kept out of the graph) |
| `failed` | long | failures in that time (each one also logged by the service's normal logger) |
| `lag` | long, may be absent | Kafka messages still waiting for this consumer group (indexer, projector), when the service can read it cheaply |
| `source` | keyword, may be absent | the engine: the source a `run` document is about |
| `message` | text, may be absent | one short sentence: the error, or the engine's result |

Rates are computed by the reader: `processed` / the seconds since the previous document of the same service.

The engine writes one `run` document per source and step (`backfill`, `verify`): `written` = conclusions sent,
`message` = for verify the summary ("7/7 proofs; golden 5 pass, 0 fail, 3 pending").

Example:
```json
{"at": "2026-10-14T01:30:00Z", "service": "indexer", "event": "heartbeat",
 "processed": 1830, "written": 1822, "skipped": 8, "failed": 0, "lag": 412530}
```

## 3. The flow Observe shows
feeders → API (`synapsis-ingest-log`) → MongoDB → relay → Kafka → indexer → Elasticsearch, projector → Neo4j →
engine → API (the engine's batches appear again in `synapsis-ingest-log` with producer "engine").
