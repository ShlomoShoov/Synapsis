/**
 * The replay feeder: reads the log forward from a moment of the past, a page at a time, ahead of the playhead, and
 * hands out the entries the playhead has passed. A jump in time (reset) drops what was read and starts again.
 */
import { isAbort } from '../data/http';
import type { EntriesCursor, IngestLog, LogEntry } from '../data/model';

const pageSize = 400;

/** Reads the log forward for a replay. */
export class ReplayFeeder {
  private readonly log: IngestLog;
  private buffer: LogEntry[] = [];
  /** The first entry of the buffer not handed out yet. */
  private head = 0;
  private cursor: EntriesCursor | null = null;
  private from = 0;
  private exhausted = false;
  private request: AbortController | null = null;
  /** Called when a page fails (other than a cancel). */
  private onError: ((error: unknown) => void) | null = null;

  constructor(log: IngestLog) {
    this.log = log;
  }

  /** Starts again from `time`; `onError` hears of pages that fail. */
  reset(time: number, onError?: (error: unknown) => void) {
    this.onError = onError ?? null;
    this.request?.abort();
    this.request = null;
    this.buffer = [];
    this.head = 0;
    this.cursor = null;
    this.from = time;
    this.exhausted = false;
  }

  /** The entries received up to `sceneTime` not handed out yet, oldest first; reads ahead `lookaheadMs` of log. */
  due(sceneTime: number, lookaheadMs: number): LogEntry[] {
    const start = this.head;
    while (
      this.head < this.buffer.length &&
      (this.buffer[this.head]?.time ?? Infinity) <= sceneTime
    ) {
      this.head++;
    }
    const out = this.buffer.slice(start, this.head);
    if (this.head > 2_000) {
      this.buffer = this.buffer.slice(this.head);
      this.head = 0;
    }
    const last = this.buffer[this.buffer.length - 1];
    const lowWater =
      this.buffer.length - this.head < 50 || (last?.time ?? -Infinity) < sceneTime + lookaheadMs;
    if (!this.exhausted && !this.request && lowWater) {
      void this.readPage();
    }
    return out;
  }

  /** Stops reading. */
  dispose() {
    this.request?.abort();
    this.request = null;
  }

  private async readPage() {
    const controller = new AbortController();
    this.request = controller;
    try {
      const page = await this.log.entries(
        {
          range: { from: this.from, to: Date.now() },
          order: 'asc',
          limit: pageSize,
          after: this.cursor,
        },
        controller.signal,
      );
      if (controller.signal.aborted) {
        return;
      }
      this.buffer.push(...page.entries);
      this.cursor = page.next;
      this.exhausted = page.next === null;
    } catch (error) {
      if (!isAbort(error)) {
        this.exhausted = true;
        this.onError?.(error);
      }
    } finally {
      if (this.request === controller) {
        this.request = null;
      }
    }
  }
}
