/**
 * How Observe writes numbers, durations and times. One place, so the whole screen speaks the same way:
 * "1,284" for a count, "12.9K" where space is short, "640 ms" and "2 min 4 s" for durations, "4 min ago" for ages.
 */
import { timeFormat, utcFormat } from 'd3-time-format';

const SECOND = 1_000;
const MINUTE = 60 * SECOND;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

const integer = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const compact = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 });

/** A count in full: 1,284,519. */
export function formatCount(value: number): string {
  return integer.format(value);
}

/** A count in few characters: 1.3M, 12.9K, 840. */
export function formatCompact(value: number): string {
  return Math.abs(value) < 1_000 ? integer.format(value) : compact.format(value);
}

/** A share as a percentage: 0.4%, 12%, 100%. Very small but present shares read "<0.1%". */
export function formatPercent(share: number, digits?: number): string {
  if (!Number.isFinite(share)) {
    return '—';
  }
  const percent = share * 100;
  if (percent > 0 && percent < 0.1) {
    return '<0.1%';
  }
  const places = digits ?? (percent < 10 ? 1 : 0);
  return `${percent.toFixed(places).replace(/\.0$/, '')}%`;
}

/** A rate per second: 182/s, 4.2/s, 0.03/s. */
export function formatRate(perSecond: number): string {
  if (perSecond === 0) {
    return '0/s';
  }
  if (perSecond >= 100) {
    return `${formatCompact(Math.round(perSecond))}/s`;
  }
  if (perSecond >= 1) {
    return `${perSecond.toFixed(1).replace(/\.0$/, '')}/s`;
  }
  return `${perSecond.toPrecision(2)}/s`;
}

/** A duration: 640 ms, 1.2 s, 2 min 4 s, 3 h 12 min, 4 d 2 h. */
export function formatDuration(ms: number | null | undefined): string {
  if (ms === null || ms === undefined || !Number.isFinite(ms)) {
    return '—';
  }
  if (ms < SECOND) {
    return `${Math.round(ms)} ms`;
  }
  if (ms < 10 * SECOND) {
    return `${(ms / SECOND).toFixed(1).replace(/\.0$/, '')} s`;
  }
  if (ms < MINUTE) {
    return `${Math.round(ms / SECOND)} s`;
  }
  if (ms < HOUR) {
    const minutes = Math.floor(ms / MINUTE);
    const seconds = Math.round((ms % MINUTE) / SECOND);
    return seconds ? `${minutes} min ${seconds} s` : `${minutes} min`;
  }
  if (ms < DAY) {
    const hours = Math.floor(ms / HOUR);
    const minutes = Math.round((ms % HOUR) / MINUTE);
    return minutes ? `${hours} h ${minutes} min` : `${hours} h`;
  }
  const days = Math.floor(ms / DAY);
  const hours = Math.round((ms % DAY) / HOUR);
  return hours ? `${days} d ${hours} h` : `${days} d`;
}

/** A span, without its fine part: "15 min", "6 h", "7 d" (for ranges and intervals). */
export function formatSpan(ms: number): string {
  if (ms < MINUTE) {
    return `${Math.round(ms / SECOND)} s`;
  }
  if (ms < HOUR) {
    return `${Math.round(ms / MINUTE)} min`;
  }
  if (ms < DAY) {
    return `${Math.round(ms / HOUR)} h`;
  }
  return `${Math.round(ms / DAY)} d`;
}

/** A span in words, for sentences: "15 minutes", "an hour", "24 hours", "7 days" (whole units only). */
export function formatSpanWords(ms: number): string {
  const words = (count: number, unit: string, one: string) =>
    count === 1 ? one : `${count} ${unit}s`;
  if (ms < HOUR) {
    return words(Math.max(1, Math.round(ms / MINUTE)), 'minute', 'a minute');
  }
  if (ms <= 2 * DAY && Math.abs(ms / HOUR - Math.round(ms / HOUR)) < 0.01) {
    return words(Math.round(ms / HOUR), 'hour', 'an hour');
  }
  return words(Math.max(1, Math.round(ms / DAY)), 'day', 'a day');
}

/** How long ago a moment was, from `now`: "just now", "40 s ago", "4 min ago", "3 h ago", "12 d ago". */
export function formatAge(time: number, now: number): string {
  const age = now - time;
  if (age < 5 * SECOND) {
    return 'just now';
  }
  if (age < MINUTE) {
    return `${Math.round(age / SECOND)} s ago`;
  }
  if (age < HOUR) {
    return `${Math.round(age / MINUTE)} min ago`;
  }
  if (age < 2 * DAY) {
    return `${Math.round(age / HOUR)} h ago`;
  }
  return `${Math.round(age / DAY)} d ago`;
}

/** Time formats, in the viewer's zone or in UTC. */
export interface TimeFormats {
  /** Whether times are written in UTC. */
  utc: boolean;
  /** 14:32:05 */
  clock: (time: number) => string;
  /** 14:32 */
  minute: (time: number) => string;
  /** 11 Oct 14:32:05 */
  dateTime: (time: number) => string;
  /** 11 Oct 14:32 */
  dateMinute: (time: number) => string;
  /** 11 Oct */
  day: (time: number) => string;
  /** Sat 11 Oct 2026 */
  longDay: (time: number) => string;
  /** The zone's name, for labels: "UTC" or "local". */
  zone: string;
}

/** The time formats of a zone. */
export function timeFormats(utc: boolean): TimeFormats {
  const make = utc ? utcFormat : timeFormat;
  const clock = make('%H:%M:%S');
  const minute = make('%H:%M');
  const dateTime = make('%-d %b %H:%M:%S');
  const dateMinute = make('%-d %b %H:%M');
  const day = make('%-d %b');
  const longDay = make('%a %-d %b %Y');
  return {
    utc,
    clock: (time) => clock(new Date(time)),
    minute: (time) => minute(new Date(time)),
    dateTime: (time) => dateTime(new Date(time)),
    dateMinute: (time) => dateMinute(new Date(time)),
    day: (time) => day(new Date(time)),
    longDay: (time) => longDay(new Date(time)),
    zone: utc ? 'UTC' : 'local',
  };
}
