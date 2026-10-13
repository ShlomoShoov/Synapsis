/**
 * The answers of the Analyze routes exactly as they arrive (contracts/synapsis-api.yaml, components.schemas): snake_case,
 * fields with a null value left out. Only adapt.ts reads these; the rest of Analyze uses the model (model.ts), so a
 * change of shape on the wire changes adapt.ts and this file only.
 */

export type WireKind = 'entity' | 'event' | 'conclusion';

export interface WireMeasure {
  name?: string;
  value?: number;
  unit?: string;
}

export interface WireEntityLabel {
  id: string;
  label: string;
}

/** RecordSummary: one record, enough to draw it and show a popup. */
export interface WireRecordSummary {
  id: string;
  kind: WireKind;
  type: string;
  sources: string[];
  known_at: string;
  label: string;
  label_from: 'name' | 'title' | 'body' | 'statement' | 'generated';
  time_start?: string;
  time_end?: string;
  time_precision?: string;
  /** [longitude, latitude] */
  point?: [number, number];
  uncertainty_km?: number;
  country?: string;
  subdivision?: string;
  area_name?: string;
  logical?: string[];
  measures?: WireMeasure[];
  participants?: WireEntityLabel[];
}

export interface WireMapCell {
  cell: string;
  resolution: number;
  count: number;
  /** [longitude, latitude] */
  center: [number, number];
  boundary: [number, number][];
}

export interface WireOmittedCount {
  reason: 'no_point' | 'too_imprecise' | string;
  count: number;
  message: string;
}

/** GET /analyze/map: only the list of the chosen layer is filled. */
export interface WireMapAnswer {
  as_of: string;
  layer: 'points' | 'cells';
  resolution?: number;
  events: number;
  total: number;
  next?: string;
  omitted?: WireOmittedCount[];
  points?: WireRecordSummary[];
  cells?: WireMapCell[];
}

export interface WireConnectedRecord extends WireRecordSummary {
  role?: string;
  direction?: 'out' | 'in';
  shared_events?: number;
}

export interface WireConnectionGroup {
  key: string;
  kind: 'relation' | 'together' | 'event' | 'participant' | string;
  type?: string;
  label: string;
  count: number;
  next?: string;
  items: WireConnectedRecord[];
}

/** GET /analyze/graph */
export interface WireGraphAnswer {
  as_of: string;
  center: WireRecordSummary;
  connections: WireConnectionGroup[];
  no_connections_reason?: string;
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
