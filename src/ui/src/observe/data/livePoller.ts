/**
 * The live poller: asks the log every few seconds for what arrived since the newest entry already shown.
 *
 * The contract suggests asking for timestamp > the newest one shown. That misses a batch that was slower than a later
 * one: the log stores the time a batch was received, but the batch appears only when it is done. So each poll looks
 * back `overlapMs` before the newest entry and drops the entries it has already delivered (by document id).
 */
import { livePollMs, liveOverlapMs } from '../config';
import type { EntriesCursor, IngestLog, LogEntry, SourceSet } from './model';

/** The settings of a poller. */
export interface LivePollerOptions {
  /** Deliver entries received from this moment on. */
  since: number;
  sources?: SourceSet;
  intervalMs?: number;
  overlapMs?: number;
  /** The largest page asked for; a full page is followed by the next one at once. */
  pageSize?: number;
}

/** Polls a log for new entries; see the file comment. */
export class LivePoller {
  private readonly log: IngestLog;
  private readonly sources: SourceSet | undefined;
  private readonly intervalMs: number;
  private readonly overlapMs: number;
  private readonly pageSize: number;
  /** The newest time delivered so far. */
  private watermark: number;
  /** The ids delivered recently, with their times, to drop them when a poll sees them again. */
  private readonly delivered = new Map<string, number>();
  private timer: number | undefined;
  private controller: AbortController | null = null;

  constructor(log: IngestLog, options: LivePollerOptions) {
    this.log = log;
    this.sources = options.sources;
    this.intervalMs = options.intervalMs ?? livePollMs;
    this.overlapMs = options.overlapMs ?? liveOverlapMs;
    this.pageSize = options.pageSize ?? 500;
    this.watermark = options.since;
  }

  /** Marks entries as already delivered (for example those loaded before the poller started). */
  remember(entries: readonly LogEntry[]): void {
    for (const entry of entries) {
      this.delivered.set(entry.id, entry.time);
      this.watermark = Math.max(this.watermark, entry.time);
    }
  }

  /** One poll: the entries not delivered before, oldest first. */
  async poll(signal?: AbortSignal): Promise<LogEntry[]> {
    const fresh: LogEntry[] = [];
    let after: EntriesCursor | null = null;
    for (let page = 0; page < 10; page++) {
      const result = await this.log.entries(
        {
          range: { from: this.watermark - this.overlapMs, to: Infinity },
          sources: this.sources,
          order: 'asc',
          limit: this.pageSize,
          after,
        },
        signal,
      );
      for (const entry of result.entries) {
        if (!this.delivered.has(entry.id)) {
          this.delivered.set(entry.id, entry.time);
          fresh.push(entry);
        }
      }
      if (!result.next) {
        break;
      }
      after = result.next;
    }
    for (const entry of fresh) {
      this.watermark = Math.max(this.watermark, entry.time);
    }
    this.forgetOld();
    return fresh;
  }

  /** Polls on a timer, handing each poll's new entries to `onEntries` and failures to `onError`. */
  start(onEntries: (entries: LogEntry[]) => void, onError: (error: unknown) => void): void {
    this.stop();
    const controller = new AbortController();
    this.controller = controller;
    const tick = async () => {
      try {
        const entries = await this.poll(controller.signal);
        if (!controller.signal.aborted && entries.length > 0) {
          onEntries(entries);
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          onError(error);
        }
      }
      if (!controller.signal.aborted) {
        this.timer = window.setTimeout(tick, this.intervalMs);
      }
    };
    void tick();
  }

  /** Stops polling and cancels a poll on its way. */
  stop(): void {
    this.controller?.abort();
    this.controller = null;
    window.clearTimeout(this.timer);
  }

  /** Drops remembered ids older than any future poll can return. */
  private forgetOld() {
    const horizon = this.watermark - 2 * this.overlapMs;
    for (const [id, time] of this.delivered) {
      if (time < horizon) {
        this.delivered.delete(id);
      }
    }
  }
}
