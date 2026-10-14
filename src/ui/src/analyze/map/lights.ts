/**
 * The events as lights: the arrays the GPU draws (LightsLayer.ts). Pure.
 *
 * Each light has a place (web mercator, 0..1), a colour (its category), the day its event started (for LIVE), a seed
 * (its own twinkle) and a size. A layer of points gives one light per event; a layer of hexagons (when there are too
 * many events to send one by one) gives one warm light per hexagon, larger with more events.
 */
import { MercatorCoordinate } from 'maplibre-gl';
import type { Cell, Item, LngLat } from '../api/model';
import { categoryOf, densityColor, rgbOf } from './palette';

const dayMs = 86_400_000;

/** Days are counted from here, so they fit a 32-bit float to within minutes. */
export const dayZero = Date.UTC(1900, 0, 1);

/** What the GPU draws, and what picking needs to name a light. */
export interface LightSet {
  count: number;
  /** x, y per light (web mercator, 0..1). */
  positions: Float32Array;
  /** r, g, b per light (0..1). */
  colors: Float32Array;
  /** The day the event started, since dayZero (NaN for a hexagon: always shown). */
  days: Float32Array;
  /** 0..1, a light's own twinkle. */
  seeds: Float32Array;
  /** 1 for one event; more for a hexagon of many. */
  sizes: Float32Array;
  /** Per light: the record id (a point), the cell id (a hexagon), or `rough:` and the cell id (events placed only
   *  to that cell, map/rough.ts). */
  ids: string[];
  places: LngLat[];
  /** Per light: the item, for a point. */
  items: (Item | undefined)[];
  /** Per light: how many events (1 for a point). */
  counts: number[];
}

/** The day of an ISO time, counted from dayZero. */
export function dayOf(iso: string | undefined): number {
  const ms = iso ? Date.parse(iso) : Number.NaN;
  return Number.isNaN(ms) ? Number.NaN : (ms - dayZero) / dayMs;
}

/** The day of a moment in ms, counted from dayZero. */
export function dayOfMs(ms: number): number {
  return (ms - dayZero) / dayMs;
}

/** A stable number in 0..1 from an id, so a light twinkles the same way every time it is drawn. */
export function seedOf(id: string): number {
  let hash = 2166136261;
  for (let index = 0; index < id.length; index++) {
    hash ^= id.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0) / 4294967296;
}

/** One light per located event. */
export function lightsOfPoints(points: Item[]): LightSet {
  const located = points.filter((item) => item.place?.point);
  const set = emptySet(located.length);
  located.forEach((item, index) => {
    const place = item.place!.point!;
    put(set, index, {
      id: item.id,
      place,
      color: categoryOf(item.type).color,
      day: dayOf(item.time?.start),
      size: 1,
      count: 1,
    });
    set.items[index] = item;
  });
  return set;
}

/** One light per hexagon: warm white, larger with more events (by the square root, as an area). */
/** The start of a rough light's id. */
export const roughPrefix = 'rough:';

export function lightsOfCells(cells: Cell[]): LightSet {
  const set = emptySet(cells.length);
  const most = cells.reduce((max, cell) => Math.max(max, cell.count), 1);
  cells.forEach((cell, index) => {
    put(set, index, {
      id: cell.rough ? `${roughPrefix}${cell.id}` : cell.id,
      place: cell.center,
      color: densityColor,
      day: Number.NaN,
      size: 1 + 2.2 * Math.sqrt(cell.count / most),
      count: cell.count,
    });
  });
  return set;
}

/** The lights of both sets, the first set's first (LIVE adds the next stretch of time to what it has). */
export function joined(first: LightSet, second: LightSet): LightSet {
  const known = new Set(first.ids);
  const keep = second.ids.flatMap((id, index) => (known.has(id) ? [] : [index]));
  const set = emptySet(first.count + keep.length);
  copy(first, set, [...Array(first.count).keys()], 0);
  copy(second, set, keep, first.count);
  return set;
}

/** Only the lights whose day is at or after `fromDay` (LIVE lets go of what has faded). */
export function since(source: LightSet, fromDay: number): LightSet {
  const keep = [...Array(source.count).keys()].filter((index) => !(source.days[index]! < fromDay));
  if (keep.length === source.count) return source;
  const set = emptySet(keep.length);
  copy(source, set, keep, 0);
  return set;
}

export function emptySet(count: number): LightSet {
  return {
    count,
    positions: new Float32Array(count * 2),
    colors: new Float32Array(count * 3),
    days: new Float32Array(count),
    seeds: new Float32Array(count),
    sizes: new Float32Array(count),
    ids: new Array<string>(count),
    places: new Array<LngLat>(count),
    items: new Array<Item | undefined>(count),
    counts: new Array<number>(count),
  };
}

function put(
  set: LightSet,
  index: number,
  light: { id: string; place: LngLat; color: string; day: number; size: number; count: number },
) {
  const mercator = MercatorCoordinate.fromLngLat({ lng: light.place[0], lat: light.place[1] });
  set.positions[index * 2] = mercator.x;
  set.positions[index * 2 + 1] = mercator.y;
  set.colors.set(rgbOf(light.color), index * 3);
  set.days[index] = light.day;
  set.seeds[index] = seedOf(light.id);
  set.sizes[index] = light.size;
  set.ids[index] = light.id;
  set.places[index] = light.place;
  set.counts[index] = light.count;
}

function copy(from: LightSet, to: LightSet, indices: number[], offset: number) {
  indices.forEach((index, position) => {
    const at = offset + position;
    to.positions.set(from.positions.subarray(index * 2, index * 2 + 2), at * 2);
    to.colors.set(from.colors.subarray(index * 3, index * 3 + 3), at * 3);
    to.days[at] = from.days[index]!;
    to.seeds[at] = from.seeds[index]!;
    to.sizes[at] = from.sizes[index]!;
    to.ids[at] = from.ids[index]!;
    to.places[at] = from.places[index]!;
    to.items[at] = from.items[index];
    to.counts[at] = from.counts[index]!;
  });
}
