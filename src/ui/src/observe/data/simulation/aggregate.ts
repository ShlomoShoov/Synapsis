/**
 * The aggregations of ../elastic/queries.ts, computed in memory over a list of entries: the simulation answers the
 * same questions with the same shapes, so the views cannot tell the two apart. Percentiles are exact here and
 * estimated in Elasticsearch (TDigest); on the simulation's few thousand entries the two agree closely.
 */
import { intervalStarts } from '../elastic/parse';
import {
  addCounts,
  emptyCounts,
  type ActivityBucket,
  type CodeStats,
  type DurationStats,
  type LogEntry,
  type MetricsSnapshot,
  type PeriodTotals,
  type SourceHistory,
  type SourceStats,
  type SourceWindow,
  type TimeBucket,
  type TimeRange,
} from '../model';

/** The value at `percent` (0-100) of sorted numbers, interpolated between the two nearest. */
export function percentileOf(sorted: readonly number[], percent: number): number | null {
  if (sorted.length === 0) {
    return null;
  }
  const rank = (percent / 100) * (sorted.length - 1);
  const low = Math.floor(rank);
  const high = Math.ceil(rank);
  const a = sorted[low] as number;
  const b = sorted[high] as number;
  return a + (b - a) * (rank - low);
}

function durationsOf(entries: readonly LogEntry[]): DurationStats {
  const sorted = entries.map((entry) => entry.durationMs).sort((a, b) => a - b);
  const sum = sorted.reduce((total, value) => total + value, 0);
  return {
    p50: percentileOf(sorted, 50),
    p95: percentileOf(sorted, 95),
    p99: percentileOf(sorted, 99),
    avg: sorted.length ? sum / sorted.length : null,
    max: sorted.length ? (sorted[sorted.length - 1] as number) : null,
  };
}

/** The totals of a list of entries. */
export function totalsOf(entries: readonly LogEntry[]): PeriodTotals {
  const counts = emptyCounts();
  const batches = new Set<string>();
  const producers = new Set<string>();
  const sources = new Set<string>();
  for (const entry of entries) {
    addCounts(counts, entry);
    batches.add(entry.batchId);
    producers.add(entry.producer);
    sources.add(entry.source);
  }
  return {
    ...counts,
    entries: entries.length,
    batches: batches.size,
    producers: producers.size,
    activeSources: sources.size,
    duration: durationsOf(entries),
  };
}

/** The time of the first entry of a group (a loop, so a group of any size is fine). */
function earliest(entries: readonly LogEntry[]): number {
  let time = Infinity;
  for (const entry of entries) {
    time = Math.min(time, entry.time);
  }
  return time;
}

/** The time of the last entry of a group. */
function latest(entries: readonly LogEntry[]): number {
  let time = -Infinity;
  for (const entry of entries) {
    time = Math.max(time, entry.time);
  }
  return time;
}

/** Groups entries by a key, keeping their order. */
function groupBy<K>(entries: readonly LogEntry[], key: (entry: LogEntry) => K): Map<K, LogEntry[]> {
  const groups = new Map<K, LogEntry[]>();
  for (const entry of entries) {
    const k = key(entry);
    const group = groups.get(k);
    if (group) {
      group.push(entry);
    } else {
      groups.set(k, [entry]);
    }
  }
  return groups;
}

/** Refused records per code over entries, largest first. */
function codeCounts(entries: readonly LogEntry[]): { code: string; count: number }[] {
  const counts = new Map<string, number>();
  for (const entry of entries) {
    for (const item of entry.rejectionCounts) {
      counts.set(item.code, (counts.get(item.code) ?? 0) + item.count);
    }
  }
  return [...counts].map(([code, count]) => ({ code, count })).sort((a, b) => b.count - a.count);
}

/** The interval each entry falls in, by its start. */
function byInterval(entries: readonly LogEntry[], intervalMs: number): Map<number, LogEntry[]> {
  return groupBy(entries, (entry) => Math.floor(entry.time / intervalMs) * intervalMs);
}

function receivedBySource(entries: readonly LogEntry[]): Record<string, number> {
  const result: Record<string, number> = {};
  for (const entry of entries) {
    result[entry.source] = (result[entry.source] ?? 0) + entry.received;
  }
  return result;
}

/** Everything the dashboards show, from the entries of the period. */
export function snapshotOf(
  entries: readonly LogEntry[],
  range: TimeRange,
  intervalMs: number,
): MetricsSnapshot {
  const sources: SourceStats[] = [...groupBy(entries, (entry) => entry.source)]
    .map(([source, group]) => {
      const durations = durationsOf(group);
      return {
        source,
        ...group.reduce((counts, entry) => addCounts(counts, entry), emptyCounts()),
        entries: group.length,
        batches: new Set(group.map((entry) => entry.batchId)).size,
        first: earliest(group),
        last: latest(group),
        durationAvg: durations.avg,
        codes: codeCounts(group),
      };
    })
    .sort((a, b) => b.entries - a.entries);

  const codes: CodeStats[] = codeCounts(entries).map(({ code, count }) => ({
    code,
    count,
    sources: sources
      .map((source) => ({
        source: source.source,
        count: source.codes.find((item) => item.code === code)?.count ?? 0,
      }))
      .filter((item) => item.count > 0)
      .sort((a, b) => b.count - a.count),
  }));

  const intervals = byInterval(entries, intervalMs);
  const buckets: TimeBucket[] = intervalStarts(range, intervalMs).map((start) => {
    const group = intervals.get(start) ?? [];
    const durations = durationsOf(group);
    return {
      start,
      ...group.reduce((counts, entry) => addCounts(counts, entry), emptyCounts()),
      entries: group.length,
      batches: new Set(group.map((entry) => entry.batchId)).size,
      durationP50: durations.p50,
      durationP95: durations.p95,
      bySource: receivedBySource(group),
    };
  });

  return { range, intervalMs, totals: totalsOf(entries), sources, codes, buckets };
}

/** The intervals of the time-travel bar. */
export function activityOf(
  entries: readonly LogEntry[],
  range: TimeRange,
  intervalMs: number,
): ActivityBucket[] {
  const intervals = byInterval(entries, intervalMs);
  return intervalStarts(range, intervalMs).map((start) => {
    const group = intervals.get(start) ?? [];
    return {
      start,
      received: group.reduce((sum, entry) => sum + entry.received, 0),
      rejected: group.reduce((sum, entry) => sum + entry.rejected, 0),
      entries: group.length,
      bySource: receivedBySource(group),
    };
  });
}

/** Each source over the entries of a window. */
export function sourceWindowsOf(entries: readonly LogEntry[]): SourceWindow[] {
  return [...groupBy(entries, (entry) => entry.source)].map(([source, group]) => ({
    source,
    received: group.reduce((sum, entry) => sum + entry.received, 0),
    rejected: group.reduce((sum, entry) => sum + entry.rejected, 0),
    entries: group.length,
    last: latest(group),
  }));
}

/** Each source over the entries of its history. */
export function sourceHistoryOf(entries: readonly LogEntry[]): SourceHistory[] {
  return [...groupBy(entries, (entry) => entry.source)].map(([source, group]) => ({
    source,
    first: earliest(group),
    last: latest(group),
    batches: new Set(group.map((entry) => entry.batchId)).size,
    entries: group.length,
    activeHours: [
      ...new Set(group.map((entry) => Math.floor(entry.time / 3_600_000) * 3_600_000)),
    ].sort((a, b) => a - b),
  }));
}
