import { describe, expect, it } from 'vitest';
import { advanced, perSecondOf, speedFor, trailOf, trailSeconds } from './live';
import { dayMs } from './window';

describe('speedFor', () => {
  it('plays a window in about half a minute at a round speed', () => {
    expect(speedFor(30 * dayMs)).toBe('day');
    expect(speedFor(365 * dayMs)).toBe('month');
    expect(speedFor(62 * 365 * dayMs)).toBe('decade');
    expect(speedFor(3 * 3_600_000)).toBe('hour');
  });
});

describe('advanced', () => {
  it('moves the playhead by the speed, and stops at the end', () => {
    expect(advanced(0, 500, 'day', 10 * dayMs)).toEqual({ head: dayMs / 2, ended: false });
    expect(advanced(9.9 * dayMs, 1000, 'day', 10 * dayMs)).toEqual({
      head: 10 * dayMs,
      ended: true,
    });
  });

  it('keeps a light lit for a few seconds of playing', () => {
    expect(trailOf('week')).toBe(perSecondOf('week') * trailSeconds);
  });
});
