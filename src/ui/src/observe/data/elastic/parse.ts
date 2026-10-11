/**
 * Reads Elasticsearch answers into Observe's shapes (../model.ts). Every field is read defensively: an index that does
 * not exist yet answers without aggregations (the API creates it on the first stored batch), and an aggregation over
 * no documents answers null. Both become zeros and empty lists, never errors.
 */
import type { IngestLogDocument } from '../contracts';
import {
  emptyCounts,
  type ActivityBucket,
  type CodeStats,
  type DurationStats,
  type EntriesCursor,
  type LogEntry,
  type MetricsSnapshot,
  type OutcomeCounts,
  type PeriodTotals,
  type SourceHistory,
  type SourceStats,
  type SourceWindow,
  type TimeBucket,
  type TimeRange,
} from '../model';
import { outcomeFields } from './queries';

/** One hit of a search. */
export interface Hit {
  _id: string;
  _source?: Partial<IngestLogDocument>;
  sort?: (string | number)[];
}

/** The parts of a search answer Observe reads. */
export interface SearchResponse {
  hits?: { total?: { value?: number }; hits?: Hit[] };
  aggregations?: Agg;
}

/** An aggregation result: a loose tree, read with the helpers below. */
type Agg = { [key: string]: unknown };

const agg = (parent: Agg | undefined, name: string): Agg | undefined =>
  (parent?.[name] as Agg | undefined) ?? undefined;

const num = (value: unknown): number =>
  typeof value === 'number' && Number.isFinite(value) ? value : 0;

const numOrNull = (value: unknown): number | null =>
  typeof value === 'number' && Number.isFinite(value) ? value : null;

/** The value of a metric aggregation (sum, min, max, avg, cardinality), 0 when it has none. */
const metric = (parent: Agg | undefined, name: string): number => num(agg(parent, name)?.value);

/** The value of a metric aggregation, null when it has none (min and max over no documents). */
const metricOrNull = (parent: Agg | undefined, name: string): number | null =>
  numOrNull(agg(parent, name)?.value);

/** The buckets of a bucket aggregation. */
const bucketsOf = (parent: Agg | undefined, name: string): Agg[] =>
  (agg(parent, name)?.buckets as Agg[] | undefined) ?? [];

/** One percentile of a percentiles aggregation ("95.0" in Elasticsearch's answer). */
function percentile(parent: Agg | undefined, name: string, percent: number): number | null {
  const values = agg(parent, name)?.values as Record<string, unknown> | undefined;
  if (!values) {
    return null;
  }
  return numOrNull(values[percent.toFixed(1)] ?? values[String(percent)]);
}

/** The six counts, from sum aggregations named after the fields. */
function countsOf(parent: Agg | undefined): OutcomeCounts {
  const counts = emptyCounts();
  for (const field of outcomeFields) {
    counts[field] = metric(parent, field);
  }
  return counts;
}

/** A log document as a LogEntry. */
export function toEntry(hit: Hit): LogEntry {
  const doc = hit._source ?? {};
  return {
    id: hit._id,
    time: Date.parse(doc.timestamp ?? '') || 0,
    batchId: doc.batch_id ?? '',
    producer: doc.producer ?? '',
    source: doc.source ?? 'unknown',
    received: num(doc.received),
    new: num(doc.new),
    changed: num(doc.changed),
    unchanged: num(doc.unchanged),
    duplicates: num(doc.duplicates),
    rejected: num(doc.rejected),
    rejectionCounts: (doc.rejection_counts ?? []).map((item) => ({
      code: String(item.code),
      count: num(item.count),
    })),
    rejections: (doc.rejections ?? []).map((item) => ({
      index: num(item.index),
      id: item.id ?? null,
      code: String(item.code),
      path: String(item.path ?? ''),
    })),
    durationMs: num(doc.duration_ms),
  };
}

/** The entries of a search and the cursor of the next page (none when the page was not full). */
export function parseEntries(
  response: SearchResponse,
  limit: number,
): { entries: LogEntry[]; next: EntriesCursor | null } {
  const hits = response.hits?.hits ?? [];
  const last = hits[hits.length - 1];
  return {
    entries: hits.map(toEntry),
    next: hits.length >= limit && last?.sort ? last.sort : null,
  };
}

/** Batch durations from the "duration" percentiles and "duration_stats" aggregations. */
function durationOf(aggs: Agg | undefined): DurationStats {
  const stats = agg(aggs, 'duration_stats');
  return {
    p50: percentile(aggs, 'duration', 50),
    p95: percentile(aggs, 'duration', 95),
    p99: percentile(aggs, 'duration', 99),
    avg: numOrNull(stats?.avg),
    max: numOrNull(stats?.max),
  };
}

