/**
 * The shapes the services send, exactly as contracts/synapsis-api.yaml defines them (snake_case, times as UTC text).
 * Nothing here is computed; ./model.ts holds the shapes Observe works with after reading these.
 */

/** One document of the Elasticsearch index synapsis-ingest-log: what one batch brought from one source. */
export interface IngestLogDocument {
  /** When the API received the batch (UTC). */
  timestamp: string;
  batch_id: string;
  /** Who sent the batch, such as "feeder:kev". */
  producer: string;
  /** The source the records name, such as "src:cisa:kev"; "unknown" for a refused record without one. */
  source: string;
  received: number;
  new: number;
  changed: number;
  unchanged: number;
  duplicates: number;
  rejected: number;
  rejection_counts: { code: string; count: number }[];
  /** The first 50 refused records. */
  rejections: {
    index: number;
    id?: string | null;
    source?: string;
    code: string;
    reason: string;
  }[];
  /** Time the API took for the whole batch. */
  duration_ms: number;
}
