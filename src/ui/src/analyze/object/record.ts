/**
 * A record as its source sent it (GET /records/{id}), laid out for reading: the facts (when, where, who sent it, how
 * reliable), its text, its measures, its participants and the records it names, its attributes and its evidence.
 * Every value is the record's own; only the labels are words. Pure.
 */
import type { SilRecord } from '../api/contract';
import type { LngLat } from '../api/model';
import { countryName, formatNumber, formatPoint, formatTime, sourceName, words } from './describe';

/** One line of the facts: a label, the value in words, and the value as sent when it differs. */
export interface Fact {
  label: string;
  value: string;
  code?: string;
}

/** Records this one names: its participants, what a conclusion is about and rests on, a relation's two ends. */
export interface Named {
  id: string;
  /** The role, or how it is named (about, premise, from, to). */
  role: string;
}

export interface Attribute {
  label: string;
  value: string;
  /** The value is a web address. */
  link: boolean;
}

export interface RecordView {
  facts: Fact[];
  point?: LngLat;
  title?: string;
  body?: string;
  measures: { label: string; value: string; unit?: string }[];
  named: Named[];
  attributes: Attribute[];
  evidence?: { url?: string; ref?: string };
}

export function viewOf(record: SilRecord): RecordView {
  const facts: Fact[] = [];
  const time = record.time;
  if (time?.start) {
    const start = formatTime(time.start, time.precision);
    const end = time.end ? formatTime(time.end, time.precision) : undefined;
    facts.push({
      label: 'When',
      value: end && end !== start ? `${start} → ${end}` : start,
      code: time.precision,
    });
  }
  const place = record.place;
  const admin = place?.admin;
  if (admin) {
    const where = [admin.name, admin.country ? countryName(admin.country) : undefined]
      .filter(Boolean)
      .join(', ');
    facts.push({
      label: 'Where',
      value: where,
      code: [admin.country, admin.subdivision].filter(Boolean).join(' · '),
    });
  }
  if (place?.point) {
    facts.push({
      label: 'Point',
      value: formatPoint(place.point),
      code: place.uncertainty_km ? `± ${formatNumber(place.uncertainty_km)} km` : undefined,
    });
  }
  if (place?.logical?.path?.length) {
    facts.push({
      label: 'Filed under',
      value: place.logical.path.join(' / '),
      code: place.logical.space,
    });
  }
  if (place?.unplaced) facts.push({ label: 'Where', value: 'Not placed by its source' });
  if (record.statement?.predicate) {
    const { predicate, object, value } = record.statement;
    facts.push({
      label: 'States',
      value: [words(predicate), object, value === undefined ? undefined : valueOf(value)]
        .filter(Boolean)
        .join(' · '),
      code: predicate,
    });
  }
  if (record.confidence !== undefined) {
    facts.push({ label: 'Confidence', value: `${formatNumber(record.confidence)} / 100` });
  }
  if (record.method?.name) {
    const { by, name, version } = record.method;
    facts.push({
      label: 'Method',
      value: [name, version ? `v${version}` : undefined].filter(Boolean).join(' '),
      code: by,
    });
  }
  if (record.aliases?.length) facts.push({ label: 'Also named', value: record.aliases.join(', ') });
  if (record.source) {
    facts.push({ label: 'Source', value: sourceName(record.source), code: record.source });
  }
  if (record.known_at) {
    facts.push({
      label: 'Known',
      value: formatTime(record.known_at, 'day'),
      code: record.known_at_basis ? words(record.known_at_basis).toLowerCase() : undefined,
    });
  }
  if (record.reliability !== undefined) {
    facts.push({
      label: 'Reliability',
      value: `${formatNumber(record.reliability)} / 100`,
      code: record.reliability_basis,
    });
  }

  const named: Named[] = [
    ...(record.participants ?? []).map((participant) => ({
      id: participant.entity,
      role: words(participant.role),
    })),
    ...(record.about ?? []).map((id) => ({ id, role: 'About' })),
    ...(record.premises ?? []).map((id) => ({ id, role: 'Premise' })),
    ...(record.from ? [{ id: record.from, role: 'From' }] : []),
    ...(record.to ? [{ id: record.to, role: 'To' }] : []),
  ];

  return {
    facts,
    point: place?.point,
    title: record.text?.title?.trim() || undefined,
    body: record.text?.body?.trim() || undefined,
    measures: (record.measures ?? []).map((measure) => ({
      label: words(measure.name),
      value: formatNumber(measure.value),
      unit: measure.unit && measure.unit !== 'count' ? measure.unit : undefined,
    })),
    named,
    attributes: Object.entries(record.attributes ?? {}).map(([key, value]) => {
      const text = valueOf(value);
      return { label: words(key), value: text, link: /^https?:\/\/\S+$/.test(text) };
    }),
    evidence: record.evidence?.url || record.evidence?.ref ? record.evidence : undefined,
  };
}

/** Any value as text: numbers and lists as people read them, objects as compact JSON. */
export function valueOf(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'number') return formatNumber(value);
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'string') return value;
  if (Array.isArray(value) && value.every((entry) => typeof entry !== 'object')) {
    return value.map(valueOf).join(', ');
  }
  return JSON.stringify(value);
}
