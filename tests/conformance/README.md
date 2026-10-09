# Conformance cases

Records with the answer every Synapsis API must give. The Python stand-in (`src/python/synapsis_reference`) and the
C# API (`src/dotnet`, `ConformanceTests.cs`) both run these files; a record that breaks a rule must get the same code
from both, so the rule order in the two validators must stay the same.

```bash
python tests/conformance/run.py --api http://localhost:8080            # every group
python tests/conformance/run.py --api http://localhost:8088 --only 04  # one group, against the C# API
```

The runner sends each case as one bundle to `POST /ingest?dry_run=true`, so nothing is written. It needs only the
Python standard library; it first registers the feeders' schema packages (`feeders/packages/`) that the cases use.

## Files

| File | What it covers |
| --- | --- |
| `cases/01-valid.json` | Valid records of every kind, which must be accepted |
| `cases/02-envelope.json` | Kind, structure, vocabularies, source, identity and type; the first broken rule decides |
| `cases/03-time.json` | Alignment to each precision, end after start, known before it happened, known in the future |
| `cases/04-place.json` | Place forms, ISO codes, logical spaces (unknown, too deep, outside a closed space) |
| `cases/05-measures-text-attributes.json` | Allowed measures, units, signs, ranges; required text; declared attributes |
| `cases/06-batch.json` | Duplicates, id conflicts, roles, references, relation domains, premises, classifications (A-CLOSED) |
| `cases/07-requests.json` | Answers to the whole request: malformed bundles, non-JSON bodies, too many records |

## Format

A record case:

```json
{
  "name": "a negative value for an unsigned measure",
  "records": [ { "kind": "entity", "...": "..." }, { "kind": "event", "...": "..." } ],
  "rejected": [ { "index": 1, "code": "E-MEASURE-NEGATIVE", "path": "measures[2].value" } ],
  "duplicates": 0
}
```

Every record not listed in `rejected` must be accepted. `path` is given where the rule fixes it; it uses the form
`measures[2].value`, and an empty path means the record itself. Errors found by the JSON Schema of the envelope are
compared by code only, because schema libraries report the first of several structural errors in different orders.

A request case gives a `bundle` override, a `raw` body or a `repeat` count, and the expected HTTP `status` and `code`.

Cases that need stored data (a new version, an immutable type changed, a batch id reused with other content) are not
here: they write, so they live in each implementation's own tests (`VersionTests.cs`, `IngestTests.cs`).
