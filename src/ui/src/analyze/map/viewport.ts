/**
 * What part of the Earth the map asks about. Pure.
 *
 * Seen from afar the map asks about the whole Earth (no bbox): the globe shows half of it and turns freely. Closer,
 * it asks about the box in view, widened by half on each side so a short pan needs no new answer, and snapped to a grid
 * so that nearly the same view asks exactly the same question (and the answer kept from before is used).
 */
import type { BBox, MapView } from '../api/query';

/** Below this zoom the whole Earth is asked for. */
export const wholeEarthBelowZoom = 3;

/** A box this wide or wider (in degrees of longitude) is asked as the whole Earth. */
const wholeEarthFromSpan = 300;

/** The question for a view: its zoom (whole steps: the API reads it only to choose the layer) and its box. */
export function requestOf(zoom: number, bounds: BBox | null): MapView {
  const whole = Math.max(0, Math.floor(zoom));
  if (zoom < wholeEarthBelowZoom || bounds === null) {
    return { zoom: whole };
  }
  const box = snapped(widened(bounds, 0.5));
  if (box.east - box.west >= wholeEarthFromSpan) {
    return { zoom: whole };
  }
  return {
    zoom: whole,
    bbox: { west: wrap(box.west), east: wrap(box.east), south: box.south, north: box.north },
  };
}

/**
 * The box widened by `share` of its size on each side, unwrapped: east is always greater than west (it may pass 180;
 * a box across the 180th meridian becomes, say, 170 to 190).
 */
export function widened(bounds: BBox, share: number): BBox {
  const width = spanOf(bounds.west, bounds.east);
  const height = bounds.north - bounds.south;
  const west = bounds.west - width * share;
  return {
    west,
    east: west + width * (1 + 2 * share),
    south: Math.max(-90, bounds.south - height * share),
    north: Math.min(90, bounds.north + height * share),
  };
}

/** An unwrapped box's edges moved outward onto a grid of about an eighth of its size. */
export function snapped(box: BBox): BBox {
  const step = gridStep(Math.max(box.east - box.west, box.north - box.south) / 8);
  return {
    west: Math.floor(box.west / step) * step,
    east: Math.ceil(box.east / step) * step,
    south: Math.max(-90, Math.floor(box.south / step) * step),
    north: Math.min(90, Math.ceil(box.north / step) * step),
  };
}

/** Degrees from west to east, eastward (crossing the 180th meridian when west > east). */
export function spanOf(west: number, east: number): number {
  const span = east - west;
  return span >= 0 ? span : span + 360;
}

/** A longitude brought into -180..180 (180 stays 180). */
export function wrap(lon: number): number {
  if (lon >= -180 && lon <= 180) return lon;
  return ((((lon + 180) % 360) + 360) % 360) - 180;
}

/** The round step (1, 2 or 5 times a power of ten) at or above `size` degrees. */
function gridStep(size: number): number {
  const power = 10 ** Math.floor(Math.log10(Math.max(size, 1e-6)));
  for (const factor of [1, 2, 5, 10]) {
    if (factor * power >= size) return factor * power;
  }
  return 10 * power;
}
