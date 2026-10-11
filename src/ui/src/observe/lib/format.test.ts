import { describe, expect, it } from 'vitest';
import {
  formatAge,
  formatCompact,
  formatCount,
  formatDuration,
  formatPercent,
  formatRate,
  formatSpanWords,
  timeFormats,
} from './format';

describe('format', () => {
  it('writes counts in full and in short', () => {
    expect(formatCount(1284519)).toBe('1,284,519');
    expect(formatCompact(840)).toBe('840');
    expect(formatCompact(12_940)).toBe('12.9K');
    expect(formatCompact(4_200_000)).toBe('4.2M');
  });

  it('writes shares, keeping tiny ones visible', () => {
    expect(formatPercent(0.004)).toBe('0.4%');
    expect(formatPercent(0.25)).toBe('25%');
    expect(formatPercent(0.0002)).toBe('<0.1%');
    expect(formatPercent(0)).toBe('0%');
  });

  it('writes rates and durations', () => {
    expect(formatRate(182.4)).toBe('182/s');
    expect(formatRate(4.25)).toBe('4.3/s');
    expect(formatDuration(640)).toBe('640 ms');
    expect(formatDuration(1_200)).toBe('1.2 s');
    expect(formatDuration(124_000)).toBe('2 min 4 s');
    expect(formatDuration(null)).toBe('—');
  });

  it('writes ages', () => {
    expect(formatAge(1_000, 3_000)).toBe('just now');
    expect(formatAge(0, 240_000)).toBe('4 min ago');
    expect(formatAge(0, 3 * 86_400_000)).toBe('3 d ago');
  });

  it('writes times in UTC when asked', () => {
    const formats = timeFormats(true);
    expect(formats.clock(Date.UTC(2026, 9, 11, 4, 5, 6))).toBe('04:05:06');
    expect(formats.dateMinute(Date.UTC(2026, 9, 11, 4, 5, 6))).toBe('11 Oct 04:05');
  });
  it('writes spans in words for sentences', () => {
    expect(formatSpanWords(15 * 60_000)).toBe('15 minutes');
    expect(formatSpanWords(3_600_000)).toBe('an hour');
    expect(formatSpanWords(24 * 3_600_000)).toBe('24 hours');
    expect(formatSpanWords(7 * 86_400_000)).toBe('7 days');
    expect(formatSpanWords(36 * 3_600_000)).toBe('36 hours');
  });
});
