/**
 * The Analyze API: the three reading routes, answered in the model's terms. The map's recent answers are kept, so
 * going back to a place or a moment already seen draws at once (and the query log says it came from memory). Answers
 * without as_of ("now") are kept a few minutes only, since now moves.
 */
import { analyzeApiUrl } from '../config';
import { mapLayerOf } from './adapt';
import type { WireMapAnswer } from './contract';
import { getJson } from './http';
import type { MapLayer } from './model';
import { logMemory } from './queryLog';
import { type Filters, type MapView, mapQuery } from './query';

/** How many map answers are kept, and how long an answer about "now" stays good. */
const keptAnswers = 160;
const nowAnswerMs = 5 * 60_000;

const mapAnswers = new Map<string, { wire: WireMapAnswer; layer: MapLayer; at: number }>();

/** GET /analyze/map: one layer of the map for the filters and the view. */
export async function fetchMapLayer(
  filters: Filters,
  view: MapView,
  signal?: AbortSignal,
): Promise<MapLayer> {
  const url = `${analyzeApiUrl}/analyze/map?${mapQuery(filters, view)}`;
  const kept = mapAnswers.get(url);
  if (kept && (filters.asOf !== undefined || Date.now() - kept.at < nowAnswerMs)) {
    // Most recently used last, so the oldest is the first to go.
    mapAnswers.delete(url);
    mapAnswers.set(url, kept);
    logMemory(url, kept.wire);
    return kept.layer;
  }
  const wire = await getJson<WireMapAnswer>(url, signal);
  const layer = mapLayerOf(wire);
  mapAnswers.set(url, { wire, layer, at: Date.now() });
  if (mapAnswers.size > keptAnswers) {
    const oldest = mapAnswers.keys().next().value;
    if (oldest !== undefined) mapAnswers.delete(oldest);
  }
  return layer;
}
