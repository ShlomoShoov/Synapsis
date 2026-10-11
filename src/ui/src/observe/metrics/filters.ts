/**
 * The filters of the dashboards: one period, some sources, a comparison with the period before, and how often to
 * refresh. They scope every panel at once, so all the numbers on the page agree. They live in the address
 * (?range=24h&sources=…), so a view can be shared as a link.
 */
import type { SourceSet, TimeRange } from '../data/model';
import { DAY, HOUR, MINUTE } from '../lib/time';

/** The periods offered, ending now. */
export const presets = [
  { value: '15m', label: '15 min', ms: 15 * MINUTE },
  { value: '1h', label: '1 h', ms: HOUR },
  { value: '6h', label: '6 h', ms: 6 * HOUR },
  { value: '24h', label: '24 h', ms: DAY },
  { value: '7d', label: '7 d', ms: 7 * DAY },
  { value: '30d', label: '30 d', ms: 30 * DAY },
] as const;

/** A period ending now. */
export type PresetId = (typeof presets)[number]['value'];

/** The refresh intervals offered (null: never on its own). */
export const refreshes = [
  { value: 'off', label: 'Off', ms: null },
  { value: '10s', label: '10 s', ms: 10_000 },
  { value: '30s', label: '30 s', ms: 30_000 },
  { value: '1m', label: '1 min', ms: 60_000 },
  { value: '5m', label: '5 min', ms: 5 * MINUTE },
] as const;

/** A refresh interval. */
export type RefreshId = (typeof refreshes)[number]['value'];

/** The filters. */
export interface MetricsFilter {
  /** A period ending now, or null for the custom period. */
  preset: PresetId | null;
  /** The custom period, when there is no preset. */
  custom: TimeRange | null;
  /** The sources shown: null for all. */
  sources: SourceSet;
  /** Compare every figure with the period just before. */
  compare: boolean;
  refresh: RefreshId;
}

/** The filters before anyone chooses: the last 24 hours, every source, compared, refreshed every 30 seconds. */
export const defaultFilter: MetricsFilter = {
  preset: '24h',
  custom: null,
  sources: null,
  compare: true,
  refresh: '30s',
};

/** The filters written in an address. */
export function readFilter(search: URLSearchParams): MetricsFilter {
  const filter: MetricsFilter = { ...defaultFilter };
  const range = search.get('range');
  if (presets.some((preset) => preset.value === range)) {
    filter.preset = range as PresetId;
  }
  const from = Date.parse(search.get('from') ?? '');
  const to = Date.parse(search.get('to') ?? '');
  if (Number.isFinite(from) && Number.isFinite(to) && from < to) {
    filter.preset = null;
    filter.custom = { from, to };
  }
  const sources = search.get('sources');
  if (sources) {
    filter.sources = sources.split(',').filter(Boolean);
  }
  if (search.get('compare') === 'off') {
    filter.compare = false;
  }
  const refresh = search.get('refresh');
  if (refreshes.some((item) => item.value === refresh)) {
    filter.refresh = refresh as RefreshId;
  }
  return filter;
}

/** Writes the filters into an address, leaving out what is the default. */
export function writeFilter(filter: MetricsFilter, url: URL): void {
  for (const key of ['range', 'from', 'to', 'sources', 'compare', 'refresh']) {
    url.searchParams.delete(key);
  }
  if (filter.preset && filter.preset !== defaultFilter.preset) {
    url.searchParams.set('range', filter.preset);
  }
  if (!filter.preset && filter.custom) {
    url.searchParams.set('from', new Date(filter.custom.from).toISOString());
    url.searchParams.set('to', new Date(filter.custom.to).toISOString());
  }
  if (filter.sources) {
    url.searchParams.set('sources', filter.sources.join(','));
  }
  if (!filter.compare) {
    url.searchParams.set('compare', 'off');
  }
  if (filter.refresh !== defaultFilter.refresh) {
    url.searchParams.set('refresh', filter.refresh);
  }
}

/** The period the filters choose, given the moment "now" means. */
export function rangeOf(filter: MetricsFilter, now: number): TimeRange {
  if (!filter.preset && filter.custom) {
    return filter.custom;
  }
  const preset = presets.find((item) => item.value === filter.preset) ?? presets[3];
  return { from: now - preset.ms, to: now };
}

/** The period of the same length just before. */
export function previousRange(range: TimeRange): TimeRange {
  const length = range.to - range.from;
  return { from: range.from - length, to: range.from };
}

/** The refresh interval of the filters, in milliseconds (null: off, or a custom period, which does not move). */
export function refreshMsOf(filter: MetricsFilter): number | null {
  if (!filter.preset) {
    return null;
  }
  return refreshes.find((item) => item.value === filter.refresh)?.ms ?? null;
}
