import { describe, expect, it } from 'vitest';
import {
  intervalStarts,
  parseEntries,
  parseSnapshot,
  parseTotals,
  type SearchResponse,
} from './parse';

const range = { from: 120_000, to: 300_000 };

describe('intervalStarts', () => {
  it('aligns intervals on whole multiples since 1970, as Elasticsearch does', () => {
    expect(intervalStarts({ from: 130_000, to: 300_000 }, 60_000)).toEqual([
      120_000, 180_000, 240_000,
    ]);
  });
});

describe('parseSnapshot', () => {
  const response: SearchResponse = {
    hits: { total: { value: 3 } },
    aggregations: {
      received: { value: 120 },
      new: { value: 100 },
      changed: { value: 5 },
      unchanged: { value: 5 },
      duplicates: { value: 0 },
      rejected: { value: 10 },
      batches: { value: 2 },
      producers: { value: 2 },
      active_sources: { value: 2 },
      duration: { values: { '50.0': 120, '95.0': 900, '99.0': 990 } },
      duration_stats: { avg: 300, max: 1000 },
      sources: {
        buckets: [
          {
            key: 'src:cisa:kev',
            doc_count: 2,
            received: { value: 100 },
            new: { value: 90 },
            changed: { value: 5 },
            unchanged: { value: 5 },
            duplicates: { value: 0 },
            rejected: { value: 0 },
            batches: { value: 2 },
            first: { value: 130_000 },
            last: { value: 250_000 },
            duration: { value: 200 },
            codes: { doc_count: 0, by_code: { buckets: [] } },
          },
          {
            key: 'src:jhu:csse-covid',
            doc_count: 1,
            received: { value: 20 },
            new: { value: 10 },
            changed: { value: 0 },
            unchanged: { value: 0 },
            duplicates: { value: 0 },
            rejected: { value: 10 },
            batches: { value: 1 },
            first: { value: 190_000 },
            last: { value: 190_000 },
            duration: { value: 500 },
            codes: { by_code: { buckets: [{ key: 'E-MEASURE-NEGATIVE', count: { value: 10 } }] } },
          },
        ],
      },
      codes: {
        by_code: { buckets: [{ key: 'E-MEASURE-NEGATIVE', doc_count: 1, count: { value: 10 } }] },
      },
      timeline: {
        buckets: [
          { key: 180_000, doc_count: 1, received: { value: 20 }, rejected: { value: 10 } },
          { key: 120_000, doc_count: 1, received: { value: 50 }, rejected: { value: 0 } },
        ],
      },
    },
  };

  it('reads totals, percentiles and per-source statistics', () => {
    const snapshot = parseSnapshot(response, range, 60_000);
    expect(snapshot.totals).toMatchObject({ received: 120, rejected: 10, entries: 3, batches: 2 });
    expect(snapshot.totals.duration).toEqual({ p50: 120, p95: 900, p99: 990, avg: 300, max: 1000 });
    expect(snapshot.sources[1]).toMatchObject({
      source: 'src:jhu:csse-covid',
      rejected: 10,
      codes: [{ code: 'E-MEASURE-NEGATIVE', count: 10 }],
    });
  });

  it('attributes each code to the sources it refused', () => {
    const snapshot = parseSnapshot(response, range, 60_000);
    expect(snapshot.codes).toEqual([
      {
        code: 'E-MEASURE-NEGATIVE',
        count: 10,
        sources: [{ source: 'src:jhu:csse-covid', count: 10 }],
      },
    ]);
  });

  it('gives one bucket per interval, in order, filling the ones Elasticsearch left out', () => {
    const snapshot = parseSnapshot(response, range, 60_000);
    expect(snapshot.buckets.map((bucket) => [bucket.start, bucket.received])).toEqual([
      [120_000, 50],
      [180_000, 20],
      [240_000, 0],
    ]);
  });

  it('answers zeros when the index does not exist yet (no aggregations at all)', () => {
    const snapshot = parseSnapshot({ hits: { total: { value: 0 }, hits: [] } }, range, 60_000);
    expect(snapshot.totals.received).toBe(0);
    expect(snapshot.totals.duration.p95).toBeNull();
    expect(snapshot.sources).toEqual([]);
    expect(snapshot.buckets).toHaveLength(3);
  });
});

describe('parseTotals', () => {
  it('reads null percentiles of an empty period as null', () => {
    const totals = parseTotals({ aggregations: { duration: { values: { '50.0': null } } } });
    expect(totals.duration.p50).toBeNull();
  });
});

describe('parseEntries', () => {
  it('reads documents and gives a cursor only when the page is full', () => {
    const hit = {
      _id: 'a1',
      _source: {
        timestamp: '2026-10-10T08:15:00.123Z',
        batch_id: 'kev-1',
        producer: 'feeder:kev',
        source: 'src:cisa:kev',
        received: 6,
        new: 5,
        changed: 0,
        unchanged: 0,
        duplicates: 0,
        rejected: 1,
        rejection_counts: [{ code: 'E-JSON', count: 1 }],
        rejections: [
          { index: 5, id: 'ent:cve:CVE-2099-0001', code: 'E-JSON', reason: 'unknown kind' },
        ],
        duration_ms: 42,
      },
      sort: [1_791_706_500_123, 'kev-1', 'src:cisa:kev'],
    };
    const full = parseEntries({ hits: { hits: [hit] } }, 1);
    expect(full.entries[0]).toMatchObject({
      id: 'a1',
      time: Date.parse('2026-10-10T08:15:00.123Z'),
      rejected: 1,
      rejections: [
        { index: 5, id: 'ent:cve:CVE-2099-0001', code: 'E-JSON', reason: 'unknown kind' },
      ],
    });
    expect(full.next).toEqual(hit.sort);
    expect(parseEntries({ hits: { hits: [hit] } }, 2).next).toBeNull();
  });
});
