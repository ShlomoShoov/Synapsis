/**
 * The questions Analyze asks the read API (EMERGENCY.md §12), as query strings.
 *
 * - The shared filters are from, to (on the event's start, `to` excluded), source and type (repeated: any value
 *   matches).
 * - Ids travel as query values (id=, entity=), encoded by URLSearchParams, because some contain "/". GET /records/{id}
 *   is the exception: the id is the path, "/" included.
 * - Times are sent as UTC date-times ("2026-10-13T00:00:00Z").
 */
import type { RecordKind } from './model';

/** The filters every Elasticsearch route takes. Each is optional; an empty list means no filter. */
export interface Filters {
  /** Events that start at or after this moment (ms since 1970, UTC). */
  from?: number;
  /** Events that start before this moment (ms). */
  to?: number;
  types?: string[];
  sources?: string[];
}

/** A box on the map, in degrees. A west edge east of the east edge crosses the 180th meridian. */
export interface BBox {
  west: number;
  south: number;
  east: number;
  north: number;
}

/** What the map looks at: its zoom (it picks the H3 resolution) and the box in view (none = the whole Earth). */
export interface MapView {
  zoom: number;
  bbox?: BBox;
}

/** The whole Earth, for /map/points when the map asks about everything. */
export const wholeEarth: BBox = { west: -180, south: -90, east: 180, north: 90 };

/** The H3 resolution for a zoom: 0-3 → 2 (edge about 158 km), 4-5 → 3 (60 km), 6-7 → 4 (23 km), 8 and more → 5 (8.5 km). */
export function resolutionOf(zoom: number): number {
  if (zoom < 4) return 2;
  if (zoom < 6) return 3;
  if (zoom < 8) return 4;
  return 5;
}

/** A moment as the API reads it: UTC, whole seconds. */
export function isoOf(ms: number): string {
  return new Date(Math.floor(ms / 1000) * 1000).toISOString().replace('.000Z', 'Z');
}

/** The shared filters as query parameters, in a stable order. */
export function filterParams(filters: Filters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.from !== undefined) params.set('from', isoOf(filters.from));
  if (filters.to !== undefined) params.set('to', isoOf(filters.to));
  for (const value of filters.sources ?? []) params.append('source', value);
  for (const value of filters.types ?? []) params.append('type', value);
  return params;
}

/** The query of GET /map/cells. */
export function cellsQuery(
  filters: Filters,
  resolution: number,
  bbox?: BBox,
  kind?: RecordKind,
): string {
  const params = filterParams(filters);
  params.set('resolution', String(resolution));
  if (bbox) params.set('bbox', bboxParam(bbox));
  if (kind) params.set('kind', kind);
  return params.toString();
}

/** The query of GET /map/points (the box is required). */
export function pointsQuery(
  filters: Filters,
  bbox: BBox,
  limit?: number,
  kind?: RecordKind,
): string {
  const params = filterParams(filters);
  params.set('bbox', bboxParam(bbox));
  if (limit !== undefined) params.set('limit', String(limit));
  if (kind) params.set('kind', kind);
  return params.toString();
}

/** What GET /search takes besides the shared filters. */
export interface SearchAsk {
  q?: string;
  /** By meaning (needs q). */
  semantic?: boolean;
  kind?: RecordKind;
  /** A logical key, such as "cyber.vendor_product/Apache". */
  logical?: string;
  /** An entity id: the events it takes part in. */
  entity?: string;
  limit?: number;
}

/** The query of GET /search. */
export function searchQuery(filters: Filters, ask: SearchAsk): string {
  const params = filterParams(filters);
  if (ask.q?.trim()) params.set('q', ask.q.trim());
  if (ask.semantic) params.set('semantic', 'true');
  if (ask.kind) params.set('kind', ask.kind);
  if (ask.logical) params.set('logical', ask.logical);
  if (ask.entity) params.set('entity', ask.entity);
  if (ask.limit !== undefined) params.set('limit', String(ask.limit));
  return params.toString();
}

/** The query of GET /logical: the children of node, or the top level. */
export function logicalQuery(filters: Filters, node?: string): string {
  const params = filterParams(filters);
  if (node) params.set('node', node);
  return params.toString();
}

/** The query of GET /graph. */
export function graphQuery(id: string, limit?: number): string {
  const params = new URLSearchParams({ id });
  if (limit !== undefined) params.set('limit', String(limit));
  return params.toString();
}

/** The path and query of GET /records/{id}: each part of the id encoded, its "/" kept. */
export function recordPath(id: string, asOf?: number, source?: string): string {
  const path = `/records/${id.split('/').map(encodeURIComponent).join('/')}`;
  const params = new URLSearchParams();
  if (asOf !== undefined) params.set('as_of', isoOf(asOf));
  if (source) params.set('source', source);
  const query = params.toString();
  return query ? `${path}?${query}` : path;
}

/** "min_lon,min_lat,max_lon,max_lat", to 4 decimals (about 10 m). */
export function bboxParam(bbox: BBox): string {
  return [bbox.west, bbox.south, bbox.east, bbox.north].map((value) => round4(value)).join(',');
}

function round4(value: number): number {
  return Math.round(value * 10_000) / 10_000;
}
