/**
 * The pipeline below the API, read from Elasticsearch (contracts/pipeline-log.md): the API's ingestion log
 * (synapsis-ingest-log, one document per batch and source) and the services' log (synapsis-pipeline-log, one document
 * per service every 30 seconds, and one per engine run). Two searches of aggregations; rates, totals and the time to
 * catch up are worked out here.
 */
import { elasticUrl, ingestLogIndex } from '../config';
import { requestJson } from '../data/http';

/** The index the relay, the indexer, the projector and the engine write their counters to. */
export const pipelineLogIndex = 'synapsis-pipeline-log';

/** How often the section reads the logs again (the services write every 30 seconds). */
export const pipelinePollMs = 30_000;

/** The period the counts cover: the last 24 hours. */
const dayMs = 86_400_000;

/** The period the API's rate is measured over. */
const recentMs = 300_000;

/** One heartbeat (30 s) and the time a service's rate is measured over (two heartbeats). */
const heartbeatMs = 30_000;
const serviceRateMs = 60_000;

/** The documents read per service: enough for two heartbeats of every copy. */
const recentDocs = 40;

/** A service silent for longer than this (four heartbeats) is shown as quiet. */
export const silentMs = 120_000;

/** One stage of the flow, in the order a record goes through them. */
export interface Stage {
  id: 'api' | 'relay' | 'indexer' | 'projector' | 'engine';
  title: string;
  /** Where it writes. */
  into: string;
  /** Handled per second now; null when not known. */
  rate: number | null;
  /** Handled in the last 24 hours (the engine: conclusions sent). */
  handled: number;
  /** Refused (the API) or failed (a service) in the last 24 hours. */
  failed: number;
  /** Handled with nothing to write in the last 24 hours. */
  skipped: number;
  /** Kafka messages still waiting for it (the indexer and the projector), all its copies together. */
  lag: number | null;
  /** How many copies of the service wrote in the last heartbeat (several indexers share the queue). */
  instances?: number;
  /** When it last wrote to its log; null when never. */
  lastAt: number | null;
  /** Its last word, when it has one (the engine's result). */
  message?: string;
}

/** One source, from the API's log: everything it sent, ever. */
export interface SourceRow {
  source: string;
  producers: string[];
  batches: number;
  received: number;
  /** New and changed states. */
  stored: number;
  refused: number;
  lastAt: number | null;
}

/** One run of the engine for one source. */
export interface EngineRun {
  at: number;
  source: string;
  written: number;
  message: string;
}

export interface Pipeline {
  stages: Stage[];
  sources: SourceRow[];
  runs: EngineRun[];
}

interface Sum {
  value: number | null;
}
interface Max {
  value: number | null;
}
interface Hit<T> {
  _source: T;
}
interface Hits<T> {
  hits: { hits: Hit<T>[] };
}
interface Buckets<T> {
  buckets: (T & { key: string; doc_count: number })[];
}
type Counts = { received: Sum; new: Sum; changed: Sum; rejected: Sum };

/** The answer of ingestBody. */
export interface IngestAnswer {
  aggregations?: {
    day: Counts & { last: Max; recent: { received: Sum } };
    sources: Buckets<Counts & { last: Max; producers: Buckets<object> }>;
  };
}

interface ServiceDoc {
  at: string;
  processed?: number;
  lag?: number;
  event?: string;
}
interface RunDoc {
  at: string;
  source?: string;
  written?: number;
  message?: string;
}

/** The answer of servicesBody. */
export interface ServicesAnswer {
  aggregations?: {
    services: Buckets<{
      latest: Hits<ServiceDoc>;
      day: { processed: Sum; written: Sum; skipped: Sum; failed: Sum };
    }>;
    runs: { latest: Hits<RunDoc> };
  };
}

const sum = (field: string) => ({ sum: { field } });
const counts = {
  received: sum('received'),
  new: sum('new'),
  changed: sum('changed'),
  rejected: sum('rejected'),
};

/** The API's log: the last 24 hours, the last 5 minutes, and every source ever. */
export function ingestBody(now: number) {
  return {
    size: 0,
    aggs: {
      day: {
        filter: { range: { timestamp: { gte: new Date(now - dayMs).toISOString() } } },
        aggs: {
          ...counts,
          last: { max: { field: 'timestamp' } },
          recent: {
            filter: { range: { timestamp: { gte: new Date(now - recentMs).toISOString() } } },
            aggs: { received: sum('received') },
          },
        },
      },
      sources: {
        terms: { field: 'source', size: 200, order: { received: 'desc' } },
        aggs: {
          ...counts,
          last: { max: { field: 'timestamp' } },
          producers: { terms: { field: 'producer', size: 5 } },
        },
      },
    },
  };
}

/** The services' log: each service's last documents and its last 24 hours, and the engine's last runs. */
export function servicesBody(now: number) {
  return {
    size: 0,
    aggs: {
      services: {
        terms: { field: 'service', size: 10 },
        aggs: {
          latest: {
            top_hits: {
              size: recentDocs,
              sort: [{ at: 'desc' }],
              _source: ['at', 'processed', 'lag', 'event'],
            },
          },
          day: {
            filter: { range: { at: { gte: new Date(now - dayMs).toISOString() } } },
            aggs: {
              processed: sum('processed'),
              written: sum('written'),
              skipped: sum('skipped'),
              failed: sum('failed'),
            },
          },
        },
      },
      runs: {
        filter: { term: { event: 'run' } },
        aggs: {
          latest: {
            top_hits: {
              size: 12,
              sort: [{ at: 'desc' }],
              _source: ['at', 'source', 'written', 'message'],
            },
          },
        },
      },
    },
  };
}

