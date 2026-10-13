/**
 * The questions Analyze asks, as query strings (contracts/synapsis-api.yaml, components.parameters).
 *
 * - A list filter repeats its parameter (type=A&type=B): any of its values matches; different filters must all match.
 * - Ids travel as query values (id=, entity=), encoded by URLSearchParams, because some contain "/". GET /records/{id}
 *   is the exception: the id is the path, "/" included.
 * - Times are sent as UTC date-times ("2026-10-13T00:00:00Z").
 *
 * Every filter the API knows has its place in `Filters`, so a filter chosen in the details (a type, a country, a
 * participant...) only has to be added to it.
 */
import type { RecordKind } from './model';

/** The filters shared by the map and the graph. Each is optional; an empty list means no filter. */
export interface Filters {
  /** What was known by then (ms since 1970, UTC); none = now. */
  asOf?: number;
  /** Events that start at or after this moment (ms). */
  from?: number;
  /** Events that start before this moment (ms). */
  to?: number;
  /** Keywords. */
  q?: string;
  kinds?: RecordKind[];
  types?: string[];
  sources?: string[];
  /** H3 cells (resolution 2 to 5). */
  cells?: string[];
  /** ISO 3166-1 alpha-2 codes. */
  countries?: string[];
  /** ISO 3166-2 codes. */
  subdivisions?: string[];
  /** Logical nodes, such as "cyber.vendor_product/Apache". */
  logical?: string[];
  /** Entity ids: events in which one of them takes part. */
  entities?: string[];
  /** "name:op:value", such as "magnitude:gte:5.5"; all must hold. */
  measures?: string[];
}

/** A box on the map, in degrees. A west edge east of the east edge crosses the 180th meridian. */
export interface BBox {
  west: number;
  south: number;
  east: number;
  north: number;
}

/** What the map asks besides the filters. */
export interface MapView {
  /** The map's zoom (0 to 22). */
  zoom: number;
  /** The box in view; none = the whole Earth. */
  bbox?: BBox;
  layer?: 'auto' | 'points' | 'cells';
  limit?: number;
  cursor?: string;
}

/** A moment as the API reads it: UTC, whole seconds. */
export function isoOf(ms: number): string {
  return new Date(Math.floor(ms / 1000) * 1000).toISOString().replace('.000Z', 'Z');
}

/** The shared filters as query parameters, in a stable order. */
export function filterParams(filters: Filters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.asOf !== undefined) params.set('as_of', isoOf(filters.asOf));
  if (filters.from !== undefined) params.set('from', isoOf(filters.from));
  if (filters.to !== undefined) params.set('to', isoOf(filters.to));
  if (filters.q?.trim()) params.set('q', filters.q.trim());
  const lists: [string, string[] | undefined][] = [
    ['kind', filters.kinds],
    ['type', filters.types],
    ['source', filters.sources],
    ['cell', filters.cells],
    ['country', filters.countries],
    ['subdivision', filters.subdivisions],
    ['logical', filters.logical],
    ['entity', filters.entities],
    ['measure', filters.measures],
  ];
  for (const [name, values] of lists) {
    for (const value of values ?? []) {
      params.append(name, value);
    }
  }
  return params;
}

/** The query of GET /analyze/map. */
export function mapQuery(filters: Filters, view: MapView): string {
  const params = filterParams(filters);
  if (view.bbox) params.set('bbox', bboxParam(view.bbox));
  params.set('zoom', String(Math.round(view.zoom * 100) / 100));
  if (view.layer && view.layer !== 'auto') params.set('layer', view.layer);
  if (view.limit !== undefined) params.set('limit', String(view.limit));
  if (view.cursor) params.set('cursor', view.cursor);
  return params.toString();
}

/** The query of GET /analyze/graph. */
export function graphQuery(
  id: string,
  filters: Filters,
  page: { group?: string; limit?: number; cursor?: string } = {},
): string {
  const params = new URLSearchParams({ id });
  for (const [name, value] of filterParams(filters)) {
    params.append(name, value);
  }
  if (page.group) params.set('group', page.group);
  if (page.limit !== undefined) params.set('limit', String(page.limit));
  if (page.cursor) params.set('cursor', page.cursor);
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
