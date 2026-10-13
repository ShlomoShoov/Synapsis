/**
 * The Analyze API: the three reading routes, answered in the model's terms. The map's recent answers are kept, so
 * going back to a place or a moment already seen draws at once.
 */
import { analyzeApiUrl } from '../config';
import { mapLayerOf } from './adapt';
import type { WireMapAnswer } from './contract';
import { getJson } from './http';
import type { MapLayer } from './model';
import { type Filters, type MapView, mapQuery } from './query';

/** How many map answers are kept. */
const keptAnswers = 120;

const mapAnswers = new Map<string, MapLayer>();

/** GET /analyze/map: one layer of the map for the filters and the view. */
export async function fetchMapLayer(
  filters: Filters,
  view: MapView,
  signal?: AbortSignal,
): Promise<MapLayer> {
  const url = `${analyzeApiUrl}/analyze/map?${mapQuery(filters, view)}`;
  const kept = mapAnswers.get(url);
  if (kept) {
    // Most recently used last, so the oldest is the first to go.
    mapAnswers.delete(url);
    mapAnswers.set(url, kept);
    return kept;
  }
  const layer = mapLayerOf(await getJson<WireMapAnswer>(url, signal));
  mapAnswers.set(url, layer);
  if (mapAnswers.size > keptAnswers) {
    const oldest = mapAnswers.keys().next().value;
    if (oldest !== undefined) mapAnswers.delete(oldest);
  }
  return layer;
}
