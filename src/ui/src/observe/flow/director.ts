/**
 * The director of the live flow: turns log entries into what the scene and the panels around it show.
 *
 * - Each entry becomes an arc from its source to the core. A source fires at most once every `minGapSeconds`; what
 *   arrives in between is merged into its next arc, so a fast replay stays readable instead of becoming a blur.
 * - Live polls deliver entries in bursts every couple of seconds; their arcs are spread over the next interval so
 *   the flow looks continuous.
 * - It keeps the arrivals feed (the latest entries) and the rolling readouts (rates over the last minutes of scene
 *   time), and tells its subscribers when they change, a few times a second at most.
 */
import type { LogEntry } from '../data/model';
import type { FlowBatch, FlowEngine } from './engine/FlowEngine';

/** The readouts over the window of scene time before "now". */
export interface FlowStats {
  windowMs: number;
  received: number;
  written: number;
  rejected: number;
  batches: number;
  sources: number;
}

/** What subscribers read. */
export interface DirectorSnapshot {
  /** The latest entries, newest first. */
  feed: readonly LogEntry[];
  stats: FlowStats;
}

const feedLength = 40;
const minGapSeconds = 0.32;
const notifyEveryMs = 220;

function emptyStats(windowMs: number): FlowStats {
  return { windowMs, received: 0, written: 0, rejected: 0, batches: 0, sources: 0 };
}

/** The director; see the file comment. */
export class FlowDirector {
  private engine: FlowEngine | null = null;
  private feed: LogEntry[] = [];
  /** Entries inside the readouts' window, oldest first. */
  private recent: LogEntry[] = [];
  private windowMs = 5 * 60_000;
  private snapshot: DirectorSnapshot = { feed: [], stats: emptyStats(this.windowMs) };
  private readonly listeners = new Set<() => void>();
  private lastNotify = 0;
  private dirty = false;
  /** Per source: when it last fired (engine seconds), and what waits to fire. */
  private readonly lanes = new Map<string, { last: number; pending: FlowBatch | null }>();
  private focus: string | null = null;
  /** Whether the readouts are on screen: off screen, changes wait and are told when it returns. */
  private shown = true;

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  getSnapshot = (): DirectorSnapshot => this.snapshot;

  /** The engine to drive (null while there is none). */
  setEngine(engine: FlowEngine | null) {
    this.engine = engine;
    this.lanes.clear();
  }

  /** Whether the readouts are on screen. While they are not, subscribers are not told (nothing would show). */
  setShown(shown: boolean) {
    this.shown = shown;
  }

  /** Follow one source only (null for all): the others still count, but launch no arcs. */
  setFocus(source: string | null) {
    this.focus = source;
  }

  /** The window of scene time the readouts cover. */
  setWindow(windowMs: number) {
    this.windowMs = windowMs;
    this.dirty = true;
  }

  /** Forgets everything shown: after a jump in time. */
  reset() {
    this.feed = [];
    this.recent = [];
    for (const lane of this.lanes.values()) {
      lane.pending = null;
    }
    this.dirty = true;
  }

  /**
   * Shows entries, oldest first. Their arcs are spread over `spreadSeconds` (0 for at once). `launch` false only
   * records them (the feed and the readouts) without arcs, for entries from before the scene opened.
   */
  play(entries: readonly LogEntry[], spreadSeconds = 0, launch = true) {
    if (entries.length === 0) {
      return;
    }
    this.feed = [...entries].reverse().concat(this.feed).slice(0, feedLength);
    this.recent.push(...entries);
    this.dirty = true;
    if (!launch || !this.engine) {
      return;
    }
    // Merge each source's entries within this call, then hand each source's batch to its lane.
    const first = entries[0]?.time ?? 0;
    const span = Math.max(1, (entries[entries.length - 1]?.time ?? first) - first);
    entries.forEach((entry, i) => {
      if (this.focus && entry.source !== this.focus) {
        return;
      }
      const at =
        spreadSeconds > 0
          ? ((entry.time - first) / span) * spreadSeconds * 0.85 + (i % 3) * 0.04
          : 0;
      this.queue(entry, at);
    });
  }

  /** Fires what waited in the lanes, trims the readouts' window, and tells subscribers. Call every frame. */
  tick(sceneTime: number, realTime: number) {
    const engine = this.engine;
    if (engine) {
      for (const [source, lane] of this.lanes) {
        if (lane.pending && engine.time - lane.last >= minGapSeconds) {
          engine.launch(lane.pending);
          lane.last = engine.time;
          lane.pending = null;
        }
        if (!lane.pending && engine.time - lane.last > 60) {
          this.lanes.delete(source);
        }
      }
    }
    const horizon = sceneTime - this.windowMs;
    let drop = 0;
    while (drop < this.recent.length && (this.recent[drop]?.time ?? Infinity) < horizon) {
      drop++;
    }
    if (drop > 0) {
      this.recent.splice(0, drop);
      this.dirty = true;
    }
    if (this.dirty && this.shown && realTime - this.lastNotify > notifyEveryMs) {
      this.lastNotify = realTime;
      this.dirty = false;
      this.snapshot = { feed: this.feed, stats: this.stats(sceneTime) };
      for (const listener of this.listeners) {
        listener();
      }
    }
  }

  /** Hands an entry to its source's lane: fired at once if the lane is free, merged into the waiting batch if not. */
  private queue(entry: LogEntry, delay: number) {
    const engine = this.engine;
    if (!engine) {
      return;
    }
    const batch: FlowBatch = {
      source: entry.source,
      received: entry.received,
      written: entry.new + entry.changed,
      rejected: entry.rejected,
    };
    const lane = this.lanes.get(entry.source) ?? { last: -Infinity, pending: null };
    this.lanes.set(entry.source, lane);
    const fireAt = engine.time + delay;
    if (!lane.pending && fireAt - lane.last >= minGapSeconds) {
      if (engine.launch(batch, delay)) {
        lane.last = fireAt;
      }
      return;
    }
    if (lane.pending) {
      lane.pending.received += batch.received;
      lane.pending.written += batch.written;
      lane.pending.rejected += batch.rejected;
    } else {
      lane.pending = batch;
    }
  }

  private stats(sceneTime: number): FlowStats {
    const stats = emptyStats(this.windowMs);
    const batches = new Set<string>();
    const sources = new Set<string>();
    for (const entry of this.recent) {
      if (entry.time > sceneTime) {
        continue;
      }
      stats.received += entry.received;
      stats.written += entry.new + entry.changed;
      stats.rejected += entry.rejected;
      batches.add(entry.batchId);
      sources.add(entry.source);
    }
    stats.batches = batches.size;
    stats.sources = sources.size;
    return stats;
  }
}
