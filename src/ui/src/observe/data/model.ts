/**
 * The shapes Observe works with, and the two interfaces every data source implements: IngestLog (the ingestion
 * log) and SchemaRegistry (the schema packages). Two implementations exist for each: one reads the real services
 * (./elastic, ./api), one plays a simulation (./simulation), so every view works the same on both.
 *
 * Times are milliseconds since 1970 (UTC), as Date.now() gives them. Ranges include `from` and exclude `to`.
 */
import type { SchemaDocument, SchemaSummary } from './contracts';

/** How many records of a set ended in each outcome. received = new + changed + unchanged + duplicates + rejected. */
export interface OutcomeCounts {
  received: number;
  new: number;
  changed: number;
  unchanged: number;
  duplicates: number;
  rejected: number;
}

/** One refused record, as the log keeps it. */
export interface LoggedRejection {
  /** Its position in the batch, from 0. */
  index: number;
  id: string | null;
  code: string;
  /** Where in the record the problem is, such as "measures[2].unit". */
  path: string;
}

/** One log entry: what one batch brought from one source. */
export interface LogEntry extends OutcomeCounts {
  /** The document id: unique, used to recognise an entry seen before. */
  id: string;
  /** When the API received the batch. */
  time: number;
  batchId: string;
  producer: string;
  source: string;
  rejectionCounts: { code: string; count: number }[];
  /** The first 50 refused records. */
  rejections: LoggedRejection[];
  durationMs: number;
}

/** A span of time: from (included) to (excluded). */
export interface TimeRange {
  from: number;
  to: number;
}

/** Which sources a question is about: null means every source. */
export type SourceSet = readonly string[] | null;

/** A page of log entries, and where the next page starts (null when there is none). */
export interface EntriesPage {
  entries: LogEntry[];
  next: EntriesCursor | null;
}

/** Where a page of entries continues: opaque to callers. */
export type EntriesCursor = readonly (string | number)[];

/** A question for log entries. */
export interface EntriesQuery {
  range: TimeRange;
  sources?: SourceSet;
  /** Oldest first ("asc") or newest first ("desc"). */
  order: 'asc' | 'desc';
  limit: number;
  /** Continue after this cursor (from a previous page). */
  after?: EntriesCursor | null;
  /** Keep only entries with at least one rejected record. */
  onlyRejected?: boolean;
  /** Keep only batches whose id starts with this text. */
  batchPrefix?: string;
}

/** Batch durations over a set of entries, in milliseconds (null when there were none). */
export interface DurationStats {
  p50: number | null;
  p95: number | null;
  p99: number | null;
  avg: number | null;
  max: number | null;
}

/** Totals over a period. */
export interface PeriodTotals extends OutcomeCounts {
  /** Log entries: one per batch and source. */
  entries: number;
  /** Distinct batches, that is, ingest requests. */
  batches: number;
  /** Distinct producers. */
  producers: number;
  /** Distinct sources with at least one entry. */
  activeSources: number;
  duration: DurationStats;
}

/** What one source sent over a period. */
export interface SourceStats extends OutcomeCounts {
  source: string;
  entries: number;
  batches: number;
  /** The first and the last time a batch of it was received in the period. */
  first: number;
  last: number;
  durationAvg: number | null;
  /** Refused records per error code. */
  codes: { code: string; count: number }[];
}

/** One error code over a period: the records it refused, and from which sources. */
export interface CodeStats {
  code: string;
  count: number;
  sources: { source: string; count: number }[];
}

/** One interval of a time chart. */
export interface TimeBucket extends OutcomeCounts {
  start: number;
  entries: number;
  batches: number;
  durationP50: number | null;
  durationP95: number | null;
  /** Records received per source in this interval. */
  bySource: Record<string, number>;
}

/** Everything the metrics dashboards show for one period and one set of sources, read at once. */
export interface MetricsSnapshot {
  range: TimeRange;
  intervalMs: number;
  totals: PeriodTotals;
  sources: SourceStats[];
  codes: CodeStats[];
  buckets: TimeBucket[];
}

/** One interval of the time-travel bar. */
export interface ActivityBucket {
  start: number;
  received: number;
  rejected: number;
  entries: number;
  /** Records received per source. */
  bySource: Record<string, number>;
}

/** One source over a window of time (the scene's state at a moment). */
export interface SourceWindow {
  source: string;
  received: number;
  rejected: number;
  entries: number;
  /** The last time a batch of it was received in the window. */
  last: number;
}

/** One source over a long history: when it began, when it last sent, and how often. */
export interface SourceHistory {
  source: string;
  first: number;
  last: number;
  batches: number;
  entries: number;
}

/** The answer of a connection check. */
export interface Ping {
  ok: boolean;
  /** How long the answer took. */
  latencyMs: number;
  /** A short description: the version of Elasticsearch, "Healthy", or what went wrong. */
  detail: string;
}

/** The ingestion log. */
export interface IngestLog {
  /** Where the answers come from. */
  readonly kind: 'elasticsearch' | 'simulated';
  /** A human description of the place, such as the address of Elasticsearch. */
  readonly location: string;
  /** Log entries, a page at a time. */
  entries(query: EntriesQuery, signal?: AbortSignal): Promise<EntriesPage>;
  /** Everything the dashboards need for one period, with time charts in intervals of `intervalMs`. */
  snapshot(
    query: { range: TimeRange; sources?: SourceSet; intervalMs: number },
    signal?: AbortSignal,
  ): Promise<MetricsSnapshot>;
  /** The totals of a period only (used to compare with the period before). */
  totals(
    query: { range: TimeRange; sources?: SourceSet },
    signal?: AbortSignal,
  ): Promise<PeriodTotals>;
  /** Records per interval, for the time-travel bar. */
  activity(
    query: { range: TimeRange; sources?: SourceSet; intervalMs: number },
    signal?: AbortSignal,
  ): Promise<ActivityBucket[]>;
  /** Each source over one window of time. */
  sourceWindows(query: { range: TimeRange }, signal?: AbortSignal): Promise<SourceWindow[]>;
  /** Each source over its history up to `to`, looking back `lookbackMs`. */
  sourceHistory(
    query: { to: number; lookbackMs: number },
    signal?: AbortSignal,
  ): Promise<SourceHistory[]>;
  /** Whether the log answers. */
  ping(signal?: AbortSignal): Promise<Ping>;
}

/** The schema packages. */
export interface SchemaRegistry {
  readonly kind: 'api' | 'simulated';
  readonly location: string;
  /** The list: name, version and fingerprint of each package. */
  list(signal?: AbortSignal): Promise<SchemaSummary[]>;
  /** Every package with its content. */
  all(signal?: AbortSignal): Promise<SchemaDocument[]>;
  /** Whether the API answers. */
  ping(signal?: AbortSignal): Promise<Ping>;
}

/** Zero of every outcome. */
export function emptyCounts(): OutcomeCounts {
  return { received: 0, new: 0, changed: 0, unchanged: 0, duplicates: 0, rejected: 0 };
}

/** Adds the counts of `add` into `into`. */
export function addCounts(into: OutcomeCounts, add: OutcomeCounts): OutcomeCounts {
  into.received += add.received;
  into.new += add.new;
  into.changed += add.changed;
  into.unchanged += add.unchanged;
  into.duplicates += add.duplicates;
  into.rejected += add.rejected;
  return into;
}

/** The records stored by a set: new ones and new versions. */
export function writtenOf(counts: OutcomeCounts): number {
  return counts.new + counts.changed;
}
