/**
 * The wire (contract.ts) to the model (model.ts). The only place that knows the answers' field names. The API passes
 * the stores' objects through, so the shaping happens here: labels, places, measures, and the hexagons' corners
 * (h3-js).
 */
import { cellToBoundary, cellToLatLng } from 'h3-js';
import type { WireBucket, WireCells, WireDoc, WireList } from './contract';
import type { Cell, Item, LngLat, MapLayer } from './model';

/** The longest start of a body shown as a label. */
const bodyLabelLength = 140;

/** A search document as an item. */
export function itemOf(doc: WireDoc): Item {
  const hasPlace =
    doc.location !== undefined || doc.country !== undefined || doc.subdivision !== undefined;
  const [label, labelFrom] = labelOf(doc);
  return {
    id: doc.id,
    kind: doc.kind,
    type: doc.type,
    sources: [doc.source],
    knownAt: doc.known_at,
    label,
    labelFrom,
    time: doc.time_start ? { start: doc.time_start, precision: doc.time_precision } : undefined,
    place: hasPlace
      ? { point: doc.location, country: doc.country, subdivision: doc.subdivision }
      : undefined,
    logical: deepest(doc.logical_prefixes ?? []),
    measures: Object.entries(doc.measures ?? {}).flatMap(([name, values]) =>
      values.map((value) => ({ name, value })),
    ),
    participants: (doc.entities ?? []).map((id) => ({ id, label: id })),
  };
}

/** What to show for a record: its name, title, the start of its body, or "type · where". */
export function labelOf(doc: WireDoc): [string, string] {
  if (doc.name?.trim()) return [doc.name.trim(), 'name'];
  if (doc.title?.trim()) return [doc.title.trim(), 'title'];
  if (doc.body?.trim()) return [startOf(doc.body), 'body'];
  const where = doc.subdivision ?? doc.country;
  return [where ? `${doc.type} · ${where}` : doc.type, 'generated'];
}

/** A cell bucket as a hexagon: its record count, centre and corners ([longitude, latitude]). */
export function cellOf(bucket: WireBucket, resolution: number): Cell {
  const [lat, lng] = cellToLatLng(bucket.key);
  const boundary: LngLat[] = cellToBoundary(bucket.key).map(([cornerLat, cornerLng]) => [
    cornerLng,
    cornerLat,
  ]);
  return { id: bucket.key, resolution, count: bucket.records.value, center: [lng, lat], boundary };
}

/** GET /map/cells as a layer: the records drawn are the sum over the cells. */
export function cellsLayerOf(wire: WireCells, resolution: number): MapLayer {
  const cells = wire.buckets.map((bucket) => cellOf(bucket, resolution));
  return {
    layer: 'cells',
    resolution,
    events: recordsIn(wire),
    total: cells.length,
    omitted: [],
    points: [],
    cells,
  };
}

/** GET /map/points as a layer. */
export function pointsLayerOf(wire: WireList): MapLayer {
  const points = wire.items.map(itemOf);
  return {
    layer: 'points',
    events: points.length,
    total: wire.total,
    omitted: [],
    points,
    cells: [],
  };
}

/** The records in all the cells: the sum of each cell's distinct records. */
export function recordsIn(wire: WireCells): number {
  return wire.buckets.reduce((sum, bucket) => sum + bucket.records.value, 0);
}

/** The logical paths a record is filed under: its prefixes that are not the start of a longer one. */
function deepest(prefixes: string[]): string[] {
  return prefixes.filter((prefix) => !prefixes.some((other) => other.startsWith(`${prefix}/`)));
}

/** The start of a text, at most bodyLabelLength characters, cut at a space, with "…". */
function startOf(body: string): string {
  const text = body.replace(/\s+/g, ' ').trim();
  if (text.length <= bodyLabelLength) return text;
  const start = text.slice(0, bodyLabelLength);
  const lastSpace = start.lastIndexOf(' ');
  return `${(lastSpace > 0 ? start.slice(0, lastSpace) : start).replace(/[,;:]+$/, '')}…`;
}
