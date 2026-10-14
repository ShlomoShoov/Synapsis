import { Callout, CodeBlock } from '../DocsBlocks';
import styles from '../GettingStartedPage.module.css';

const ingestRequest = `curl -X POST "$API_BASE/ingest" \\
  -H "Content-Type: application/json" \\
  -d '{
    "sil": "1.2",
    "batch_id": "example-batch-001",
    "producer": "example-client",
    "records": [{
      "kind": "entity",
      "id": "ent:example:organization-001",
      "type": "organization",
      "source": "src:example:feed",
      "reliability": 90,
      "reliability_basis": "analyst",
      "known_at": "2026-10-01T12:00:00Z",
      "known_at_basis": "source",
      "name": "Example organization"
    }]
  }'`;

const ingestResponse = `{
  "timestamp": "2026-10-01T12:00:01Z",
  "batch_id": "example-batch-001",
  "producer": "example-client",
  "received": 1, "new": 1, "changed": 0,
  "unchanged": 0, "duplicates": 0, "rejected": 0,
  "duration_ms": 12,
  "rejection_counts": [], "rejections": []
}`;

export function ApiIngest() {
  return (
    <>
      <p>
        Set <code>API_BASE</code> to the address supplied by your deployment. The API uses JSON with
        snake_case field names. Check <code>GET /health</code> and the deployment's Swagger page
        before sending a batch. No universal host, port, or credential is defined by the
        controllers.
      </p>
      <p>
        Use a server-side client or a client permitted by your deployment. The configured browser
        CORS policy covers GET reads; arbitrary cross-origin browser ingestion is not guaranteed.
      </p>
      <p>
        Send <code>batch_id</code>, <code>producer</code>, and up to 5,000 <code>records</code>.
        This Entity includes the runtime-required fields and the extra envelope fields and{' '}
        <code>sil</code> marker required by the SIL 1.2 schema. The API batch model uses the batch
        ID, producer, and records. All identifiers and dates below are illustrative.
      </p>
      <CodeBlock label="Illustrative POST /ingest request" code={ingestRequest} />
      <CodeBlock label="Illustrative 200 batch summary" code={ingestResponse} />
      <p>
        The summary covers the whole batch; <code>source</code> appears in per-source log documents
        rather than the API response. Accepted and rejected rows can coexist in a 200 result.
        Rejections list zero-based <code>index</code>, <code>id</code> when readable,{' '}
        <code>source</code>, <code>code</code>, and <code>reason</code>. Runtime codes include{' '}
        <code>E-JSON</code>, <code>E-MISSING</code>, and <code>E-DUPLICATE-ID</code>.
      </p>
      <Callout title="Indexing is asynchronous">
        The API stores accepted states in MongoDB. A change-stream relay then sends document IDs
        through Kafka to separate Elasticsearch and Neo4j consumers. Check the read routes later; a
        successful ingest response does not prove that both indexes are ready.
      </Callout>
    </>
  );
}