const valueOf = (sum: Sum | undefined) => sum?.value ?? 0;
const timeOf = (iso: string | undefined) => {
  const ms = iso ? Date.parse(iso) : Number.NaN;
  return Number.isNaN(ms) ? null : ms;
};

/** The two answers as stages, sources and runs. */
export function pipelineOf(ingest: IngestAnswer, services: ServicesAnswer): Pipeline {
  const day = ingest.aggregations?.day;
  const api: Stage = {
    id: 'api',
    title: 'API',
    into: 'MongoDB',
    rate: day ? valueOf(day.recent.received) / (recentMs / 1000) : null,
    handled: valueOf(day?.received),
    failed: valueOf(day?.rejected),
    skipped: 0,
    lag: null,
    lastAt: day?.last.value ?? null,
  };

  const byService = new Map((services.aggregations?.services.buckets ?? []).map((b) => [b.key, b]));
  const service = (id: 'relay' | 'indexer' | 'projector', title: string, into: string): Stage => {
    const bucket = byService.get(id);
    const docs = (bucket?.latest.hits.hits ?? []).map((hit) => hit._source);
    const [latest, previous] = docs;
    const lastAt = timeOf(latest?.at);
    const previousAt = timeOf(previous?.at);
    const seconds = lastAt !== null && previousAt !== null ? (lastAt - previousAt) / 1000 : 0;
    // Several copies of a service (the indexers) each write a heartbeat every 30 s with their own share of the queue
    // (one Kafka partition each). The windows end at the last document, not at this clock, so a heartbeat
    // Elasticsearch does not show yet never drops a copy: one heartbeat per copy in the last 30 s, two in the last 60.
    const since = (ms: number) =>
      docs.filter((doc) => lastAt !== null && (timeOf(doc.at) ?? 0) > lastAt - ms);
    const beat = since(heartbeatMs).filter((doc) => doc.event === 'heartbeat');
    const instances = beat.length;
    const shared = instances > 1;
    const total = (list: ServiceDoc[], field: 'processed' | 'lag') =>
      list.reduce((acc, doc) => acc + (doc[field] ?? 0), 0);
    return {
      id,
      title,
      into,
      rate:
        latest?.event === 'stop'
          ? 0
          : shared
            ? total(since(serviceRateMs), 'processed') / (serviceRateMs / 1000)
            : seconds > 0
              ? (latest?.processed ?? 0) / seconds
              : null,
      handled: valueOf(bucket?.day.processed),
      failed: valueOf(bucket?.day.failed),
      skipped: valueOf(bucket?.day.skipped),
      lag: shared ? total(beat, 'lag') : (latest?.lag ?? null),
      instances: shared ? instances : undefined,
      lastAt,
    };
  };

  const runs = (services.aggregations?.runs.latest.hits.hits ?? []).map(({ _source: run }) => ({
    at: timeOf(run.at) ?? 0,
    source: run.source ?? '',
    written: run.written ?? 0,
    message: run.message ?? '',
  }));
  const engine = byService.get('engine');
  const engineStage: Stage = {
    id: 'engine',
    title: 'Engine',
    into: 'API',
    rate: null,
    handled: valueOf(engine?.day.written),
    failed: valueOf(engine?.day.failed),
    skipped: 0,
    lag: null,
    lastAt: runs[0]?.at ?? null,
    message: runs[0]?.message,
  };

  const sources = (ingest.aggregations?.sources.buckets ?? []).map((bucket) => ({
    source: bucket.key,
    producers: bucket.producers.buckets.map((producer) => producer.key),
    batches: bucket.doc_count,
    received: valueOf(bucket.received),
    stored: valueOf(bucket.new) + valueOf(bucket.changed),
    refused: valueOf(bucket.rejected),
    lastAt: bucket.last.value,
  }));

  return {
    stages: [
      api,
      service('relay', 'Relay', 'Kafka'),
      service('indexer', 'Indexer', 'Elasticsearch'),
      service('projector', 'Projector', 'Neo4j'),
      engineStage,
    ],
    sources,
    runs,
  };
}

/** How long a stage needs to empty its queue at its present rate, in ms; null when it cannot be said. */
export function catchUpMs(stage: Stage): number | null {
  if (!stage.lag || !stage.rate || stage.rate <= 0) return null;
  return (stage.lag / stage.rate) * 1000;
}

/** Reads both logs. A log not created yet reads as empty. */
export async function readPipeline(now: number, signal?: AbortSignal): Promise<Pipeline> {
  const search = <T>(index: string, body: object) =>
    requestJson<T>(
      `${elasticUrl}/${index}/_search?ignore_unavailable=true`,
      {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(body),
      },
      { service: 'Elasticsearch', signal },
    );
  const [ingest, services] = await Promise.all([
    search<IngestAnswer>(ingestLogIndex, ingestBody(now)),
    search<ServicesAnswer>(pipelineLogIndex, servicesBody(now)),
  ]);
  return pipelineOf(ingest, services);
}
