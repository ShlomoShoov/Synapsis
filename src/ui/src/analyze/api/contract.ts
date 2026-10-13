/**
 * The answers of the read API exactly as they arrive (EMERGENCY.md §12): the stores' own objects, passed through.
 * Elasticsearch documents (`_source`, snake_case, a field the record does not have is absent), Elasticsearch buckets,
 * Neo4j nodes and links. Only adapt.ts reads the map's answers; the rest of Analyze uses the model (model.ts).
 */

export type WireKind = 'entity' | 'event' | 'conclusion';

/** One search document as Elasticsearch stores it: one state of one record. */
export interface WireDoc {
  id: string;
  record_key: string;
  state: number;
  kind: WireKind;
  type: string;
  source: string;
  known_at: string;
  name?: string;
  title?: string;
  body?: string;
  time_start?: string;
  time_precision?: string;
  week?: string;
  /** [longitude, latitude] */
  location?: [number, number];
  h3_r2?: string;
  h3_r3?: string;
  h3_r4?: string;
  h3_r5?: string;
  country?: string;
  subdivision?: string;
  /** Every prefix of every logical path: "cyber.vendor_product", "cyber.vendor_product/Apache", ... */
  logical_prefixes?: string[];
  /** Each measure's values, by name: { "magnitude": [5.6] }. */
  measures?: Record<string, number[]>;
  /** The ids of the entities that take part. */
  entities?: string[];
}

/** GET /map/points and GET /search: one document per record; total = distinct records that match (all of them). */
export interface WireList {
  total: number;
  items: WireDoc[];
}

/** One Elasticsearch bucket: a key, its documents (states), its distinct records. */
export interface WireBucket {
  key: string;
  doc_count: number;
  records: { value: number };
}

/** GET /map/cells: the cells (key = H3 cell id), the most records first. */
export interface WireCells {
  /** Distinct records in all the cells. */
  records: number;
  /** Documents in cells beyond the ones returned (0 = nothing cut). */
  sum_other_doc_count: number;
  buckets: WireBucket[];
}

/** GET /logical: one level of the tree (key = the full path). */
export interface WireLogical {
  sum_other_doc_count: number;
  buckets: WireBucket[];
}

/** A Neo4j node: its properties as stored, plus its labels (Entity, Event or Conclusion). */
export interface WireGraphNode {
  id: string;
  labels: string[];
  [property: string]: unknown;
}

/** A Neo4j link (PARTICIPATES, RELATED, ABOUT, PREMISE) and the node at its other end. */
export interface WireGraphLink {
  type: string;
  from: string;
  to: string;
  properties: Record<string, unknown>;
  node: WireGraphNode;
}

/** GET /graph: total = all the node's links; links holds at most the limit. */
export interface WireGraph {
  center: WireGraphNode;
  total: number;
  links: WireGraphLink[];
}

/** GET /records/{id}: one per source. `record` is the record exactly as it arrived (SIL 1.2). */
export interface WireRecordAnswer {
  source: string;
  state: number;
  known_at: string;
  received_at: string;
  batch_id: string;
  producer: string;
  record: Record<string, unknown>;
}