const routes = [
  {
    name: 'Map cells',
    request: 'GET /map/cells?resolution=3&bbox=34,30,36,34',
    response:
      '{ "records": 1, "sum_other_doc_count": 0, "buckets": [{ "key": "illustrative-h3-cell", "doc_count": 1, "records": { "value": 1 } }] }',
    note: 'resolution 2–5 is required; bbox is optional; kind defaults to event.',
  },
  {
    name: 'Map points',
    request: 'GET /map/points?bbox=34,30,36,34&limit=50',
    response:
      '{ "total": 1, "items": [{ "id": "example-event-001", "record_key": "illustrative-key", "state": 1, "kind": "event", "type": "observation", "source": "example-source", "known_at": "2026-10-01T12:00:00Z", "location": [35, 32] }] }',
    note: 'bbox is required; kind defaults to event; limit is 1–5,000.',
  },
  {
    name: 'Search',
    request: 'GET /search?q=example&kind=entity&limit=10',
    response:
      '{ "total": 1, "items": [{ "id": "example-entity-001", "record_key": "illustrative-key", "state": 1, "kind": "entity", "type": "organization", "source": "example-source", "known_at": "2026-10-01T12:00:00Z", "name": "Example organization" }] }',
    note: 'q, semantic, kind, logical, entity and limit are optional. semantic=true requires q; limit is 1–500.',
  },
  {
    name: 'Logical tree',
    request: 'GET /logical?node=cyber',
    response:
      '{ "sum_other_doc_count": 0, "buckets": [{ "key": "cyber/example", "doc_count": 1, "records": { "value": 1 } }] }',
    note: 'node is optional; without it the route returns the top level.',
  },
  {
    name: 'Graph',
    request: 'GET /graph?id=example-entity-001&limit=20',
    response:
      '{ "center": { "id": "example-entity-001", "labels": ["Entity"] }, "total": 1, "links": [{ "type": "RELATED", "from": "example-entity-001", "to": "example-event-001", "properties": {}, "node": { "id": "example-event-001", "labels": ["Event"] } }] }',
    note: 'id is required; limit is 1–1,000. It returns direct neighbors only.',
  },
  {
    name: 'Source states',
    request: 'GET /records/example-entity-001?as_of=2026-10-02&source=example-source',
    response:
      '[{ "source": "example-source", "state": 1, "known_at": "2026-10-01T12:00:00Z", "received_at": "2026-10-01T12:00:01Z", "batch_id": "example-batch-001", "producer": "example-client", "record": { "kind": "entity", "id": "example-entity-001", "type": "organization", "source": "example-source", "known_at": "2026-10-01T12:00:00Z", "name": "Example organization" } }]',
    note: 'as_of and a single source are optional. The route returns the latest source state known at that moment.',
  },
  {
    name: 'Catalog',
    request: 'GET /catalog',
    response: '[{ "field": "source", "value": "example-source", "count": 1 }]',
    note: 'The UI uses these source and type entries for selectors.',
  },
] as const;

export function ApiRead() {
  return (
    <>
      <p>
        These requests and response shapes are illustrative. Example IDs, source names, coordinates,
        counts, and H3 keys do not name deployed data. Fields absent from a record can be absent
        from search items.
      </p>
      {routes.map((route) => (
        <div key={route.name} className={styles.routeExample}>
          <h3>{route.name}</h3>
          <p>{route.note}</p>
          <CodeBlock label={route.request} code={route.request} />
          <CodeBlock label="Illustrative response" code={route.response} />
        </div>
      ))}
    </>
  );
}

const filterRows = [
  ['Map cells', 'Yes', 'resolution, bbox, kind'],
  ['Map points', 'Yes', 'bbox, kind, limit'],
  ['Search', 'Yes', 'q, semantic, kind, logical, entity, limit'],
  ['Logical', 'Yes', 'node'],
  ['Graph', 'No', 'id, limit'],
  ['Source states', 'No', 'as_of, single source'],
  ['Catalog', 'No', 'none'],
] as const;

export function ApiFilters() {
  return (
    <>
      <p>
        The four shared Elasticsearch filters are <code>from</code>, <code>to</code>, repeated{' '}
        <code>source</code>, and repeated <code>type</code>. Source values are OR matches within the
        source field; type values are OR matches within the type field. The time interval tests{' '}
        <code>time_start</code> as <code>[from, to)</code>, so <code>to</code> is excluded. Use UTC
        timestamps.
      </p>
      <div className={styles.tableWrap}>
        <table>
          <thead>
            <tr>
              <th>Route</th>
              <th>Shared filters</th>
              <th>Other parameters</th>
            </tr>
          </thead>
          <tbody>
            {filterRows.map(([name, shared, specific]) => (
              <tr key={name}>
                <td>{name}</td>
                <td>{shared}</td>
                <td>{specific}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <CodeBlock
        label="Repeated source and type example"
        code="GET /search?source=feed-a&source=feed-b&type=observation&type=report&from=2026-10-01T00:00:00Z&to=2026-10-02T00:00:00Z"
      />
      <p>
        A map <code>bbox</code> is <code>minLon,minLat,maxLon,maxLat</code>. Map cells need
        resolution 2–5. Map points limit is 1–5,000, Search 1–500, and Graph 1–1,000.{' '}
        <code>GET /records/{'{id}'}</code> accepts one <code>source</code>, not the repeated
        Elasticsearch filter, and its <code>as_of</code> accepts a date or date-time. A date alone
        means midnight UTC.
      </p>
      <Callout title="Errors and freshness">
        Invalid route parameters return 400. Graph and Source states return 404 when no matching
        node or record exists. A malformed ingest batch returns 400, while a valid batch can return
        200 with rejected rows. Search and Graph may lag behind an accepted ingest; no fixed
        indexing delay is promised.
      </Callout>
    </>
  );
}
