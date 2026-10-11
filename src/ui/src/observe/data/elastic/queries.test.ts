import { describe, expect, it } from 'vitest';
import { entriesBody, filterQuery, snapshotBody } from './queries';

describe('filterQuery', () => {
  it('filters on the time range, in epoch milliseconds', () => {
    expect(filterQuery({ from: 1000, to: 2000 })).toEqual({
      bool: { filter: [{ range: { timestamp: { gte: 1000, lt: 2000, format: 'epoch_millis' } } }] },
    });
  });

  it('leaves the end open for an endless range (live polls)', () => {
    const query = filterQuery({ from: 1000, to: Infinity }) as {
      bool: { filter: { range: { timestamp: Record<string, unknown> } }[] };
    };
    expect(query.bool.filter[0]?.range.timestamp).toEqual({ gte: 1000, format: 'epoch_millis' });
  });

  it('adds a terms filter for chosen sources, and none for every source', () => {
    const chosen = filterQuery({ from: 0, to: 1 }, ['src:cisa:kev']) as {
      bool: { filter: unknown[] };
    };
    expect(chosen.bool.filter[1]).toEqual({ terms: { source: ['src:cisa:kev'] } });
    const every = filterQuery({ from: 0, to: 1 }, null) as { bool: { filter: unknown[] } };
    expect(every.bool.filter).toHaveLength(1);
  });
});

describe('entriesBody', () => {
  it('sorts on time, batch and source, and continues after a cursor', () => {
    const body = entriesBody({
      range: { from: 0, to: 10 },
      order: 'desc',
      limit: 50,
      after: [5, 'kev-abc', 'src:cisa:kev'],
      onlyRejected: true,
      batchPrefix: 'kev-',
    });
    expect(body.size).toBe(50);
    expect(body.sort).toEqual([
      { timestamp: { order: 'desc' } },
      { batch_id: { order: 'desc' } },
      { source: { order: 'desc' } },
    ]);
    expect(body.search_after).toEqual([5, 'kev-abc', 'src:cisa:kev']);
    const filter = (body.query as { bool: { filter: unknown[] } }).bool.filter;
    expect(filter).toContainEqual({ range: { rejected: { gt: 0 } } });
    expect(filter).toContainEqual({ prefix: { batch_id: 'kev-' } });
  });
});

describe('snapshotBody', () => {
  it('asks for totals, sources, codes and a full date histogram in one question', () => {
    const body = snapshotBody({ from: 0, to: 3_600_000 }, null, 60_000);
    const aggs = body.aggs as Record<string, Record<string, unknown>>;
    expect(Object.keys(aggs)).toEqual(
      expect.arrayContaining([
        'received',
        'rejected',
        'batches',
        'duration',
        'sources',
        'codes',
        'timeline',
      ]),
    );
    expect(aggs.timeline?.date_histogram).toEqual({
      field: 'timestamp',
      fixed_interval: '60000ms',
      min_doc_count: 0,
      extended_bounds: { min: 0, max: 3_599_999 },
    });
    expect(aggs.codes?.nested).toEqual({ path: 'rejection_counts' });
  });
});
