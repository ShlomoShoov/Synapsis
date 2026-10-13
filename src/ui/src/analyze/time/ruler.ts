/**
 * The timeline's ruler: which stretch of time it shows (the extent), and its ticks. Pure.
 *
 * The ruler moves independently of the window: the wheel zooms it around the pointer and dragging its empty part pans
 * it, from a few days wide to the whole timeline.
 */
import { utcDay, utcHour, utcMonth, utcYear, type CountableTimeInterval } from 'd3-time';
import { dayMs, hourMs, timelineStart, type TimeWindow } from './window';

export interface Extent {
  start: number;
  end: number;
}

/** The narrowest ruler: two days. */
export const narrowestExtent = 2 * dayMs;

/** How far past "now" the ruler may show (a little room after today). */
export function latestEdge(now: number): number {
  return now + 45 * dayMs;
}

/** The extent that shows a window in the middle, with room on both sides (the window takes about half). */
export function extentAround(window: TimeWindow, now: number): Extent {
  const length = window.to - window.from;
  const room = Math.max(length * 0.55, dayMs);
  return clampExtent({ start: window.from - room * 1.4, end: window.to + room * 0.6 }, now);
}

/** The extent zoomed by `factor` (above 1 = wider) around the moment `at`. */
export function zoomed(extent: Extent, factor: number, at: number, now: number): Extent {
  const share = (at - extent.start) / (extent.end - extent.start);
  const span = (extent.end - extent.start) * factor;
  const start = at - span * share;
  return clampExtent({ start, end: start + span }, now);
}

/** The extent moved by `delta` ms. */
export function panned(extent: Extent, delta: number, now: number): Extent {
  return clampExtent({ start: extent.start + delta, end: extent.end + delta }, now);
}

/** An extent kept between the timeline's start and a little after now, and at least two days wide. */
export function clampExtent(extent: Extent, now: number): Extent {
  const latest = latestEdge(now);
  const widest = latest - timelineStart;
  let span = Math.min(Math.max(extent.end - extent.start, narrowestExtent), widest);
  let start = extent.start;
  if (start < timelineStart) start = timelineStart;
  if (start + span > latest) start = latest - span;
  span = Math.min(span, latest - start);
  return { start, end: start + span };
}

/** A labelled tick. */
export interface Tick {
  at: number;
  label: string;
  /** A tick that starts a larger unit (a year among months), drawn stronger. */
  strong: boolean;
}

interface Scale {
  /** The approximate length of one step. */
  step: number;
  interval: CountableTimeInterval;
  every: number;
  label: (date: Date) => string;
  /** Whether a tick starts the larger unit. */
  strong: (date: Date) => boolean;
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

const year = (date: Date) => String(date.getUTCFullYear());
const monthDay = (date: Date) => `${date.getUTCDate()} ${monthNames[date.getUTCMonth()]}`;
const month = (date: Date) =>
  date.getUTCMonth() === 0 ? year(date) : (monthNames[date.getUTCMonth()] ?? '');
const startsMonth = (date: Date) => date.getUTCDate() === 1 && date.getUTCHours() === 0;
const startsYear = (date: Date) => date.getUTCMonth() === 0 && startsMonth(date);

/** The scales, finest first. */
const scales: Scale[] = [
  { step: hourMs, interval: utcHour, every: 1, label: hourLabel, strong: startsDay },
  { step: 3 * hourMs, interval: utcHour, every: 3, label: hourLabel, strong: startsDay },
  { step: 6 * hourMs, interval: utcHour, every: 6, label: hourLabel, strong: startsDay },
  { step: 12 * hourMs, interval: utcHour, every: 12, label: hourLabel, strong: startsDay },
  { step: dayMs, interval: utcDay, every: 1, label: monthDay, strong: startsMonth },
  { step: 2 * dayMs, interval: utcDay, every: 2, label: monthDay, strong: startsMonth },
  { step: 7 * dayMs, interval: utcDay, every: 7, label: monthDay, strong: startsMonth },
  { step: 30 * dayMs, interval: utcMonth, every: 1, label: month, strong: startsYear },
  { step: 91 * dayMs, interval: utcMonth, every: 3, label: month, strong: startsYear },
  { step: 182 * dayMs, interval: utcMonth, every: 6, label: month, strong: startsYear },
  {
    step: 365 * dayMs,
    interval: utcYear,
    every: 1,
    label: year,
    strong: (d) => d.getUTCFullYear() % 10 === 0,
  },
  {
    step: 2 * 365 * dayMs,
    interval: utcYear,
    every: 2,
    label: year,
    strong: (d) => d.getUTCFullYear() % 10 === 0,
  },
  {
    step: 5 * 365 * dayMs,
    interval: utcYear,
    every: 5,
    label: year,
    strong: (d) => d.getUTCFullYear() % 50 === 0,
  },
  {
    step: 10 * 365 * dayMs,
    interval: utcYear,
    every: 10,
    label: year,
    strong: (d) => d.getUTCFullYear() % 50 === 0,
  },
  {
    step: 20 * 365 * dayMs,
    interval: utcYear,
    every: 20,
    label: year,
    strong: (d) => d.getUTCFullYear() % 100 === 0,
  },
  {
    step: 50 * 365 * dayMs,
    interval: utcYear,
    every: 50,
    label: year,
    strong: (d) => d.getUTCFullYear() % 100 === 0,
  },
];

function startsDay(date: Date): boolean {
  return date.getUTCHours() === 0;
}

function hourLabel(date: Date): string {
  return startsDay(date) ? monthDay(date) : `${String(date.getUTCHours()).padStart(2, '0')}:00`;
}

/**
 * The labelled ticks for an extent drawn `width` pixels wide: the finest scale whose labels stand at least `spacing`
 * pixels apart; and the unlabelled minor ticks of the next finer scale, when they are not too dense.
 */
export function ticksOf(
  extent: Extent,
  width: number,
  spacing = 88,
): { major: Tick[]; minor: number[] } {
  const span = extent.end - extent.start;
  const perPixel = span / Math.max(width, 1);
  const index = scales.findIndex((scale) => scale.step / perPixel >= spacing);
  const scaleIndex = index === -1 ? scales.length - 1 : index;
  const scale = scales[scaleIndex]!;
  const start = new Date(extent.start);
  const end = new Date(extent.end);
  const majorDates = everyOf(scale).range(start, end);
  const major = majorDates.map((date) => ({
    at: date.getTime(),
    label: scale.label(date),
    strong: scale.strong(date),
  }));
  const finer = scales[scaleIndex - 1];
  const minor =
    finer && finer.step / perPixel >= 8
      ? everyOf(finer)
          .range(start, end)
          .map((date) => date.getTime())
          .filter((at) => !major.some((tick) => tick.at === at))
      : [];
  return { major, minor };
}

function everyOf(scale: Scale) {
  // Days and hours count from the start of their month or day, so ticks fall on the same dates at every pan.
  if (scale.interval === utcDay && scale.every > 1) {
    return utcDay.filter(
      (date) => (date.getUTCDate() - 1) % scale.every === 0 && date.getUTCDate() < 29,
    );
  }
  return scale.interval.every(scale.every) ?? scale.interval;
}
