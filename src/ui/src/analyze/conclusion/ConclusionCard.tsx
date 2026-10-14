/**
 * A conclusion, read at a glance (the top of the object panel): what kind it is, what it says in words and numbers,
 * when it was said and about what period (the proof it was said before), the records it rests on (each opens), and
 * the method with its parameters. A forecast shows its parts (routine + self + neighbours = expected) and its 80%
 * band; a forecast score shows the model against the routine.
 */
import { useState, type CSSProperties } from 'react';
import type { SilRecord } from '../api/contract';
import { formatNumber, kindOfId, words } from '../object/describe';
import { valueOf } from '../object/record';
import type { Focus } from '../object/useObject';
import {
  conclusionTypeOf,
  forecastPartsOf,
  numbersOfValue,
  percent,
  saidAbout,
  scorePairs,
  short,
  type ForecastParts,
  type Numbers,
  type ScorePair,
} from './conclusion';
import { Glyph } from './Glyph';
import styles from './Conclusion.module.css';

/** The premises listed before "All". */
const firstPremises = 6;

export function ConclusionCard({
  record,
  knownAt,
  onOpen,
}: {
  record: SilRecord;
  /** When it was said: the record's known_at, else when the API received it. */
  knownAt: string;
  onOpen: (focus: Focus) => void;
}) {
  const type = record.type ?? '';
  const style = conclusionTypeOf(type);
  const numbers = numbersOfValue(record.statement?.value);
  if (record.confidence !== undefined && numbers.confidence === undefined) {
    numbers.confidence = record.confidence;
  }
  const attributes = record.attributes ?? {};
  const text = (key: string) =>
    typeof attributes[key] === 'string' ? (attributes[key] as string) : undefined;
  const said = saidAbout(record.known_at ?? knownAt, {
    start: text('period_start') ?? record.time?.start,
    end: text('period_end') ?? record.time?.end,
    precision: text('period_precision') ?? record.time?.precision,
  });
  const parts = type === 'forecast' ? forecastPartsOf(numbers) : null;
  const band =
    !parts && numbers.lower80 !== undefined && numbers.upper80 !== undefined
      ? {
          value: numbers.expected_total ?? numbers.expected,
          lower: numbers.lower80,
          upper: numbers.upper80,
        }
      : null;
  const pairs = type === 'forecast_score' ? scorePairs(numbers) : [];
  const params = attributes.params;
  const premises = record.premises ?? [];

  return (
    <section className={styles.card} style={{ '--type': style.color } as CSSProperties}>
      <p className={styles.badge}>
        <Glyph shape={style.shape} color={style.color} size={16} />
        <b>Conclusion</b>
        <span>·</span>
        {style.name}
        <small>{style.says}</small>
      </p>

      {record.statement?.predicate && (
        <p className={styles.predicate}>
          {words(record.statement.predicate)}
          {record.statement.object && <code>{record.statement.object}</code>}
        </p>
      )}
      <NumberGrid numbers={numbers} />

      {parts && <ForecastBar parts={parts} />}
      {band && band.value !== undefined && (
        <Band value={band.value} lower={band.lower} upper={band.upper} />
      )}
      {pairs.length > 0 && <ModelAgainstRoutine pairs={pairs} />}
      {type === 'calibrated_estimate' &&
        numbers.poll !== undefined &&
        numbers.corrected !== undefined && (
          <p className={styles.sum}>
            poll {short(numbers.poll)}
            {numbers.bias !== undefined && ` − bias ${short(numbers.bias)}`} = corrected{' '}
            {short(numbers.corrected)}
            {numbers.probability !== undefined &&
              ` · win probability ${percent(numbers.probability)}`}
          </p>
        )}

      <p className={styles.said}>
        <span>Said on</span> <b>{said.said}</b>
        {said.about && (
          <>
            {' '}
            <span>about</span> <b>{said.about}</b>
          </>
        )}
        {said.before && <em>{said.before}</em>}
      </p>

      {premises.length > 0 && <Premises ids={premises} onOpen={onOpen} />}

      {(record.method?.name || params !== undefined) && (
        <div className={styles.method}>
          <h4>
            Method
            {record.method?.name && (
              <span>
                {record.method.name}
                {record.method.version && ` v${record.method.version}`}
                {record.method.by && <code>{record.method.by}</code>}
              </span>
            )}
          </h4>
          {params !== undefined && <Params params={params} />}
        </div>
      )}
    </section>
  );
}

/** The engine's measure names, in words (others are spelled out as they come). */
const measureNames: Record<string, string> = {
  expected: 'Expected',
  routine: 'Routine (usual)',
  self: 'Own recent activity',
  neighbours: 'Neighbours',
  lower80: '80% band, low',
  upper80: '80% band, high',
  rank: 'Rank',
  of: 'Out of',
  confidence: 'Confidence / 100',
};

function NumberGrid({ numbers }: { numbers: Numbers }) {
  const entries = Object.entries(numbers);
  if (entries.length === 0) return null;
  return (
    <ul className={styles.numbers}>
      {entries.map(([name, value]) => (
        <li key={name} title={`${name}: ${formatNumber(value)}`}>
          <span>{measureNames[name] ?? words(name)}</span>
          <b>{short(value)}</b>
        </li>
      ))}
    </ul>
  );
}

