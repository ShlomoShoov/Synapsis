import { describe, expect, it } from 'vitest';
import type { SourceHistory } from '../data/model';
import { freshnessOf, runStarts, typicalCadence } from './freshness';

const HOUR = 3_600_000;
const DAY = 24 * HOUR;

function history(hours: number[], last: number): SourceHistory {
  return {
    source: 'src:test:one',
    first: hours[0] ?? 0,
    last,
    batches: hours.length,
    entries: hours.length,
    activeHours: hours,
  };
}

describe('freshness', () => {
  it('treats neighbouring hours as one run', () => {
    expect(runStarts([0, HOUR, 2 * HOUR, 10 * HOUR, 11 * HOUR, 30 * HOUR])).toEqual([
      0,
      10 * HOUR,
      30 * HOUR,
    ]);
  });

  it('finds a daily cadence even when each run spans two hours', () => {
    const hours = [0, 1, 2, 3, 4].flatMap((day) => [day * DAY, day * DAY + HOUR]);
    expect(typicalCadence(hours)).toBe(DAY);
  });

  it('says on schedule, late or silent from the time since the last batch', () => {
    const hours = [0, DAY, 2 * DAY, 3 * DAY];
    const last = 3 * DAY + 5 * 60_000;
    expect(freshnessOf(history(hours, last), last + 20 * HOUR).label).toBe('On schedule');
    expect(freshnessOf(history(hours, last), last + 2.5 * DAY).label).toBe('Late');
    expect(freshnessOf(history(hours, last), last + 5 * DAY).label).toBe('Silent');
  });

  it('does not judge a feeder that ran once, or never', () => {
    expect(freshnessOf(history([0], 1000), 10 * DAY)).toMatchObject({
      label: 'Ran once',
      status: undefined,
    });
    expect(freshnessOf(undefined, 10 * DAY)).toMatchObject({ label: 'No data', status: undefined });
  });
});
