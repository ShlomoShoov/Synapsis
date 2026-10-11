/**
 * What the panels share: the data of the dashboards, and the arithmetic of rates and changes.
 */
import type { Catalog } from '../../data/catalog';
import type {
  LogEntry,
  MetricsSnapshot,
  PeriodTotals,
  SourceHistory,
  SourceSet,
  TimeBucket,
  TimeRange,
} from '../../data/model';
import { overlapOf } from '../../lib/time';

/** Everything a panel may read. */
export interface MetricsData {
  range: TimeRange;
  intervalMs: number;
  sources: SourceSet;
  snapshot: MetricsSnapshot | undefined;
  /** The same totals over the period before, when comparing. */
  previous: PeriodTotals | undefined;
  /** Each source over the last 30 days, for freshness. */
  history: SourceHistory[] | undefined;
  refreshing: boolean;
  catalog: Catalog;
  /** The moment "now" means for ages and freshness. */
  now: number;
  /** A key that changes whenever the period or the sources change (for panels that read more on their own). */
  scope: string;
  /** Opens the sheet of a batch. */
  openBatch: (entry: LogEntry) => void;
  /** Narrows every panel to one source. */
  focusSource: (source: string) => void;
}

/** The seconds of an interval that lie inside the period (the first and the last are often partial). */
export function secondsOf(bucket: TimeBucket, intervalMs: number, range: TimeRange): number {
  return overlapOf(bucket.start, intervalMs, range.from, range.to) / 1000;
}

/** A per-second rate of a count in each interval, or null for an interval with no time inside the period. */
export function ratePerSecond(
  buckets: readonly TimeBucket[],
  intervalMs: number,
  range: TimeRange,
  count: (bucket: TimeBucket) => number,
): (number | null)[] {
  return buckets.map((bucket) => {
    const seconds = secondsOf(bucket, intervalMs, range);
    return seconds > 0 ? count(bucket) / seconds : null;
  });
}

/** How a figure changed from the period before. */
export interface Change {
  /** The change itself, shown strong: "+12%", "−3.1%", "18×", "+0.4 pt", "new", "no change". */
  text: string;
  /** The words after it: "vs the period before", "the period before" (after a multiple). */
  rest: string;
  /** Whether it moved, and which way is good: "good", "bad", or "neutral". */
  tone: 'good' | 'bad' | 'neutral';
}

const versus = 'vs the period before';

/**
 * The change of a figure from `before` to `now`, as a relative change; from twice as much on, as a multiple ("18×"
 * reads better than "+1,682%"). `upIsBad` colours a rise red (refusals, durations) and a fall green; when neither
 * way is good, the change stays uncoloured.
 */
export function relativeChange(
  now: number,
  before: number | null | undefined,
  upIsBad?: boolean,
): Change | null {
  if (before === null || before === undefined) {
    return null;
  }
  if (before === 0) {
    return now === 0
      ? { text: 'no change', rest: versus, tone: 'neutral' }
      : { text: 'new', rest: 'none the period before', tone: 'neutral' };
  }
  const change = (now - before) / before;
  if (Math.abs(change) < 0.0005) {
    return { text: 'no change', rest: versus, tone: 'neutral' };
  }
  const tone = upIsBad === undefined ? 'neutral' : change > 0 === upIsBad ? 'bad' : 'good';
  if (change >= 1) {
    // Past a hundred times, the period before was all but empty: the exact multiple says nothing more.
    const ratio = now / before;
    const times =
      ratio >= 100 ? '>100' : ratio < 10 ? ratio.toFixed(1).replace(/\.0$/, '') : Math.round(ratio);
    return { text: `${times}×`, rest: 'the period before', tone };
  }
  const percent = Math.abs(change * 100);
  return {
    text: `${change > 0 ? '+' : '−'}${percent < 10 ? percent.toFixed(1) : Math.round(percent)}%`,
    rest: versus,
    tone,
  };
}

/** The change of a share (refusal rate), in percentage points; a rise is bad. */
export function pointChange(now: number, before: number | null | undefined): Change | null {
  if (before === null || before === undefined || !Number.isFinite(before)) {
    return null;
  }
  const points = (now - before) * 100;
  if (Math.abs(points) < 0.05) {
    return { text: 'no change', rest: versus, tone: 'neutral' };
  }
  return {
    text: `${points > 0 ? '+' : '−'}${Math.abs(points).toFixed(1)} pt`,
    rest: versus,
    tone: points > 0 ? 'bad' : 'good',
  };
}
