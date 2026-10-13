/**
 * useEvents: the events of the time window in view, as lights.
 *
 * The map asks for the events themselves (layer=points, up to 5,000), so every light has its type's colour. When more
 * events match than one answer holds, it asks for hexagons instead and draws their density as warm lights, which grow
 * with the count; zooming in brings back the events one by one.
 */
import { fetchMapLayer } from '../api/client';
import type { MapLayer } from '../api/model';
import { type Filters, type MapView, mapQuery } from '../api/query';
import { useLatest } from '../lib/useLatest';
import { type LightSet, lightsOfCells, lightsOfPoints } from './lights';

/** The most events drawn one by one. */
export const mostPoints = 5000;

export interface Events {
  /** The answer drawn: points, or cells when there were too many points. */
  layer: MapLayer;
  lights: LightSet;
}

export function useEvents(filters: Filters, view: MapView | null) {
  const pointsView: MapView | null = view && { ...view, layer: 'points', limit: mostPoints };
  const key = pointsView ? mapQuery(filters, pointsView) : null;
  return useLatest<Events>(key, async (signal) => {
    const points = await fetchMapLayer(filters, pointsView!, signal);
    if (!points.next) {
      return { layer: points, lights: lightsOfPoints(points.points) };
    }
    const cells = await fetchMapLayer(filters, { ...view!, layer: 'cells' }, signal);
    return { layer: cells, lights: lightsOfCells(cells.cells) };
  });
}
