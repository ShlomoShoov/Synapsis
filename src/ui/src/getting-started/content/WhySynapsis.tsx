import { Callout } from '../DocsBlocks';
import styles from '../GettingStartedPage.module.css';

export function WhySynapsis() {
  return (
    <>
      <h3>One record, many dimensions</h3>
      <p className={styles.lead}>
        Real-world intelligence is fractured across isolated formats — cyber feeds, event logs, and
        field reports. Synapsis bridges them into a single canonical model, making any observation
        instantly searchable across space, time, semantics, and relationships.
      </p>
      <h3>Why it exists</h3>
      <p>
        Investigating complex events usually requires wrestling with multiple databases at once.
        Synapsis eliminates the friction of multi-store synchronization: you ingest a record once,
        and the system automatically projects it into the right analytical dimension — while
        preserving the untouched ground truth.
      </p>
      <div className={styles.pipeline} aria-label="Ingestion pipeline">
        <span>POST /ingest</span>
        <span>MongoDB</span>
        <span>Kafka</span>
        <span>Search + Graph</span>
      </div>
      <p className={styles.note}>
        A MongoDB change-stream relay publishes document IDs to Kafka. Separate Elasticsearch and
        Neo4j consumers build the read views asynchronously. The ingestion response confirms the
        write outcome, not that either index is already updated. See <code>IngestController</code>,{' '}
        <code>DocumentRelayOrchestrator</code>, <code>ElasticsearchOrchestrator</code>, and{' '}
        <code>Neo4jOrchestrator</code>.
      </p>
      <h3>Anchor, then pivot</h3>
      <p>
        Investigations rarely start with a graph query. Start from any clue — a location on the
        globe, a spike in time, or a semantic concept. Once anchored, pivot through the relationship
        graph to uncover hidden connections, then trace back to the raw source.
      </p>
      <p>
        Find an anchor with Search or Map, inspect direct neighbors with Graph, and open the stored
        source states to check the original record. A time spike is a clue to investigate, not an
        automatic detector or a direct graph entry point.
      </p>
    </>
  );
}

export function Primitives() {
  return (
    <>
      <p>
        Every ingested record has a <code>kind</code>, <code>id</code>, <code>type</code>,{' '}
        <code>source</code>, and <code>known_at</code>. The kind adds its own required fields;
        records may carry more detail.
      </p>
      <div className={styles.tableWrap}>
        <table>
          <thead>
            <tr>
              <th>Kind</th>
              <th>Meaning</th>
              <th>Additional required fields</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>Entity</td>
              <td>A named subject</td>
              <td>
                <code>name</code>
              </td>
            </tr>
            <tr>
              <td>Event</td>
              <td>Something occurring at a time</td>
              <td>
                <code>time.start</code>
              </td>
            </tr>
            <tr>
              <td>Relation</td>
              <td>An explicit link between records</td>
              <td>
                <code>from</code>, <code>to</code>
              </td>
            </tr>
            <tr>
              <td>Conclusion</td>
              <td>An assertion about records</td>
              <td>
                <code>about</code>, <code>statement</code>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <p>
        Conclusions can also record <code>premises</code>, <code>method</code>, and{' '}
        <code>confidence</code>. These requirements come from{' '}
        <code>RecordValidator.FindMissingField</code> and the record model classes.
      </p>
      <Callout title="Inference readiness">
        A conclusion can represent an inference with its supporting records and method. Automatic
        forecasting, model execution, and prediction publishing are planned or unavailable in the
        current application.
      </Callout>
    </>
  );
}

export function Architecture() {
  return (
    <>
      <p>
        The write path stores canonical source states before read indexes are built. Each consumer
        loads the stored record after receiving its document ID.
      </p>
      <div
        className={styles.flow}
        aria-label="POST ingest to MongoDB, change-stream relay, Kafka, then Elasticsearch and Neo4j consumers"
      >
        <span>POST /ingest</span>
        <span>MongoDB</span>
        <span>Change-stream relay</span>
        <span>Kafka</span>
        <span>
          Elasticsearch consumer
          <br />
          Neo4j consumer
        </span>
      </div>
      <Callout title="Read freshness">
        Search, Map, and Logical read Elasticsearch. Graph reads Neo4j.{' '}
        <code>GET /records/{'{id}'}</code> reads MongoDB source states. New records can appear in
        these read views at different times.
      </Callout>
      <ol>
        <li>
          Use <code>GET /search</code> or a map route to locate a record.
        </li>
        <li>
          Use <code>GET /graph?id=…</code> to inspect direct connections.
        </li>
        <li>
          Use <code>GET /records/{'{id}'}</code> to inspect the original source record and states.
        </li>
      </ol>
      <p className={styles.note}>
        Verified in <code>IngestionOrchestrator</code>, <code>DocumentRelayOrchestrator</code>,{' '}
        <code>SearchRepository</code>, <code>GraphRepository</code>, and{' '}
        <code>RecordRepository</code>.
      </p>
    </>
  );
}
