import { describe, expect, it } from 'vitest';
import {
  dayMs,
  describeLength,
  formatWindow,
  fromDayInput,
  hourMs,
  lastMonth,
  moved,
  presetOf,
  presets,
  resized,
  timelineStart,
} from './window';

const at = (text: string) => Date.parse(text);
const now = at('2026-10-13T09:30:00Z');

describe('lastMonth', () => {
  it('runs from one month ago to the end of today (UTC)', () => {
    expect(lastMonth(now)).toEqual({
      from: at('2026-09-14T00:00:00Z'),
      to: at('2026-10-14T00:00:00Z'),
    });
  });

  it('is the first preset, and is recognised as one', () => {
    expect(presets[0]!.window(now)).toEqual(lastMonth(now));
    expect(presetOf(lastMonth(now), now)).toBe('month');
    expect(presetOf({ from: 0, to: dayMs }, now)).toBeNull();
  });
});

describe('formatWindow', () => {
  it('names both days included', () => {
    expect(formatWindow(lastMonth(now))).toEqual({ from: '14 Sep 2026', to: '13 Oct 2026' });
  });

  it('names hours when the window is not whole days', () => {
    const window = { from: at('2026-10-13T06:00:00Z'), to: at('2026-10-13T18:00:00Z') };
    expect(formatWindow(window)).toEqual({ from: '13 Oct 2026, 06:00', to: '13 Oct 2026, 18:00' });
  });
});

describe('describeLength', () => {
  it('says days, years or hours', () => {
    expect(describeLength(lastMonth(now))).toBe('30 days');
    expect(
      describeLength({ from: at('2016-10-14T00:00:00Z'), to: at('2026-10-14T00:00:00Z') }),
    ).toBe('10 years');
    expect(
      describeLength({ from: at('1965-01-01T00:00:00Z'), to: at('2026-10-14T00:00:00Z') }),
    ).toBe('61.8 years');
    expect(describeLength({ from: 0, to: 6 * hourMs })).toBe('6 hours');
  });
});

describe('moved', () => {
  const window = lastMonth(now);

  it('keeps the length and snaps to days', () => {
    const next = moved(window, -10.4 * dayMs, window.to);
    expect(next.to - next.from).toBe(window.to - window.from);
    expect(next.from).toBe(window.from - 10 * dayMs);
  });

  it('stops at today and at the start of the timeline', () => {
    expect(moved(window, 5 * dayMs, window.to)).toEqual(window);
    expect(moved(window, -1e15, window.to).from).toBe(timelineStart);
  });
});

describe('resized', () => {
  const window = lastMonth(now);

  it('moves one edge, keeping at least one hour', () => {
    expect(resized(window, 'from', window.from - 3 * dayMs, window.to).from).toBe(
      window.from - 3 * dayMs,
    );
    expect(resized(window, 'to', window.from - dayMs, window.to)).toEqual({
      from: window.from,
      to: window.from + hourMs,
    });
  });

  it('snaps to hours once the window is short', () => {
    const short = { from: at('2026-10-10T00:00:00Z'), to: at('2026-10-11T00:00:00Z') };
    expect(resized(short, 'to', at('2026-10-11T05:20:00Z'), window.to).to).toBe(
      at('2026-10-11T05:00:00Z'),
    );
  });
});

describe('fromDayInput', () => {
  it('reads a date field as the start of that day, UTC', () => {
    expect(fromDayInput('1970-01-01')).toBe(0);
    expect(fromDayInput('nonsense')).toBeNull();
  });
});
