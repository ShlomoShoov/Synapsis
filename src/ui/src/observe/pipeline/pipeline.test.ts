import { describe, expect, it } from 'vitest';
import { catchUpMs, type IngestAnswer, pipelineOf, type ServicesAnswer } from './pipeline';

const v = (value: number | null) => ({ value });
const counts = (received: number, stored: number, rejected: number) => ({
  received: v(received),
  new: v(stored),
  changed: v(0),
  rejected: v(rejected),
});

const ingest: IngestAnswer = {
  aggregations: {
    day: {
      ...counts(9000, 8000, 3),
      last: v(Date.parse('2026-10-14T00:00:00Z')),
      recent: { received: v(3000) },
    },
    sources: {
      buckets: [
        {
          key: 'src:ucdp:ged',
          doc_count: 12,
          ...counts(5000, 4990, 3),
          last: v(Date.parse('2026-10-13T21:00:00Z')),
          producers: { buckets: [{ key: 'feeder:ucdp', doc_count: 12 }] },
        },
      ],
    },
  },
};

const heartbeat = (at: string, processed: number, lag?: number) => ({
  _source: { at, processed, lag, event: 'heartbeat' },
});

const services: ServicesAnswer = {
  aggregations: {
    services: {
      buckets: [
        {
          key: 'indexer',
          doc_count: 300,
          latest: {
            hits: {
              hits: [
                heartbeat('2026-10-14T00:00:30Z', 1650, 15_000_000),
                heartbeat('2026-10-14T00:00:00Z', 1700, 15_001_000),
              ],
            },
          },
          day: { processed: v(1_000_000), written: v(999_000), skipped: v(1000), failed: v(0) },
        },
        {
          key: 'engine',
          doc_count: 20,
          latest: { hits: { hits: [] } },
          day: { processed: v(41_258), written: v(41_258), skipped: v(0), failed: v(0) },
        },
      ],
    },
    runs: {
      latest: {
        hits: {
          hits: [
            {
              _source: {
                at: '2026-10-13T22:18:56Z',
                source: 'src:wikipedia:polls',
                written: 1185,
                message: 'backfill: 1,185 conclusions sent',
              },
            },
          ],
        },
      },
    },
  },
};

describe('pipelineOf', () => {
  const pipeline = pipelineOf(ingest, services);
  const stage = (id: string) => pipeline.stages.find((each) => each.id === id)!;

  it('lists the stages in the order a record goes through them', () => {
    expect(pipeline.stages.map((each) => each.id)).toEqual([
      'api',
      'relay',
      'indexer',
      'projector',
      'engine',
    ]);
  });

  it("gives the API's last 24 hours and its rate over the last 5 minutes", () => {
    expect(stage('api')).toMatchObject({ handled: 9000, failed: 3, rate: 10 });
  });

  it("measures a service's rate from its two last documents and keeps its lag", () => {
    expect(stage('indexer')).toMatchObject({
      rate: 55,
      lag: 15_000_000,
      handled: 1_000_000,
      skipped: 1000,
    });
    expect(catchUpMs(stage('indexer'))).toBeCloseTo((15_000_000 / 55) * 1000);
  });

  it('shows a service that never wrote as unknown', () => {
    expect(stage('relay')).toMatchObject({ rate: null, lastAt: null, handled: 0, lag: null });
    expect(catchUpMs(stage('relay'))).toBeNull();
  });

  it("gives the engine's conclusions and its last run", () => {
    expect(stage('engine')).toMatchObject({
      handled: 41_258,
      message: 'backfill: 1,185 conclusions sent',
    });
    expect(pipeline.runs[0]).toMatchObject({ source: 'src:wikipedia:polls', written: 1185 });
  });

  it('gives each source what it sent', () => {
    expect(pipeline.sources[0]).toMatchObject({
      source: 'src:ucdp:ged',
      producers: ['feeder:ucdp'],
      batches: 12,
      received: 5000,
      stored: 4990,
      refused: 3,
    });
  });

  it('reads a log not created yet as empty', () => {
    const empty = pipelineOf({}, {});
    expect(empty.sources).toEqual([]);
    expect(empty.runs).toEqual([]);
    expect(empty.stages.every((each) => each.handled === 0)).toBe(true);
  });

  it('sums several indexers: their rate over two heartbeats and their shares of the queue', () => {
    const indexer = services.aggregations!.services.buckets[0]!;
    const shared: ServicesAnswer = {
      aggregations: {
        ...services.aggregations!,
        services: {
          buckets: [
            {
              ...indexer,
              rated: { doc_count: 12, processed: v(16_200) },
              beat: { doc_count: 6, lag: v(18_000_000) },
            },
          ],
        },
      },
    };
    const stage = pipelineOf(ingest, shared).stages.find((s) => s.id === 'indexer')!;
    expect(stage.instances).toBe(6);
    expect(stage.rate).toBe(270);
    expect(stage.lag).toBe(18_000_000);
    expect(catchUpMs(stage)).toBe((18_000_000 / 270) * 1000);
  });
});
