/**
 * The filter of the current view: the sources, types and kind of record kept, and (for search) a logical place and an
 * entity. Every dimension shows only what matches: the map sends source, type and kind to its routes (the time is the
 * time card's window), the tree sends source and type, the graph hides the drawn nodes that do not match, and the
 * search window starts from it. Pure.
 */
import type { RecordKind } from '../api/model';
import { dayMs, type TimeWindow } from '../time/window';

export interface ViewFilter {
  sources: string[];
  types: string[];
  kind: RecordKind | null;
  logical: string | null;
  entity: string | null;
}

/** A field of a record that can become a filter. */
export type FilterField = 'source' | 'type' | 'kind' | 'logical' | 'entity';

export const noFilter: ViewFilter = {
  sources: [],
  types: [],
  kind: null,
  logical: null,
  entity: null,
};

/** The filter with one more value (a list field gains it, a single field is set to it). */
export function withValue(filter: ViewFilter, field: FilterField, value: string): ViewFilter {
  if (field === 'source') return { ...filter, sources: added(filter.sources, value) };
  if (field === 'type') return { ...filter, types: added(filter.types, value) };
  if (field === 'kind') return { ...filter, kind: value as RecordKind };
  return { ...filter, [field]: value };
}

/** The filter without one value. */
export function withoutValue(filter: ViewFilter, field: FilterField, value: string): ViewFilter {
  if (field === 'source') return { ...filter, sources: filter.sources.filter((v) => v !== value) };
  if (field === 'type') return { ...filter, types: filter.types.filter((v) => v !== value) };
  return { ...filter, [field]: null };
}

/** Each value of the filter, as a chip. */
export function chipsOf(filter: ViewFilter): { field: FilterField; value: string }[] {
  return [
    ...(filter.kind ? [{ field: 'kind' as const, value: filter.kind }] : []),
    ...filter.sources.map((value) => ({ field: 'source' as const, value })),
    ...filter.types.map((value) => ({ field: 'type' as const, value })),
    ...(filter.logical ? [{ field: 'logical' as const, value: filter.logical }] : []),
    ...(filter.entity ? [{ field: 'entity' as const, value: filter.entity }] : []),
  ];
}

/** Whether a drawn record matches (the graph): its kind, its type, and any of its sources. */
export function matches(
  filter: ViewFilter,
  record: { kind: RecordKind; type: string; sources: string[] },
): boolean {
  if (filter.kind && record.kind !== filter.kind) return false;
  if (filter.types.length > 0 && !filter.types.includes(record.type)) return false;
  return filter.sources.length === 0 || record.sources.some((s) => filter.sources.includes(s));
}

function added(list: string[], value: string): string[] {
  return list.includes(value) ? list : [...list, value];
}

/**
 * The time window of a record's date ("start|end|precision", as the details give it): its day, month or year, from
 * the start to the end of the record's time; null for a date that cannot be read.
 */
export function timeWindowOf(value: string): TimeWindow | null {
  const [start = '', end = '', precision = 'day'] = value.split('|');
  const from = Date.parse(start);
  const until = end ? Date.parse(end) : from;
  if (Number.isNaN(from) || Number.isNaN(until)) return null;
  const first = new Date(from);
  const last = new Date(until);
  if (precision === 'year') {
    return {
      from: Date.UTC(first.getUTCFullYear(), 0, 1),
      to: Date.UTC(last.getUTCFullYear() + 1, 0, 1),
    };
  }
  if (precision === 'month' || precision === 'quarter') {
    return {
      from: Date.UTC(first.getUTCFullYear(), first.getUTCMonth(), 1),
      to: Date.UTC(last.getUTCFullYear(), last.getUTCMonth() + 1, 1),
    };
  }
  const day = (ms: number) => Math.floor(ms / dayMs) * dayMs;
  return { from: day(from), to: day(until) + dayMs };
}
