/**
 * Conclusions: what the engine said (EMERGENCY.md §12, "Conclusions in the read routes"). Each type has its own
 * glyph and colour, the same on the map, in the lists, in the key, in the panel and in the graph, so a conclusion is
 * never mistaken for an event (a round light). Its numbers are its search document's measures (each number of the
 * statement, and confidence) or its record's statement.value. Pure.
 */
import type { Item } from '../api/model';
import { formatNumber, formatTime, words } from '../object/describe';

/** A glyph: an SVG path in a 20 × 20 box, centred on (10, 10). */
export type Shape =
  'star' | 'diamond' | 'triangle' | 'hourglass' | 'square' | 'pentagon' | 'cross' | 'octagon';

export const glyphs: Record<Shape, string> = {
  star: 'M10 1 12.4 7.6 19 10 12.4 12.4 10 19 7.6 12.4 1 10 7.6 7.6Z',
  diamond: 'M10 1.5 18.5 10 10 18.5 1.5 10Z',
  triangle: 'M10 2 18.5 17 1.5 17Z',
  hourglass: 'M3 2.5H17L10 10 17 17.5H3L10 10Z',
  square: 'M3.5 3.5H16.5V16.5H3.5Z',
  pentagon: 'M10 1.5 18.5 7.7 15.3 17.8H4.7L1.5 7.7Z',
  cross: 'M7.5 2H12.5V7.5H18V12.5H12.5V18H7.5V12.5H2V7.5H7.5Z',
  octagon: 'M6.5 2H13.5L18 6.5V13.5L13.5 18H6.5L2 13.5V6.5Z',
};

export interface ConclusionType {
  type: string;
  name: string;
  /** What it says, in a few words (the key and the panel). */
  says: string;
  color: string;
  shape: Shape;
}

export const conclusionTypes: ConclusionType[] = [
  {
    type: 'spark',
    name: 'Spark',
    says: 'far above what was expected',
    color: '#c8ff6e',
    shape: 'star',
  },
  {
    type: 'forecast',
    name: 'Forecast',
    says: 'what to expect next',
    color: '#5ef2c8',
    shape: 'diamond',
  },
  {
    type: 'joint_anomaly',
    name: 'Joint anomaly',
    says: 'several measures off at once',
    color: '#ff6ec7',
    shape: 'triangle',
  },
  {
    type: 'similar_pattern',
    name: 'Similar pattern',
    says: 'what followed similar pasts',
    color: '#7dffa0',
    shape: 'hourglass',
  },
  {
    type: 'calibrated_estimate',
    name: 'Calibrated estimate',
    says: 'a poll corrected for its bias',
    color: '#ffcf6e',
    shape: 'square',
  },
  {
    type: 'forecast_score',
    name: 'Forecast score',
    says: 'how well the forecasts did',
    color: '#b9b3ff',
    shape: 'pentagon',
  },
  {
    type: 'classification',
    name: 'Classification',
    says: 'filed in a class',
    color: '#f2f4ff',
    shape: 'cross',
  },
];

/** A conclusion type not listed above. */
const otherType: Omit<ConclusionType, 'type'> = {
  name: 'Conclusion',
  says: 'what the engine said',
  color: '#c8ff6e',
  shape: 'octagon',
};

/** The style of a conclusion type (unknown types keep their code as a name, with the octagon). */
export function conclusionTypeOf(type: string): ConclusionType {
  return (
    conclusionTypes.find((entry) => entry.type === type) ?? {
      ...otherType,
      type,
      name: type ? words(type) : otherType.name,
    }
  );
}

/** "Conclusion · Forecast" */
export function badgeOf(type: string): string {
  return `Conclusion · ${conclusionTypeOf(type).name}`;
}

/** A conclusion's numbers by name: from a search document's measures (lists) or a statement's value (an object). */
export type Numbers = Record<string, number>;

export function numbersOfItem(item: Item): Numbers {
  const numbers: Numbers = {};
  for (const measure of item.measures) {
    if (!(measure.name in numbers)) numbers[measure.name] = measure.value;
  }
  return numbers;
}

/** statement.value: an object of named numbers, or a bare number (as `value`). */
export function numbersOfValue(value: unknown): Numbers {
  if (typeof value === 'number') return { value };
  if (!value || typeof value !== 'object') return {};
  const numbers: Numbers = {};
  for (const [name, entry] of Object.entries(value)) {
    const number = Array.isArray(entry) ? entry[0] : entry;
    if (typeof number === 'number' && Number.isFinite(number)) numbers[name] = number;
  }
  return numbers;
}

/** A number for a short line: at most 3 significant digits after the point. */
export function short(value: number): string {
  if (Number.isInteger(value)) return value.toLocaleString('en');
  const digits = Math.abs(value) >= 100 ? 0 : Math.abs(value) >= 1 ? 2 : 3;
  return Number(value.toFixed(digits)).toLocaleString('en', { maximumFractionDigits: digits });
}

/** A share 0..1 as a percent: 0.312 → "31.2%". */
export function percent(value: number): string {
  return `${Number((value * 100).toFixed(1))}%`;
}

