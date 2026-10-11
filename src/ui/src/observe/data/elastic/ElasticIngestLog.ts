/**
 * The ingestion log read from Elasticsearch, as contracts/synapsis-api.yaml describes it (x-elasticsearch). Every
 * search adds ignore_unavailable=true: before the API stores its first batch the index does not exist, and the log
 * is then simply empty.
 */
import { elasticUrl, ingestLogIndex } from '../../config';
import { isAbort, requestJson, type RequestOptions } from '../http';
import type {
  ActivityBucket,
  EntriesPage,
  EntriesQuery,
  IngestLog,
  MetricsSnapshot,
  PeriodTotals,
  Ping,
  SourceHistory,
  SourceSet,
  SourceWindow,
  TimeRange,
} from '../model';
import {
  parseActivity,
  parseEntries,
  parseSnapshot,
  parseSourceHistory,
  parseSourceWindows,
  parseTotals,
  type SearchResponse,
} from './parse';
import {
  activityBody,
  entriesBody,
  snapshotBody,
  sourceHistoryBody,
  sourceWindowsBody,
  totalsBody,
  type Body,
} from './queries';

const service = 'Elasticsearch';

/** The ingestion log in Elasticsearch. */
export class ElasticIngestLog implements IngestLog {
  readonly kind = 'elasticsearch';
  readonly location: string;
  private readonly searchUrl: string;

  /** Reads the log from the given address (config.elasticUrl by default) and index. */
  constructor(baseUrl: string = elasticUrl, index: string = ingestLogIndex) {
    this.location = baseUrl;
    this.searchUrl = `${baseUrl}/${encodeURIComponent(index)}/_search?ignore_unavailable=true`;
  }

  async entries(query: EntriesQuery, signal?: AbortSignal): Promise<EntriesPage> {
    return parseEntries(await this.search(entriesBody(query), signal), query.limit);
  }

  async snapshot(
    query: { range: TimeRange; sources?: SourceSet; intervalMs: number },
    signal?: AbortSignal,
  ): Promise<MetricsSnapshot> {
    const body = snapshotBody(query.range, query.sources, query.intervalMs);
    return parseSnapshot(await this.search(body, signal), query.range, query.intervalMs);
  }

  async totals(
    query: { range: TimeRange; sources?: SourceSet },
    signal?: AbortSignal,
  ): Promise<PeriodTotals> {
    return parseTotals(await this.search(totalsBody(query.range, query.sources), signal));
  }

  async activity(
    query: { range: TimeRange; sources?: SourceSet; intervalMs: number },
    signal?: AbortSignal,
  ): Promise<ActivityBucket[]> {
    const body = activityBody(query.range, query.sources, query.intervalMs);
    return parseActivity(await this.search(body, signal), query.range, query.intervalMs);
  }

  async sourceWindows(query: { range: TimeRange }, signal?: AbortSignal): Promise<SourceWindow[]> {
    return parseSourceWindows(await this.search(sourceWindowsBody(query.range), signal));
  }

  async sourceHistory(
    query: { to: number; lookbackMs: number },
    signal?: AbortSignal,
  ): Promise<SourceHistory[]> {
    const range = { from: query.to - query.lookbackMs, to: query.to };
    return parseSourceHistory(await this.search(sourceHistoryBody(range), signal));
  }

  async ping(signal?: AbortSignal): Promise<Ping> {
    const started = performance.now();
    try {
      const info = await requestJson<{ version?: { number?: string } }>(
        `${this.location}/`,
        { method: 'GET' },
        this.options(signal, 5_000),
      );
      return {
        ok: true,
        latencyMs: performance.now() - started,
        detail: `Elasticsearch ${info.version?.number ?? ''}`.trim(),
      };
    } catch (error) {
      if (isAbort(error)) {
        throw error;
      }
      return {
        ok: false,
        latencyMs: performance.now() - started,
        detail: error instanceof Error ? error.message : String(error),
      };
    }
  }

  private search(body: Body, signal?: AbortSignal): Promise<SearchResponse> {
    return requestJson<SearchResponse>(
      this.searchUrl,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      },
      this.options(signal),
    );
  }

  private options(signal?: AbortSignal, timeoutMs?: number): RequestOptions {
    return { service, signal, timeoutMs };
  }
}
