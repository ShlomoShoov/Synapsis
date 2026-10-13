/**
 * LIVE: travelling through the time window. A playhead runs from the window's start to its end; each event lights up as
 * the playhead reaches it and fades over the trail. Pure.
 */
import { dayMs, hourMs } from './window';

/** How fast the playhead runs: so much time per second. */
export const speeds = [
  { id: 'hour', label: '1 h/s', perSecond: hourMs },
  { id: 'day', label: '1 day/s', perSecond: dayMs },
  { id: 'week', label: '1 wk/s', perSecond: 7 * dayMs },
  { id: 'month', label: '1 mo/s', perSecond: 30 * dayMs },
  { id: 'year', label: '1 yr/s', perSecond: 365 * dayMs },
  { id: 'decade', label: '10 yr/s', perSecond: 3650 * dayMs },
] as const;

export type SpeedId = (typeof speeds)[number]['id'];

/** About how long a playthrough of the window should take. */
const playthroughSeconds = 30;

/** How many seconds of playing a light stays lit (its trail, in time, is this many seconds of the speed). */
export const trailSeconds = 4;

/** The slowest speed that plays the window in about half a minute. */
export function speedFor(length: number): SpeedId {
  const wanted = length / playthroughSeconds;
  return (speeds.find((speed) => speed.perSecond >= wanted) ?? speeds[speeds.length - 1]!).id;
}

export function perSecondOf(id: SpeedId): number {
  return speeds.find((speed) => speed.id === id)!.perSecond;
}

/** The trail of a speed: how long, in time, a light stays lit. */
export function trailOf(id: SpeedId): number {
  return perSecondOf(id) * trailSeconds;
}

/** The playhead after `elapsedMs` of real time, and whether it reached the end. */
export function advanced(
  head: number,
  elapsedMs: number,
  speed: SpeedId,
  end: number,
): { head: number; ended: boolean } {
  const next = head + (elapsedMs / 1000) * perSecondOf(speed);
  return next >= end ? { head: end, ended: true } : { head: next, ended: false };
}
