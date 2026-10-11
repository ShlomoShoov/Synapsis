/**
 * The ingestion log played by the simulation (simulator.ts), behind the same interface as Elasticsearch. Each answer
 * waits a few tens of milliseconds, like a request over the network, so the views show their loading states as they
 * would against the real services.
 */
import type {
  ActivityBucket,
  EntriesCursor,
  EntriesPage,
  EntriesQuery,
  IngestLog,
  LogEntry,
  MetricsSnapshot,
  PeriodTotals,
  Ping,
  SourceHistory,
  SourceSet,
  SourceWindow,
  TimeRange,
} from '../model';
import { activityOf, snapshotOf, sourceHistoryOf, sourceWindowsOf, totalsOf } from './aggregate';
import type { LogSimulator } from './simulator';

/** Waits `ms`, or rejects with an AbortError when the signal fires first. */
export function delay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(new DOMException('The request was cancelled.', 'AbortError'));
      return;
    }
    const timer = setTimeout(resolve, ms);
    signal?.addEventListener(
      'abort',
      () => {
        clearTimeout(timer);
        reject(new DOMException('The request was cancelled.', 'AbortError'));
      },
      { once: true },
    );
  });
}

/** Compares an entry with a page cursor, in the log's sort order (time, then batch, then source). */
function compareToCursor(entry: LogEntry, cursor: EntriesCursor): number {
  const [time, batchId, source] = cursor;
  return (
    entry.time - Number(time) ||
    entry.batchId.localeCompare(String(batchId)) ||
    entry.source.localeCompare(String(source))
  );
}

/** The ingestion log of a simulation. */
export class SimulatedIngestLog implements IngestLog {
  readonly kind = 'simulated';
  readonly location = 'Simulation';
  private readonly simulator: LogSimulator;
  private readonly now: () => number;
  private readonly latencyMs: number;

  /** Answers from `simulator` as seen at `now()`, each answer after about `latencyMs`. */
  constructor(simulator: LogSimulator, now: () => number = Date.now, latencyMs = 70) {
    this.simulator = simulator;
    this.now = now;
    this.latencyMs = latencyMs;
  }

  async entries(query: EntriesQuery, signal?: AbortSignal): Promise<EntriesPage> {
    await this.wait(signal);
    let entries = this.select(query.range, query.sources);
    if (query.onlyRejected) {
      entries = entries.filter((entry) => entry.rejected > 0);
    }
    if (query.batchPrefix) {
      const prefix = query.batchPrefix;
      entries = entries.filter((entry) => entry.batchId.startsWith(prefix));
    }
    if (query.order === 'desc') {
      entries.reverse();
    }
    const after = query.after;
    if (after) {
      entries = entries.filter((entry) =>
        query.order === 'asc'
          ? compareToCursor(entry, after) > 0
          : compareToCursor(entry, after) < 0,
      );
    }
    const page = entries.slice(0, query.limit);
    const last = page[page.length - 1];
    return {
      entries: page,
      next: page.length >= query.limit && last ? [last.time, last.batchId, last.source] : null,
    };
  }

  async snapshot(
    query: { range: TimeRange; sources?: SourceSet; intervalMs: number },
    signal?: AbortSignal,
  ): Promise<MetricsSnapshot> {
    await this.wait(signal);
    return snapshotOf(this.select(query.range, query.sources), query.range, query.intervalMs);
  }

  async totals(
    query: { range: TimeRange; sources?: SourceSet },
    signal?: AbortSignal,
  ): Promise<PeriodTotals> {
    await this.wait(signal);
    return totalsOf(this.select(query.range, query.sources));
  }

  async activity(
    query: { range: TimeRange; sources?: SourceSet; intervalMs: number },
    signal?: AbortSignal,
  ): Promise<ActivityBucket[]> {
    await this.wait(signal);
    return activityOf(this.select(query.range, query.sources), query.range, query.intervalMs);
  }

  async sourceWindows(query: { range: TimeRange }, signal?: AbortSignal): Promise<SourceWindow[]> {
    await this.wait(signal);
    return sourceWindowsOf(this.select(query.range));
  }

  async sourceHistory(
    query: { to: number; lookbackMs: number },
    signal?: AbortSignal,
  ): Promise<SourceHistory[]> {
    await this.wait(signal);
    return sourceHistoryOf(this.select({ from: query.to - query.lookbackMs, to: query.to }));
  }

  async ping(signal?: AbortSignal): Promise<Ping> {
    await this.wait(signal);
    return { ok: true, latencyMs: this.latencyMs, detail: 'Simulated log' };
  }

  private select(range: TimeRange, sources?: SourceSet): LogEntry[] {
    const entries = this.simulator.select(range, this.now());
    if (!sources) {
      return entries;
    }
    const wanted = new Set(sources);
    return entries.filter((entry) => wanted.has(entry.source));
  }

  private wait(signal?: AbortSignal): Promise<void> {
    return delay(this.latencyMs * (0.6 + Math.random() * 0.8), signal);
  }
}
