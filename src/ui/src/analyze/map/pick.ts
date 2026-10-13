/**
 * Which light is under the pointer. The lights are drawn on the GPU, so MapLibre cannot be asked; instead each light is
 * projected to the screen and the nearest one within reach is taken. Lights on the far side of the globe are skipped
 * (MapLibre would still project them, onto the near side). Pure: the projection is passed in.
 */
import type { LngLat } from '../api/model';

/** How far from a light, in pixels, the pointer still names it. */
export const reachPx = 14;

/** The light nearest to (x, y) within reach, or -1. `facing` skips lights more than ~85° from the view's centre. */
export function nearestLight(
  places: LngLat[],
  x: number,
  y: number,
  project: (place: LngLat) => { x: number; y: number },
  center: LngLat,
  reach = reachPx,
): number {
  const centre = unit(center);
  let best = -1;
  let bestDistance = reach * reach;
  places.forEach((place, index) => {
    if (dot(unit(place), centre) < 0.09) return; // behind the horizon
    const point = project(place);
    const distance = (point.x - x) ** 2 + (point.y - y) ** 2;
    if (distance < bestDistance) {
      bestDistance = distance;
      best = index;
    }
  });
  return best;
}

/** A place as a point on the unit sphere. */
export function unit([lon, lat]: LngLat): [number, number, number] {
  const phi = (lat * Math.PI) / 180;
  const lambda = (lon * Math.PI) / 180;
  return [Math.cos(phi) * Math.cos(lambda), Math.cos(phi) * Math.sin(lambda), Math.sin(phi)];
}

function dot(a: [number, number, number], b: [number, number, number]): number {
  return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
}
