/**
 * The events drawn on the map, as GeoJSON for MapLibre. Pure: the map layers (layers.ts) only read these properties.
 *
 * Hexagons are 3D columns. Their height and colour both say how many events the hexagon holds, against the largest
 * hexagon in view (`scaleMax`): v = sqrt(count / scaleMax), from 0 to 1, so a hexagon with four times the events stands
 * twice as tall. The height is measured in the hexagon's own edge, so columns keep their proportions at every zoom.
 */
import type { FeatureCollection, Point, Polygon } from 'geojson';
import type { Cell, Item, LngLat } from '../api/model';
import { colorOfType, domainOf } from './palette';

/** The edge of an H3 cell, in metres, by resolution (as the contract states them). */
const edgeMetres: Record<number, number> = { 2: 158_000, 3: 60_000, 4: 23_000, 5: 8_500 };

/**
 * The tallest column, in edges of its hexagon, by resolution. The large hexagons of the whole-Earth view stand lower,
 * so the columns on the far side do not rise over the horizon.
 */
const tallestEdges: Record<number, number> = { 2: 2.5, 3: 3.5 };
const tallest = 4;

/** The shortest column (one event), in edges, so a single event still stands. */
const shortest = 0.25;

/** A column as drawn: where, how tall, and its value on the ramp. */
export interface Column {
  id: string;
  count: number;
  boundary: LngLat[];
  center: LngLat;
  /** Height in metres. */
  height: number;
  /** sqrt(count / scaleMax), 0 to 1. */
  value: number;
}

/** The columns of a layer of cells. */
export function columnsOf(cells: Cell[], scaleMax: number): Column[] {
  const max = Math.max(1, scaleMax);
  return cells.map((cell) => {
    const value = Math.sqrt(Math.min(cell.count, max) / max);
    const edge = edgeMetres[cell.resolution] ?? 60_000;
    return {
      id: cell.id,
      count: cell.count,
      boundary: cell.boundary,
      center: cell.center,
      height: edge * (shortest + ((tallestEdges[cell.resolution] ?? tallest) - shortest) * value),
      value,
    };
  });
}

/**
 * The columns part of the way (`t`, 0 to 1) from one layer to the next: a column in both changes height, a new one
 * rises from the ground, and one that is gone sinks into it.
 */
export function blendColumns(from: Column[], to: Column[], t: number): Column[] {
  if (t >= 1) return to;
  const before = new Map(from.map((column) => [column.id, column]));
  const blended: Column[] = to.map((column) => {
    const old = before.get(column.id);
    before.delete(column.id);
    return {
      ...column,
      height: lerp(old?.height ?? 0, column.height, t),
      value: lerp(old?.value ?? column.value, column.value, t),
    };
  });
  for (const old of before.values()) {
    blended.push({ ...old, height: lerp(old.height, 0, t) });
  }
  return blended;
}

/** Columns as GeoJSON polygons (the boundary closed), with the properties the layers read. */
export function columnFeatures(columns: Column[]): FeatureCollection<Polygon> {
  return {
    type: 'FeatureCollection',
    features: columns.map((column) => ({
      type: 'Feature',
      id: column.id,
      properties: {
        id: column.id,
        count: column.count,
        height: Math.round(column.height),
        value: column.value,
        lon: column.center[0],
        lat: column.center[1],
      },
      geometry: { type: 'Polygon', coordinates: [closedRing(column.boundary)] },
    })),
  };
}

/** The points of a layer: one feature per located event, coloured by the domain of its type. */
export function pointFeatures(points: Item[]): FeatureCollection<Point> {
  return {
    type: 'FeatureCollection',
    features: points.flatMap((item) => {
      const point = item.place?.point;
      if (!point) return [];
      return [
        {
          type: 'Feature' as const,
          id: item.id,
          properties: {
            id: item.id,
            label: item.label,
            type: item.type,
            domain: domainOf(item.type),
            color: colorOfType(item.type),
            start: item.time?.start ?? '',
          },
          geometry: { type: 'Point' as const, coordinates: point },
        },
      ];
    }),
  };
}

/**
 * A ring that crosses the 180th meridian (an edge longer than 180°) is unwrapped, so it is drawn as one hexagon and not
 * a band around the Earth. Then closed, as GeoJSON wants.
 */
export function closedRing(boundary: LngLat[]): LngLat[] {
  if (boundary.length === 0) return [];
  const ring: LngLat[] = [boundary[0]!];
  for (let index = 1; index < boundary.length; index++) {
    const previous = ring[index - 1]!;
    let [lon, lat] = boundary[index]!;
    while (lon - previous[0] > 180) lon -= 360;
    while (previous[0] - lon > 180) lon += 360;
    ring.push([lon, lat]);
  }
  ring.push(ring[0]!);
  return ring;
}

function lerp(from: number, to: number, t: number): number {
  return from + (to - from) * t;
}
