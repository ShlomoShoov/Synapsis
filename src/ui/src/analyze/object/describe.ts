/**
 * Words for what the API sends as codes: a type, a source id, a country, a field name, a time at its precision, a
 * point. Display only: the codes themselves stay as they are, next to the words. Pure.
 */
import type { Item, LngLat, RecordKind } from '../api/model';
import type { SilRecord, WireGraphNode } from '../api/contract';
import { categoryOf } from '../map/palette';

const monthNames = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

/** The colour of a kind of record that is not an event (events take their category's colour). */
export const kindColors: Record<RecordKind, string> = {
  event: '#dce8ff',
  entity: '#62f0c0',
  conclusion: '#c8ff6e',
};

/** "deaths_best" → "Deaths best", "ArmedActor" → "Armed actor", "KNOWN_EXPLOITED" → "Known exploited". */
export function words(code: string): string {
  const spaced = code
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_\-.]+/g, ' ')
    .trim()
    .toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/** The last part of a type, in words: "SEC.CONFLICT.STATE_BASED" → "State based"; "Product" → "Product". */
export function typeWords(type: string): string {
  return words(type.split('.').at(-1) ?? type);
}

/** "src:ucdp:ged" → "UCDP GED". */
export function sourceName(source: string): string {
  const parts = source.split(':');
  return (parts[0] === 'src' ? parts.slice(1) : parts).join(' ').toUpperCase();
}

const regions = new Intl.DisplayNames(['en'], { type: 'region', fallback: 'code' });

/** "SD" → "Sudan" (the code itself when unknown). */
export function countryName(code: string): string {
  try {
    return regions.of(code) ?? code;
  } catch {
    return code;
  }
}

/** A time at its precision, in UTC: "1989", "Jan 1989", "1 Jan 1989", "30 Jan 1965, 04:37 UTC". */
export function formatTime(iso: string, precision = 'day'): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  const year = date.getUTCFullYear();
  const month = monthNames[date.getUTCMonth()]!;
  const day = `${date.getUTCDate()} ${month} ${year}`;
  if (precision === 'year') return String(year);
  if (precision === 'quarter') return `Q${Math.floor(date.getUTCMonth() / 3) + 1} ${year}`;
  if (precision === 'month') return `${month} ${year}`;
  if (precision === 'day' || precision === 'week') return day;
  const clock = [date.getUTCHours(), date.getUTCMinutes()]
    .map((part) => String(part).padStart(2, '0'))
    .join(':');
  const seconds = precision === 'second' ? `:${String(date.getUTCSeconds()).padStart(2, '0')}` : '';
  return `${day}, ${clock}${seconds} UTC`;
}

/** "5.00° N, 33.00° E" */
export function formatPoint([lon, lat]: LngLat): string {
  const ns = `${Math.abs(lat).toFixed(2)}° ${lat < 0 ? 'S' : 'N'}`;
  const ew = `${Math.abs(lon).toFixed(2)}° ${lon < 0 ? 'W' : 'E'}`;
  return `${ns}, ${ew}`;
}

/** A number as people read it: 2,700; 5.6; 0.000231. */
export function formatNumber(value: number): string {
  if (Number.isInteger(value)) return value.toLocaleString('en');
  return value.toLocaleString('en', { maximumSignificantDigits: 6 });
}

/** What the panel's head shows for a record: its kind, type, title, and the colour and name of its family. */
export interface Heading {
  kind: RecordKind;
  type: string;
  title: string;
  family: string;
  color: string;
}

/** The colour and family name of a record: an event's category, or its kind. */
export function familyOf(kind: RecordKind, type: string): { family: string; color: string } {
  if (kind === 'event') {
    const category = categoryOf(type);
    return { family: category.name, color: category.color };
  }
  const family = type ? typeWords(type) : kind === 'entity' ? 'Entity' : 'Conclusion';
  return { family, color: kindColors[kind] };
}

/** The head of a record as its source sent it. */
export function headingOfRecord(record: SilRecord): Heading {
  const kind = record.kind === 'relation' || !record.kind ? 'entity' : record.kind;
  const type = record.type ?? '';
  const where =
    record.place?.admin?.name ??
    (record.place?.admin?.country ? countryName(record.place.admin.country) : undefined) ??
    record.place?.logical?.path?.at(-1);
  const statement = record.statement?.predicate
    ? [words(record.statement.predicate), record.statement.object].filter(Boolean).join(': ')
    : undefined;
  const title =
    record.name?.trim() ||
    record.text?.title?.trim() ||
    statement ||
    [typeWords(type), where].filter(Boolean).join(' · ');
  return { kind, type, title, ...familyOf(kind, type) };
}

/** The head of a record from its map or search document, while its full record is on its way. */
export function headingOfItem(item: Item): Heading {
  const where = item.place?.country ? countryName(item.place.country) : undefined;
  const title =
    item.labelFrom === 'generated'
      ? [typeWords(item.type), where].filter(Boolean).join(' · ')
      : item.label;
  return { kind: item.kind, type: item.type, title, ...familyOf(item.kind, item.type) };
}

/** The kind of a graph node, from its labels (Entity, Event, Conclusion). */
export function kindOfNode(node: WireGraphNode): RecordKind {
  if (node.labels.includes('Event')) return 'event';
  if (node.labels.includes('Conclusion')) return 'conclusion';
  return 'entity';
}

/** What to call a graph node: its name, its title, its statement, or its type in words. */
export function labelOfNode(node: WireGraphNode): string {
  const text = (key: string) => {
    const value = node[key];
    return typeof value === 'string' && value.trim() ? value.trim() : undefined;
  };
  const predicate = text('predicate');
  return (
    text('name') ??
    text('title') ??
    (predicate ? words(predicate) : undefined) ??
    (text('type') ? typeWords(text('type')!) : node.id)
  );
}
