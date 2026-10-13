/**
 * The read API (EMERGENCY.md §12): one function per route. The map's recent answers are kept, so going back to a
 * place or a window already seen draws at once (and the query log says it came from memory).
 */
import { analyzeApiUrl } from '../config';
import { cellsLayerOf, itemOf, pointsLayerOf } from './adapt';
import type { WireCells, WireGraph, WireList, WireLogical, WireRecordAnswer } from './contract';
import { getJson } from './http';
import type { Item, MapLayer } from './model';
import { logMemory } from './queryLog';
import {
  type BBox,
  type Filters,
  type SearchAsk,
  cellsQuery,
  graphQuery,
  logicalQuery,
  pointsQuery,
  recordPath,
  searchQuery,
} from './query';

/** How many map answers are kept. */
const keptAnswers = 160;

const mapAnswers = new Map<string, unknown>();

/** GET a map answer, from memory when this exact question was asked before. */
async function getKept<T>(url: string, signal?: AbortSignal): Promise<T> {
  if (mapAnswers.has(url)) {
    const kept = mapAnswers.get(url) as T;
    // Most recently used last, so the oldest is the first to go.
    mapAnswers.delete(url);
    mapAnswers.set(url, kept);
    logMemory(url, kept);
    return kept;
  }
  const wire = await getJson<T>(url, signal);
  mapAnswers.set(url, wire);
  if (mapAnswers.size > keptAnswers) {
    const oldest = mapAnswers.keys().next().value;
    if (oldest !== undefined) mapAnswers.delete(oldest);
  }
  return wire;
}

/** GET /map/cells: the records per H3 cell, as a cells layer. */
export async function fetchCells(
  filters: Filters,
  resolution: number,
  bbox: BBox | undefined,
  signal?: AbortSignal,
): Promise<MapLayer> {
  const url = `${analyzeApiUrl}/map/cells?${cellsQuery(filters, resolution, bbox)}`;
  return cellsLayerOf(await getKept<WireCells>(url, signal), resolution);
}

/** GET /map/points: the records with a point in the box, as a points layer. */
export async function fetchPoints(
  filters: Filters,
  bbox: BBox,
  limit: number,
  signal?: AbortSignal,
): Promise<MapLayer> {
  const url = `${analyzeApiUrl}/map/points?${pointsQuery(filters, bbox, limit)}`;
  return pointsLayerOf(await getKept<WireList>(url, signal));
}

/** GET /search: total = all the records that match; items = at most the limit. */
export async function fetchSearch(
  filters: Filters,
  ask: SearchAsk,
  signal?: AbortSignal,
): Promise<{ total: number; items: Item[] }> {
  const wire = await getJson<WireList>(
    `${analyzeApiUrl}/search?${searchQuery(filters, ask)}`,
    signal,
  );
  return { total: wire.total, items: wire.items.map(itemOf) };
}

/** GET /logical: one level of the logical tree. */
export function fetchLogical(
  filters: Filters,
  node?: string,
  signal?: AbortSignal,
): Promise<WireLogical> {
  return getJson<WireLogical>(`${analyzeApiUrl}/logical?${logicalQuery(filters, node)}`, signal);
}

/** GET /graph: a node and its direct links. */
export function fetchGraph(id: string, limit?: number, signal?: AbortSignal): Promise<WireGraph> {
  return getJson<WireGraph>(`${analyzeApiUrl}/graph?${graphQuery(id, limit)}`, signal);
}

/** GET /records/{id}: each source's statement of the record, as stored. */
export function fetchRecord(id: string, signal?: AbortSignal): Promise<WireRecordAnswer[]> {
  return getJson<WireRecordAnswer[]>(`${analyzeApiUrl}${recordPath(id)}`, signal);
}
