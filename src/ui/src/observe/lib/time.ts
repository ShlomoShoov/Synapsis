/**
 * Time arithmetic for charts and the time-travel bar: the units, and the interval a chart of a given span is cut into.
 */

export const SECOND = 1_000;
export const MINUTE = 60 * SECOND;
export const HOUR = 60 * MINUTE;
export const DAY = 24 * HOUR;

/** The intervals a time chart may use: each divides the next evenly enough to read as round numbers. */
const ladder = [
  SECOND,
  2 * SECOND,
  5 * SECOND,
  10 * SECOND,
  15 * SECOND,
  30 * SECOND,
  MINUTE,
  2 * MINUTE,
  5 * MINUTE,
  10 * MINUTE,
  15 * MINUTE,
  30 * MINUTE,
  HOUR,
  2 * HOUR,
  3 * HOUR,
  6 * HOUR,
  12 * HOUR,
  DAY,
  2 * DAY,
  7 * DAY,
];

/** The smallest round interval that cuts `spanMs` into at most `buckets` intervals. */
export function niceInterval(spanMs: number, buckets = 90): number {
  const wanted = spanMs / buckets;
  return ladder.find((interval) => interval >= wanted) ?? (ladder[ladder.length - 1] as number);
}

/** The part of an interval inside a range, in milliseconds (the first and last intervals of a chart are partial). */
export function overlapOf(start: number, intervalMs: number, from: number, to: number): number {
  return Math.max(0, Math.min(start + intervalMs, to) - Math.max(start, from));
}