/** routine + self + neighbours = expected, as one stacked bar, with the 80% band under it on the same scale. */
function ForecastBar({ parts }: { parts: ForecastParts }) {
  const pieces = [
    { name: 'routine', value: parts.routine },
    { name: 'self', value: parts.self },
    { name: 'neighbours', value: parts.neighbours },
  ];
  const scale = Math.max(
    parts.expected,
    parts.upper80 ?? 0,
    pieces.reduce((sum, piece) => sum + Math.max(0, piece.value), 0),
    1e-9,
  );
  return (
    <figure className={styles.forecast} aria-label="The forecast's parts">
      <div className={styles.stack}>
        {pieces.map((piece) => (
          <i
            key={piece.name}
            data-part={piece.name}
            style={{ width: `${(Math.max(0, piece.value) / scale) * 100}%` }}
            title={`${piece.name} ${formatNumber(piece.value)}`}
          />
        ))}
      </div>
      {parts.lower80 !== undefined && parts.upper80 !== undefined && (
        <BandLine
          value={parts.expected}
          lower={parts.lower80}
          upper={parts.upper80}
          scale={scale}
        />
      )}
      <figcaption>
        {pieces.map((piece, index) => (
          <span key={piece.name} data-part={piece.name}>
            {index > 0 && (piece.value < 0 ? ' − ' : ' + ')}
            <i />
            {piece.name} {short(Math.abs(piece.value))}
          </span>
        ))}
        <b> = expected {short(parts.expected)}</b>
        {parts.lower80 !== undefined && parts.upper80 !== undefined && (
          <span className={styles.bandWords}>
            80% between {short(parts.lower80)} and {short(parts.upper80)}
          </span>
        )}
      </figcaption>
    </figure>
  );
}

function Band({ value, lower, upper }: { value: number; lower: number; upper: number }) {
  const scale = Math.max(upper, value, 1e-9);
  return (
    <figure className={styles.forecast} aria-label="The 80% band">
      <BandLine value={value} lower={lower} upper={upper} scale={scale} />
      <figcaption>
        <b>expected {short(value)}</b>
        <span className={styles.bandWords}>
          80% between {short(lower)} and {short(upper)}
        </span>
      </figcaption>
    </figure>
  );
}

function BandLine({
  value,
  lower,
  upper,
  scale,
}: {
  value: number;
  lower: number;
  upper: number;
  scale: number;
}) {
  const at = (number: number) => `${(Math.max(0, Math.min(number, scale)) / scale) * 100}%`;
  return (
    <div className={styles.band} title={`80%: ${formatNumber(lower)} to ${formatNumber(upper)}`}>
      <i style={{ left: at(lower), width: `calc(${at(upper)} - ${at(lower)})` }} />
      <b style={{ left: at(value) }} />
    </div>
  );
}

/** Each measure of a forecast score: the model's bar over the routine's, on one scale. */
function ModelAgainstRoutine({ pairs }: { pairs: ScorePair[] }) {
  return (
    <div className={styles.score}>
      <h4>
        The model against the routine
        <span>
          <i data-who="model" /> model <i data-who="routine" /> routine
        </span>
      </h4>
      {pairs.map((pair) => {
        const scale =
          pair.of ??
          (Math.max(pair.model, pair.routine) <= 1 ? 1 : Math.max(pair.model, pair.routine));
        const width = (value: number) => `${(Math.max(0, value) / (scale || 1)) * 100}%`;
        const shown = (value: number) =>
          pair.of !== undefined ? `${short(value)} of ${short(pair.of)}` : short(value);
        return (
          <div key={pair.label} className={styles.pair}>
            <span>{pair.label}</span>
            <div>
              <i data-who="model" style={{ width: width(pair.model) }} />
              <i data-who="routine" style={{ width: width(pair.routine) }} />
            </div>
            <b>
              {shown(pair.model)}
              <small>{shown(pair.routine)}</small>
            </b>
          </div>
        );
      })}
    </div>
  );
}

function Premises({ ids, onOpen }: { ids: string[]; onOpen: (focus: Focus) => void }) {
  const [all, setAll] = useStateToggle();
  const shown = all ? ids : ids.slice(0, firstPremises);
  return (
    <div className={styles.premises}>
      <h4>
        Rests on <span>{ids.length.toLocaleString('en')}</span>
      </h4>
      <ul>
        {shown.map((id) => (
          <li key={id}>
            <button type="button" onClick={() => onOpen({ id })} title={`Open ${id}`}>
              <small>{words(kindOfId(id))}</small>
              <code>{id}</code>
            </button>
          </li>
        ))}
      </ul>
      {ids.length > firstPremises && (
        <button type="button" className={styles.more} onClick={setAll}>
          {all ? 'Fewer' : `All ${ids.length.toLocaleString('en')}`}
        </button>
      )}
    </div>
  );
}

function Params({ params }: { params: unknown }) {
  if (!params || typeof params !== 'object' || Array.isArray(params)) {
    return <p className={styles.params}>{valueOf(params)}</p>;
  }
  return (
    <dl className={styles.params}>
      {Object.entries(params).map(([name, value]) => (
        <div key={name}>
          <dt>{name}</dt>
          <dd>{valueOf(value)}</dd>
        </div>
      ))}
    </dl>
  );
}

function useStateToggle(): [boolean, () => void] {
  const [on, setOn] = useState(false);
  return [on, () => setOn((value) => !value)];
}
