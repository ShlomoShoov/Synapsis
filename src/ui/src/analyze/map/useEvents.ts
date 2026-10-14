/**
 * useEvents: the events of the time window in view, as lights.
 *
 * The map first asks how many records are in view, per H3 cell at the zoom's resolution (GET /map/cells). When they
 * are 5,000 or fewer, it asks for the events themselves (GET /map/points), so every light has its type's colour and
 * no point is ever cut. Otherwise it draws the cells' density as warm lights, which grow with the count; zooming in
 * brings back the events one by one. The coarser resolutions in view are counted too, so events placed only roughly
 * (no cell at this zoom's resolution) stay on the map (rough.ts).
 */
import { fetchCells, fetchPoints } from '../api/client';
import type { MapLayer, RecordKind } from '../api/model';
import { type Filters, type MapView, cellsQuery, resolutionOf, wholeEarth } from '../api/query';
import { useLatest } from '../lib/useLatest';
import { type LightSet, lightsOfCells, lightsOfPoints } from './lights';
import { withRough } from './rough';

/** The most events drawn one by one (the API's largest page of points). */
export const mostPoints = 5000;

export interface Events {
  /** The answer drawn: points, or cells when there were too many points. */
  layer: MapLayer;
  lights: LightSet;
}

/** The records of the window in view; `kind` chooses events (the default), conclusions or entities. */
export function useEvents(filters: Filters, view: MapView | null, kind?: RecordKind) {
  const resolution = view ? resolutionOf(view.zoom) : 2;
  const key = view ? cellsQuery(filters, resolution, view.bbox, kind) : null;
  return useLatest<Events>(key, async (signal) => {
    // 1. The records in view, per cell, and per coarser cell (for the events with no place fine enough for this zoom).
    const coarser = view!.bbox ? [...Array(resolution - 2).keys()].map((step) => step + 2) : [];
    const [fine, ...parents] = await Promise.all(
      [resolution, ...coarser].map((each) => fetchCells(filters, each, view!.bbox, signal, kind)),
    );
    const cells = withRough(fine!, parents);

    // 2. Few enough: the points themselves. A point too imprecise for a cell is not in the cells' sum, so a list of
    //    points that was cut after all (total above the items) gives way to the cells.
    if (cells.events <= mostPoints) {
      const points = await fetchPoints(filters, view!.bbox ?? wholeEarth, mostPoints, signal, kind);
      if (points.total <= points.points.length) {
        return { layer: points, lights: lightsOfPoints(points.points) };
      }
    }
    return { layer: cells, lights: lightsOfCells(cells.cells) };
  });
}
