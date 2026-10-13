/**
 * What Analyze works with: the answers of the three routes, in the client's own words (adapt.ts makes them from the
 * wire). Times stay ISO strings in UTC, as they arrive.
 */

export type RecordKind = 'entity' | 'event' | 'conclusion';

/** [longitude, latitude] */
export type LngLat = [number, number];

export interface Measure {
  name: string;
  value: number;
  unit?: string;
}

/** Another record named by this one: its id and what to show. */
export interface EntityLabel {
  id: string;
  label: string;
}

/** When an event happened. */
export interface ItemTime {
  start: string;
  end?: string;
  /** second, minute, hour, day, week, month, quarter or year */
  precision?: string;
}

/** Where an event happened. Any part may be missing: a place given by region only has no point. */
export interface ItemPlace {
  point?: LngLat;
  uncertaintyKm?: number;
  /** ISO 3166-1 alpha-2 */
  country?: string;
  /** ISO 3166-2 */
  subdivision?: string;
  areaName?: string;
}

/**
 * One record in short: the map's points, the graph's centre and every connected node. Each value that can become a
 * filter (type, sources, country, subdivision, logical node, entity) is kept as its plain value, so the details can
 * offer it as a filter later.
 */
export interface Item {
  id: string;
  kind: RecordKind;
  type: string;
  sources: string[];
  knownAt: string;
  label: string;
  labelFrom: string;
  time?: ItemTime;
  place?: ItemPlace;
  logical: string[];
  measures: Measure[];
  participants: EntityLabel[];
}

/** One H3 hexagon of the map, with the matching events in it. */
export interface Cell {
  id: string;
  resolution: number;
  count: number;
  center: LngLat;
  boundary: LngLat[];
}

/** Matching events the map cannot draw, and why. */
export interface Omitted {
  reason: string;
  count: number;
  message: string;
}

/** GET /analyze/map: one layer of the map, either points or cells. */
export interface MapLayer {
  asOf: string;
  layer: 'points' | 'cells';
  resolution?: number;
  /** The matching events drawn, in all the points or cells. */
  events: number;
  /** All the points or cells, not only this page. */
  total: number;
  /** The cursor of the next page; absent on the last one. */
  next?: string;
  omitted: Omitted[];
  points: Item[];
  cells: Cell[];
}
