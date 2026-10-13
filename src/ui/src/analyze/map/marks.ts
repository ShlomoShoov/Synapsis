/**
 * The conclusions on the map: their own layer of glyphs over the events' round lights, one glyph and colour per type
 * (conclusion/conclusion.ts). A layer of points gives one mark per conclusion, at its cell's centre; a layer of cells
 * (too many to draw one by one) gives one hollow mark per cell, larger with more. Pure.
 */
import { conclusionTypeOf, type Shape } from '../conclusion/conclusion';
import type { Item, LngLat, MapLayer } from '../api/model';

/** The image of the mark of many conclusions in one cell. */
export const cellIcon = 'con:cell';

/** The image of a conclusion type's mark: its shape and colour. */
export function iconOf(shape: Shape, color: string): string {
  return `con:${shape}:${color}`;
}

export interface MarkFeature {
  type: 'Feature';
  id: number;
  geometry: { type: 'Point'; coordinates: LngLat };
  properties: { icon: string; size: number };
}

/** What the map draws and what picking needs to name a mark (the same index in every list). */
export interface MarkSet {
  ids: string[];
  places: LngLat[];
  /** Per mark: the conclusion, for a point; undefined for a cell. */
  items: (Item | undefined)[];
  /** Per mark: how many conclusions (1 for a point). */
  counts: number[];
  features: { type: 'FeatureCollection'; features: MarkFeature[] };
}

export function marksOfLayer(layer: MapLayer): MarkSet {
  const set: MarkSet = {
    ids: [],
    places: [],
    items: [],
    counts: [],
    features: { type: 'FeatureCollection', features: [] },
  };
  const add = (
    id: string,
    place: LngLat,
    item: Item | undefined,
    count: number,
    icon: string,
    size: number,
  ) => {
    set.features.features.push({
      type: 'Feature',
      id: set.ids.length,
      geometry: { type: 'Point', coordinates: place },
      properties: { icon, size },
    });
    set.ids.push(id);
    set.places.push(place);
    set.items.push(item);
    set.counts.push(count);
  };
  if (layer.layer === 'points') {
    for (const item of layer.points) {
      const place = item.place?.point;
      if (!place) continue;
      const style = conclusionTypeOf(item.type);
      add(item.id, place, item, 1, iconOf(style.shape, style.color), 1);
    }
  } else {
    const most = layer.cells.reduce((max, cell) => Math.max(max, cell.count), 1);
    for (const cell of layer.cells) {
      add(
        cell.id,
        cell.center,
        undefined,
        cell.count,
        cellIcon,
        0.9 + 1.1 * Math.sqrt(cell.count / most),
      );
    }
  }
  return set;
}
