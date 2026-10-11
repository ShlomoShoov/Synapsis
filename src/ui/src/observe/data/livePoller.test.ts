import { describe, expect, it } from 'vitest';
import { LivePoller } from './livePoller';
import type { EntriesQuery, IngestLog, LogEntry } from './model';

function entry(id: string, time: number): LogEntry {
  return {
    id,
    time,
    batchId: `b-${id}`,
    producer: 'feeder:test',
    source: 'src:test:one',
    received: 1,
    new: 1,
    changed: 0,
    unchanged: 0,
    duplicates: 0,
    rejected: 0,
    rejectionCounts: [],
    rejections: [],
    durationMs: 1,
  };
}

/** A log whose visible entries the test changes between polls. */
function fakeLog(visible: LogEntry[]): IngestLog {
  return {
    kind: 'simulated',
    location: 'test',
    entries: async (query: EntriesQuery) => ({
      entries: visible
        .filter((item) => item.time >= query.range.from && item.time < query.range.to)
        .sort((a, b) => a.time - b.time),
      next: null,
    }),
  } as unknown as IngestLog;
}

describe('LivePoller', () => {
  it('delivers a slow batch that appears after a newer one, exactly once', async () => {
    const visible = [entry('fast', 10_000)];
    const poller = new LivePoller(fakeLog(visible), { since: 0, overlapMs: 60_000 });
    expect((await poller.poll()).map((item) => item.id)).toEqual(['fast']);

    // A batch received before "fast" but done after it: its timestamp is older than the newest one shown.
    visible.push(entry('slow', 4_000));
    expect((await poller.poll()).map((item) => item.id)).toEqual(['slow']);
    expect(await poller.poll()).toEqual([]);
  });

  it('does not deliver entries it was told were already shown', async () => {
    const shown = entry('old', 5_000);
    const poller = new LivePoller(fakeLog([shown, entry('new', 6_000)]), { since: 0 });
    poller.remember([shown]);
    expect((await poller.poll()).map((item) => item.id)).toEqual(['new']);
  });
});
