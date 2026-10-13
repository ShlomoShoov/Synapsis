import { describe, expect, it } from 'vitest';
import { clampExtent, extentAround, narrowestExtent, panned, ticksOf, zoomed } from './ruler';
import { dayMs, timelineStart } from './window';

const at = (text: string) => Date.parse(text);
const now = at('2026-10-13T09:30:00Z');

describe('the extent', () => {
  it('shows a window with room around it', () => {
    const window = { from: at('2026-09-14T00:00:00Z'), to: at('2026-10-14T00:00:00Z') };
    const extent = extentAround(window, now);
    expect(extent.start).toBeLessThan(window.from);
    expect(extent.end).toBeGreaterThan(window.to);
    expect((window.to - window.from) / (extent.end - extent.start)).toBeGreaterThan(0.4);
  });

  it('zooms around a moment, which stays where it was', () => {
    const extent = { start: at('2020-01-01T00:00:00Z'), end: at('2022-01-01T00:00:00Z') };
    const pivot = at('2021-01-01T00:00:00Z');
    const wider = zoomed(extent, 2, pivot, now);
    expect(wider.end - wider.start).toBe(2 * (extent.end - extent.start));
    expect((pivot - wider.start) / (wider.end - wider.start)).toBeCloseTo(
      (pivot - extent.start) / (extent.end - extent.start),
    );
  });

  it('stays between 1900 and a little after now, at least two days wide', () => {
    expect(
      panned({ start: timelineStart, end: timelineStart + 10 * dayMs }, -1e12, now).start,
    ).toBe(timelineStart);
    expect(panned({ start: now - dayMs * 10, end: now }, 1e12, now).end).toBeLessThan(
      now + 60 * dayMs,
    );
    const narrow = clampExtent({ start: now - 1000, end: now }, now);
    expect(narrow.end - narrow.start).toBe(narrowestExtent);
  });
});

describe('ticksOf', () => {
  it('labels years across decades, with decades stronger', () => {
    const { major } = ticksOf(
      { start: at('1960-01-01T00:00:00Z'), end: at('2026-01-01T00:00:00Z') },
      1300,
    );
    expect(major[0]!.label).toBe('1960');
    expect(major.find((tick) => tick.label === '2000')?.strong).toBe(true);
    expect(major.length).toBeLessThan(16);
  });

  it('labels months with the year at January, and days when close', () => {
    const months = ticksOf(
      { start: at('2025-10-01T00:00:00Z'), end: at('2026-10-01T00:00:00Z') },
      1300,
    ).major;
    expect(months.map((tick) => tick.label)).toContain('2026');
    expect(months.map((tick) => tick.label)).toContain('Mar');
    const days = ticksOf(
      { start: at('2026-09-01T00:00:00Z'), end: at('2026-09-20T00:00:00Z') },
      1300,
    ).major;
    expect(days[0]!.label).toBe('1 Sep');
  });
});
