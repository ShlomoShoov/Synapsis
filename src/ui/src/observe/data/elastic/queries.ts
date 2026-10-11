/**
 * The Elasticsearch questions Observe asks the ingestion log, as plain request bodies. They follow the field types of
 * contracts/synapsis-api.yaml (x-elasticsearch.mapping): sums over the count fields, terms over the keyword fields,
 * a nested aggregation over rejection_counts. Pure functions, so they are tested on their own (queries.test.ts).
 */
import type { EntriesQuery, SourceSet, TimeRange } from '../model';

/** A request body. */
export type Body = Record<string, unknown>;

/** The count fields of a log document, summed by most questions. */
export const outcomeFields = [
  'received',
  'new',
  'changed',
  'unchanged',
  'duplicates',
  'rejected',
] as const;

/** One sum aggregation per count field. */
function outcomeSums(): Body {
  return Object.fromEntries(outcomeFields.map((field) => [field, { sum: { field } }]));
}

/** The filter of every question: a time range, optionally some sources, optionally more conditions. */
export function filterQuery(range: TimeRange, sources?: SourceSet, extra: Body[] = []): Body {
  const timestamp: Body = { gte: range.from, format: 'epoch_millis' };
  if (Number.isFinite(range.to)) {
    timestamp.lt = range.to;
  }
  const filter: Body[] = [{ range: { timestamp } }];
  if (sources) {
    filter.push({ terms: { source: [...sources] } });
  }
  return { bool: { filter: [...filter, ...extra] } };
}

/** A page of entries, sorted by time (then batch and source, so the order is total and pages never overlap). */
export function entriesBody(query: EntriesQuery): Body {
  const extra: Body[] = [];
  if (query.onlyRejected) {
    extra.push({ range: { rejected: { gt: 0 } } });
  }
  if (query.batchPrefix) {
    extra.push({ prefix: { batch_id: query.batchPrefix } });
  }
  const body: Body = {
    size: query.limit,
    track_total_hits: false,
    sort: [
      { timestamp: { order: query.order } },
      { batch_id: { order: query.order } },
      { source: { order: query.order } },
    ],
    query: filterQuery(query.range, query.sources, extra),
  };
  if (query.after) {
    body.search_after = [...query.after];
  }
  return body;
}

/** The date histogram of a range, in intervals of `intervalMs`, with an interval for every step even when empty. */
function histogram(range: TimeRange, intervalMs: number): Body {
  return {
    field: 'timestamp',
    fixed_interval: `${intervalMs}ms`,
    min_doc_count: 0,
    extended_bounds: { min: range.from, max: range.to - 1 },
  };
}

/** The totals of a period: counts, distinct batches, producers and sources, batch durations. */
function totalsAggs(): Body {
  return {
    ...outcomeSums(),
    batches: { cardinality: { field: 'batch_id', precision_threshold: 40000 } },
    producers: { cardinality: { field: 'producer' } },
    active_sources: { cardinality: { field: 'source' } },
    duration: { percentiles: { field: 'duration_ms', percents: [50, 95, 99] } },
    duration_stats: { stats: { field: 'duration_ms' } },
  };
}

/** Everything the dashboards show, in one question (so every number on the page agrees with the others). */
export function snapshotBody(
  range: TimeRange,
  sources: SourceSet | undefined,
  intervalMs: number,
): Body {
  return {
    size: 0,
    track_total_hits: true,
    query: filterQuery(range, sources),
    aggs: {
      ...totalsAggs(),
      sources: {
        terms: { field: 'source', size: 500 },
        aggs: {
          ...outcomeSums(),
          batches: { cardinality: { field: 'batch_id' } },
          first: { min: { field: 'timestamp' } },
          last: { max: { field: 'timestamp' } },
          duration: { avg: { field: 'duration_ms' } },
          codes: {
            nested: { path: 'rejection_counts' },
            aggs: {
              by_code: {
                terms: { field: 'rejection_counts.code', size: 50 },
                aggs: { count: { sum: { field: 'rejection_counts.count' } } },
              },
            },
          },
        },
      },
      codes: {
        nested: { path: 'rejection_counts' },
        aggs: {
          by_code: {
            terms: { field: 'rejection_counts.code', size: 200 },
            aggs: { count: { sum: { field: 'rejection_counts.count' } } },
          },
        },
      },
      timeline: {
        date_histogram: histogram(range, intervalMs),
        aggs: {
          ...outcomeSums(),
          batches: { cardinality: { field: 'batch_id' } },
          duration: { percentiles: { field: 'duration_ms', percents: [50, 95] } },
          sources: {
            terms: { field: 'source', size: 100 },
            aggs: { received: { sum: { field: 'received' } } },
          },
        },
      },
    },
  };
}

/** The totals of a period only. */
export function totalsBody(range: TimeRange, sources?: SourceSet): Body {
  return {
    size: 0,
    track_total_hits: true,
    query: filterQuery(range, sources),
    aggs: totalsAggs(),
  };
}

/** Records received and refused per interval, and per source within each, for the time-travel bar. */
export function activityBody(
  range: TimeRange,
  sources: SourceSet | undefined,
  intervalMs: number,
): Body {
  return {
    size: 0,
    query: filterQuery(range, sources),
    aggs: {
      timeline: {
        date_histogram: histogram(range, intervalMs),
        aggs: {
          received: { sum: { field: 'received' } },
          rejected: { sum: { field: 'rejected' } },
          sources: {
            terms: { field: 'source', size: 100 },
            aggs: { received: { sum: { field: 'received' } } },
          },
        },
      },
    },
  };
}

/** Each source over one window: records received and refused, and its last batch. */
export function sourceWindowsBody(range: TimeRange): Body {
  return {
    size: 0,
    query: filterQuery(range),
    aggs: {
      sources: {
        terms: { field: 'source', size: 500 },
        aggs: {
          received: { sum: { field: 'received' } },
          rejected: { sum: { field: 'rejected' } },
          last: { max: { field: 'timestamp' } },
        },
      },
    },
  };
}

/** Each source over a long history: its first and last batch, and how many batches. */
export function sourceHistoryBody(range: TimeRange): Body {
  return {
    size: 0,
    query: filterQuery(range),
    aggs: {
      sources: {
        terms: { field: 'source', size: 500 },
        aggs: {
          first: { min: { field: 'timestamp' } },
          last: { max: { field: 'timestamp' } },
          batches: { cardinality: { field: 'batch_id' } },
        },
      },
    },
  };
}
