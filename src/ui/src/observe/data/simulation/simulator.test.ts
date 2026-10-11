import { describe, expect, it } from 'vitest';
import { historyLength, LogSimulator } from './simulator';

const epoch = Date.UTC(2026, 9, 11, 12, 0, 0);
const all = { from: epoch - historyLength, to: epoch + 3_600_000 };

describe('LogSimulator', () => {
  it('is deterministic: the same epoch and seed give the same log', () => {
    const a = new LogSimulator({ epoch }).select(all, epoch + 600_000);
    const b = new LogSimulator({ epoch }).select(all, epoch + 600_000);
    expect(a.length).toBeGreaterThan(1000);
    expect(b.map((entry) => entry.batchId)).toEqual(a.map((entry) => entry.batchId));
  });

  it('shows an entry only once its batch is done, as the real log does', () => {
    const now = epoch + 120_000;
    const entries = new LogSimulator({ epoch }).select(all, now);
    for (const entry of entries) {
      expect(entry.time + entry.durationMs).toBeLessThanOrEqual(now);
    }
  });

  it('accounts for every record: received = new + changed + unchanged + duplicates + rejected', () => {
    const entries = new LogSimulator({ epoch }).select(all, epoch);
    for (const entry of entries) {
      expect(entry.new + entry.changed + entry.unchanged + entry.duplicates + entry.rejected).toBe(
        entry.received,
      );
      const byCode = entry.rejectionCounts.reduce((sum, item) => sum + item.count, 0);
      expect(byCode).toBe(entry.rejected);
      expect(entry.rejections.length).toBe(Math.min(50, entry.rejected));
    }
  });

  it('keeps the log sorted by time and every id unique', () => {
    const entries = new LogSimulator({ epoch }).select(all, epoch + 60_000);
    for (let i = 1; i < entries.length; i++) {
      expect((entries[i] as { time: number }).time).toBeGreaterThanOrEqual(
        (entries[i - 1] as { time: number }).time,
      );
    }
    expect(new Set(entries.map((entry) => entry.id)).size).toBe(entries.length);
  });

  it('never invents traffic for feeders whose key is missing', () => {
    const entries = new LogSimulator({ epoch }).select(all, epoch);
    expect(entries.some((entry) => entry.source === 'src:nasa:firms')).toBe(false);
    expect(entries.some((entry) => entry.source === 'src:fred:series')).toBe(false);
  });
});
