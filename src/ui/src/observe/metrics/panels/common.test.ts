import { describe, expect, it } from 'vitest';
import type { TimeBucket } from '../../data/model';
import { pointChange, ratePerSecond, relativeChange } from './common';

function bucket(start: number, received: number): TimeBucket {
  return {
    start,
    received,
    new: 0,
    changed: 0,
    unchanged: 0,
    duplicates: 0,
    rejected: 0,
    entries: 1,
    batches: 1,
    durationP50: null,
    durationP95: null,
    bySource: {},
  };
}

describe('panel arithmetic', () => {
  it('measures rates over the part of each interval inside the period', () => {
    // Intervals of 10 s; the period starts 5 s into the first and ends 2 s into the third.
    const rates = ratePerSecond(
      [bucket(0, 50), bucket(10_000, 100), bucket(20_000, 20)],
      10_000,
      { from: 5_000, to: 22_000 },
      (b) => b.received,
    );
    expect(rates).toEqual([10, 10, 10]);
  });

  it('describes changes, coloured only when a direction is good or bad', () => {
    expect(relativeChange(112, 100)).toMatchObject({ text: '+12%', tone: 'neutral' });
    expect(relativeChange(90, 100, true)).toMatchObject({ text: '−10%', tone: 'good' });
    expect(relativeChange(5, 0)).toMatchObject({ text: 'new', tone: 'neutral' });
    expect(relativeChange(5, undefined)).toBeNull();
    expect(pointChange(0.015, 0.01)).toMatchObject({ text: '+0.5 pt', tone: 'bad' });
  });

  it('writes a change of twice as much or more as a multiple', () => {
    expect(relativeChange(200, 100)).toEqual({
      text: '2×',
      rest: 'the period before',
      tone: 'neutral',
    });
    expect(relativeChange(463, 100, true)).toMatchObject({ text: '4.6×', tone: 'bad' });
    expect(relativeChange(1782, 100)).toMatchObject({ text: '18×' });
    expect(relativeChange(449_800, 100)).toMatchObject({ text: '>100×' });
    expect(relativeChange(199, 100)).toMatchObject({ text: '+99%', rest: 'vs the period before' });
  });
});
