/**
 * The time window of "what happened": the events that start in [from, to). Pure; times are ms since 1970, UTC, and the
 * window's edges fall on whole days (whole hours when it is shorter than a few days).
 */
import { utcDay, utcHour, utcMonth, utcYear } from 'd3-time';

export interface TimeWindow {
  from: number;
  to: number;
}

export const hourMs = 3_600_000;
export const dayMs = 86_400_000;

/** The earliest moment the timeline reaches. */
export const timelineStart = Date.UTC(1900, 0, 1);

/** The shortest window: one hour. */
export const shortestWindow = hourMs;

/** The first window: the last month, from one month ago up to the end of today (UTC). */
export function lastMonth(now: number): TimeWindow {
  const to = utcDay.offset(utcDay.floor(new Date(now)), 1).getTime();
  return { from: utcMonth.offset(new Date(to), -1).getTime(), to };
}

/** The windows offered in one click. */
export const presets: { id: string; label: string; window: (now: number) => TimeWindow }[] = [
  { id: 'month', label: 'Last month', window: lastMonth },
  {
    id: 'year',
    label: 'Last year',
    window: (now) => {
      const { to } = lastMonth(now);
      return { from: utcYear.offset(new Date(to), -1).getTime(), to };
    },
  },
  {
    id: 'decade',
    label: '10 years',
    window: (now) => {
      const { to } = lastMonth(now);
      return { from: utcYear.offset(new Date(to), -10).getTime(), to };
    },
  },
  {
    id: 'all',
    label: 'All time',
    window: (now) => ({ from: timelineStart, to: lastMonth(now).to }),
  },
];

/** The preset a window is exactly, if any. */
export function presetOf(window: TimeWindow, now: number): string | null {
  return (
    presets.find((preset) => {
      const candidate = preset.window(now);
      return candidate.from === window.from && candidate.to === window.to;
    })?.id ?? null
  );
}

/** The unit edges snap to for a window of this length: hours below four days, days otherwise. */
export function snapUnit(length: number): number {
  return length < 4 * dayMs ? hourMs : dayMs;
}

/** A moment rounded to the nearest whole unit (UTC). */
export function snap(ms: number, unit: number): number {
  const interval = unit === hourMs ? utcHour : utcDay;
  return interval.round(new Date(ms)).getTime();
}

/** The window moved by `delta`, keeping its length, kept inside [timelineStart, latest]. */
export function moved(window: TimeWindow, delta: number, latest: number): TimeWindow {
  const length = window.to - window.from;
  const unit = snapUnit(length);
  let from = snap(window.from + delta, unit);
  from = Math.min(Math.max(from, timelineStart), latest - length);
  return { from, to: from + length };
}

/** The window with one edge moved to `at`, kept at least one unit long and inside [timelineStart, latest]. */
export function resized(
  window: TimeWindow,
  edge: 'from' | 'to',
  at: number,
  latest: number,
): TimeWindow {
  const unit = snapUnit(edge === 'from' ? window.to - at : at - window.from);
  const value = snap(at, unit);
  if (edge === 'from') {
    return { from: Math.max(timelineStart, Math.min(value, window.to - unit)), to: window.to };
  }
  return { from: window.from, to: Math.min(latest, Math.max(value, window.from + unit)) };
}

const monthNames = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

/** "13 Oct 2026" (UTC); with the hour when it is not midnight: "13 Oct 2026, 14:00". */
export function formatMoment(ms: number): string {
  const date = new Date(ms);
  const day = `${date.getUTCDate()} ${monthNames[date.getUTCMonth()]} ${date.getUTCFullYear()}`;
  const hours = date.getUTCHours();
  return hours === 0 ? day : `${day}, ${String(hours).padStart(2, '0')}:00`;
}

/**
 * The window as people say it: both ends included ("14 Sep 2026 – 13 Oct 2026"). A window of whole days ends on the
 * day before `to`; a window of hours ends at `to`.
 */
export function formatWindow(window: TimeWindow): { from: string; to: string } {
  const wholeDays = window.from % dayMs === 0 && window.to % dayMs === 0;
  return {
    from: formatMoment(window.from),
    to: formatMoment(wholeDays ? window.to - dayMs : window.to),
  };
}

/** The window's length in words: "31 days", "1 year", "10 years", "6 hours". */
export function describeLength(window: TimeWindow): string {
  const length = window.to - window.from;
  const plural = (count: number, unit: string) => `${count} ${unit}${count === 1 ? '' : 's'}`;
  if (length < dayMs) return plural(Math.round(length / hourMs), 'hour');
  const days = Math.round(length / dayMs);
  const years = utcYear.count(new Date(window.from), new Date(window.to));
  if (years >= 1 && utcYear.offset(new Date(window.from), years).getTime() === window.to) {
    return plural(years, 'year');
  }
  if (days >= 730) return `${(days / 365.25).toFixed(1).replace(/\.0$/, '')} years`;
  return plural(days, 'day');
}

/** A date input's value ("2026-10-13") for a moment, and back (the start of that day, UTC). */
export function dayInputValue(ms: number): string {
  return new Date(ms).toISOString().slice(0, 10);
}

export function fromDayInput(value: string): number | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const ms = Date.parse(`${value}T00:00:00Z`);
  return Number.isNaN(ms) ? null : ms;
}