/** The key numbers of a conclusion, as short phrases for a list row or a hover label. */
export function keyNumbers(type: string, n: Numbers): string[] {
  const has = (...names: string[]) => names.every((name) => n[name] !== undefined);
  const band = has('lower80', 'upper80') ? ` (80%: ${short(n.lower80!)}–${short(n.upper80!)})` : '';
  const rank = has('rank', 'of') ? `rank ${short(n.rank!)} of ${short(n.of!)}` : undefined;
  const out: (string | undefined)[] = [];
  switch (type) {
    case 'spark':
      if (has('observed', 'expected'))
        out.push(`observed ${short(n.observed!)} vs expected ${short(n.expected!)}`);
      if (has('value', 'change')) out.push(`value ${short(n.value!)}, change ${short(n.change!)}`);
      if (has('s')) out.push(`s ${short(n.s!)}`);
      if (has('z')) out.push(`z ${short(n.z!)}`);
      break;
    case 'forecast':
      if (has('expected')) out.push(`expected ${short(n.expected!)}${band}`);
      out.push(rank);
      break;
    case 'joint_anomaly':
      out.push(rank);
      if (has('z')) out.push(`z ${short(n.z!)}`);
      break;
    case 'similar_pattern':
      if (has('expected_total')) out.push(`expected ${short(n.expected_total!)}${band}`);
      if (has('k')) out.push(`${short(n.k!)} similar pasts`);
      break;
    case 'calibrated_estimate':
      if (has('poll', 'corrected')) out.push(`poll ${short(n.poll!)} → ${short(n.corrected!)}`);
      if (has('probability')) out.push(`p ${percent(n.probability!)}`);
      break;
    case 'forecast_score':
      for (const pair of scorePairs(n))
        out.push(`${pair.label} ${short(pair.model)} vs ${short(pair.routine)}`);
      if (has('auc') && !has('auc_model')) out.push(`AUC ${short(n.auc!)}`);
      if (has('failed_in_top50', 'failed'))
        out.push(`${short(n.failed_in_top50!)} of ${short(n.failed!)} in the top 50`);
      break;
    default:
      break;
  }
  if (out.filter(Boolean).length === 0 && n.confidence !== undefined)
    out.push(`confidence ${short(n.confidence)}`);
  return out.filter((phrase): phrase is string => Boolean(phrase));
}

/** A forecast's parts: routine + self + neighbours = expected, and its 80% band. */
export interface ForecastParts {
  routine: number;
  self: number;
  neighbours: number;
  expected: number;
  lower80?: number;
  upper80?: number;
}

export function forecastPartsOf(n: Numbers): ForecastParts | null {
  if (n.expected === undefined || n.routine === undefined) return null;
  return {
    routine: n.routine,
    self: n.self ?? 0,
    neighbours: n.neighbours ?? 0,
    expected: n.expected,
    lower80: n.lower80,
    upper80: n.upper80,
  };
}

/** A forecast score's comparisons of the model with the routine (the plain repeat of the usual). */
export interface ScorePair {
  label: string;
  model: number;
  routine: number;
  /** Shares (0..1) are drawn on one scale; counts against `of`. */
  of?: number;
}

const scoreNames: [string, string][] = [
  ['captured', 'captured'],
  ['auc', 'AUC'],
  ['top1', 'top-1'],
  ['top5', 'top-5'],
  ['brier', 'Brier'],
  ['mae', 'MAE'],
];

export function scorePairs(n: Numbers): ScorePair[] {
  return scoreNames.flatMap(([name, label]) => {
    const model = n[`${name}_model`];
    const routine = n[`${name}_routine`];
    if (model === undefined || routine === undefined) return [];
    return [{ label, model, routine, of: name === 'captured' ? n.events : undefined }];
  });
}

/** "Said on 2 Oct 2026 about 1–7 Sep 2026": when the conclusion was said, and the period it speaks of. */
export function saidAbout(
  knownAt: string,
  period?: { start?: string; end?: string; precision?: string },
): { said: string; about?: string; before?: string } {
  const said = formatTime(knownAt, 'minute');
  if (!period?.start) return { said };
  const precision = period.precision ?? 'day';
  const start = formatTime(period.start, precision);
  // The end is excluded (the next month's first moment): the period's last moment is just before it.
  const endMs = period.end ? Date.parse(period.end) - 1 : Number.NaN;
  const end = Number.isNaN(endMs)
    ? undefined
    : formatTime(new Date(endMs).toISOString(), precision);
  const about = end && end !== start ? `${start} → ${end}` : start;
  // Said before the period began (a forecast) or ended: the proof that it was said in advance.
  const knownMs = Date.parse(knownAt);
  const startMs = Date.parse(period.start);
  const dayMs = 86_400_000;
  let before: string | undefined;
  if (!Number.isNaN(knownMs) && !Number.isNaN(startMs)) {
    const days = (startMs - knownMs) / dayMs;
    if (days > 0)
      before = `said ${days >= 1 ? `${formatNumber(Math.round(days))} days` : 'hours'} before the period began`;
    else if (days <= 0 && Math.abs(days) < 0.0001) before = 'said as the period began';
    else before = 'said after the period began';
  }
  return { said, about, before };
}