/** The totals of a period. */
export function parseTotals(response: SearchResponse): PeriodTotals {
  const aggs = response.aggregations;
  return {
    ...countsOf(aggs),
    entries: num(response.hits?.total?.value),
    batches: metric(aggs, 'batches'),
    producers: metric(aggs, 'producers'),
    activeSources: metric(aggs, 'active_sources'),
    duration: durationOf(aggs),
  };
}

/**
 * The starts of every interval from the one holding `range.from` to the one holding the last moment of the range,
 * as Elasticsearch's date histogram aligns them (on whole multiples of the interval since 1970).
 */
export function intervalStarts(range: TimeRange, intervalMs: number): number[] {
  const starts: number[] = [];
  const first = Math.floor(range.from / intervalMs) * intervalMs;
  for (let start = first; start < range.to; start += intervalMs) {
    starts.push(start);
  }
  return starts;
}

/** Records per source inside one interval, from its "sources" terms aggregation. */
function bySourceOf(bucket: Agg): Record<string, number> {
  const result: Record<string, number> = {};
  for (const item of bucketsOf(bucket, 'sources')) {
    result[String(item.key)] = metric(item, 'received');
  }
  return result;
}

/** Everything the dashboards show, from one snapshot question. */
export function parseSnapshot(
  response: SearchResponse,
  range: TimeRange,
  intervalMs: number,
): MetricsSnapshot {
  const aggs = response.aggregations;
  const sources: SourceStats[] = bucketsOf(aggs, 'sources').map((bucket) => ({
    source: String(bucket.key),
    ...countsOf(bucket),
    entries: num(bucket.doc_count),
    batches: metric(bucket, 'batches'),
    first: metric(bucket, 'first'),
    last: metric(bucket, 'last'),
    durationAvg: metricOrNull(bucket, 'duration'),
    codes: bucketsOf(agg(bucket, 'codes'), 'by_code')
      .map((code) => ({ code: String(code.key), count: metric(code, 'count') }))
      .filter((code) => code.count > 0),
  }));

  const codes: CodeStats[] = bucketsOf(agg(aggs, 'codes'), 'by_code')
    .map((bucket) => {
      const code = String(bucket.key);
      return {
        code,
        count: metric(bucket, 'count'),
        sources: sources
          .map((source) => ({
            source: source.source,
            count: source.codes.find((item) => item.code === code)?.count ?? 0,
          }))
          .filter((item) => item.count > 0)
          .sort((a, b) => b.count - a.count),
      };
    })
    .filter((code) => code.count > 0)
    .sort((a, b) => b.count - a.count);

  const answered = new Map<number, Agg>();
  for (const bucket of bucketsOf(aggs, 'timeline')) {
    answered.set(num(bucket.key), bucket);
  }
  const buckets: TimeBucket[] = intervalStarts(range, intervalMs).map((start) => {
    const bucket = answered.get(start);
    return {
      start,
      ...countsOf(bucket),
      entries: num(bucket?.doc_count),
      batches: metric(bucket, 'batches'),
      durationP50: percentile(bucket, 'duration', 50),
      durationP95: percentile(bucket, 'duration', 95),
      bySource: bucket ? bySourceOf(bucket) : {},
    };
  });

  return { range, intervalMs, totals: parseTotals(response), sources, codes, buckets };
}

/** The intervals of the time-travel bar. */
export function parseActivity(
  response: SearchResponse,
  range: TimeRange,
  intervalMs: number,
): ActivityBucket[] {
  const answered = new Map<number, Agg>();
  for (const bucket of bucketsOf(response.aggregations, 'timeline')) {
    answered.set(num(bucket.key), bucket);
  }
  return intervalStarts(range, intervalMs).map((start) => {
    const bucket = answered.get(start);
    return {
      start,
      received: metric(bucket, 'received'),
      rejected: metric(bucket, 'rejected'),
      entries: num(bucket?.doc_count),
      bySource: bucket ? bySourceOf(bucket) : {},
    };
  });
}

/** Each source over one window. */
export function parseSourceWindows(response: SearchResponse): SourceWindow[] {
  return bucketsOf(response.aggregations, 'sources').map((bucket) => ({
    source: String(bucket.key),
    received: metric(bucket, 'received'),
    rejected: metric(bucket, 'rejected'),
    entries: num(bucket.doc_count),
    last: metric(bucket, 'last'),
  }));
}

/** Each source over its history. */
export function parseSourceHistory(response: SearchResponse): SourceHistory[] {
  return bucketsOf(response.aggregations, 'sources').map((bucket) => ({
    source: String(bucket.key),
    first: metric(bucket, 'first'),
    last: metric(bucket, 'last'),
    batches: metric(bucket, 'batches'),
    entries: num(bucket.doc_count),
    activeHours: bucketsOf(bucket, 'hours').map((hour) => num(hour.key)),
  }));
}
