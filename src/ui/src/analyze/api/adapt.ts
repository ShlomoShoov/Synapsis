/**
 * The wire (contract.ts) to the model (model.ts). The only place that knows the answers' field names.
 */
import type { WireMapAnswer, WireMapCell, WireRecordSummary } from './contract';
import type { Cell, Item, MapLayer } from './model';

/** A record summary as an item. */
export function itemOf(wire: WireRecordSummary): Item {
  const hasPlace =
    wire.point !== undefined ||
    wire.country !== undefined ||
    wire.subdivision !== undefined ||
    wire.area_name !== undefined;
  return {
    id: wire.id,
    kind: wire.kind,
    type: wire.type,
    sources: wire.sources ?? [],
    knownAt: wire.known_at,
    label: wire.label,
    labelFrom: wire.label_from,
    time: wire.time_start
      ? { start: wire.time_start, end: wire.time_end, precision: wire.time_precision }
      : undefined,
    place: hasPlace
      ? {
          point: wire.point,
          uncertaintyKm: wire.uncertainty_km,
          country: wire.country,
          subdivision: wire.subdivision,
          areaName: wire.area_name,
        }
      : undefined,
    logical: wire.logical ?? [],
    measures: (wire.measures ?? []).flatMap((measure) =>
      measure.name !== undefined && typeof measure.value === 'number'
        ? [{ name: measure.name, value: measure.value, unit: measure.unit }]
        : [],
    ),
    participants: wire.participants ?? [],
  };
}

/** A map cell. */
export function cellOf(wire: WireMapCell): Cell {
  return {
    id: wire.cell,
    resolution: wire.resolution,
    count: wire.count,
    center: wire.center,
    boundary: wire.boundary,
  };
}

/** A map answer as a layer. */
export function mapLayerOf(wire: WireMapAnswer): MapLayer {
  return {
    asOf: wire.as_of,
    layer: wire.layer,
    resolution: wire.resolution,
    events: wire.events,
    total: wire.total,
    next: wire.next,
    omitted: wire.omitted ?? [],
    points: (wire.points ?? []).map(itemOf),
    cells: (wire.cells ?? []).map(cellOf),
  };
}
