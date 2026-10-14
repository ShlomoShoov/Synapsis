/**
 * The filter of the current view, on the left above the key: the kind of record, and sources and types chosen from the
 * catalog (GET /catalog), each a chip that a click removes. A click on a source, type, kind or date in a record's
 * details adds it here too. What does not match disappears from the view.
 */
import type { WireCatalogEntry } from '../api/contract';
import type { RecordKind } from '../api/model';
import { conclusionTypes } from '../conclusion/conclusion';
import { Glyph } from '../conclusion/Glyph';
import { PanelFrame } from '../lib/PanelFrame';
import { sourceName, words } from '../object/describe';
import { IndexedNote } from './IndexedNote';
import { chipsOf, noFilter, withValue, withoutValue, type ViewFilter } from './viewFilter';
import styles from './FilterCard.module.css';

interface FilterCardProps {
  filter: ViewFilter;
  onChange: (filter: ViewFilter) => void;
  catalog: WireCatalogEntry[] | undefined;
  /** Shown on the map, where only source, type, kind and the time card's window apply. */
  onMap: boolean;
  /** The conclusions' own layer on the map, over the events. */
  conclusionsOn: boolean;
  onConclusionsOn: (on: boolean) => void;
}

const kinds: { value: RecordKind | null; label: string }[] = [
  { value: null, label: 'Any' },
  { value: 'event', label: 'Events' },
  { value: 'entity', label: 'Entities' },
  { value: 'conclusion', label: 'Conclusions' },
];

const typeFields: [string, string][] = [
  ['event_type', 'Events'],
  ['entity_type', 'Entities'],
  ['conclusion_type', 'Conclusions'],
];

export function FilterCard({
  filter,
  onChange,
  catalog,
  onMap,
  conclusionsOn,
  onConclusionsOn,
}: FilterCardProps) {
  const chips = chipsOf(filter).filter((chip) => chip.field !== 'kind');
  const searchOnly = onMap && (filter.logical !== null || filter.entity !== null);
  return (
    <PanelFrame name="filters" label="the filters" width={290}>
      <section className={styles.card} aria-label="Filters">
        <header className={styles.head}>
          <span className={styles.kicker}>Filters</span>
          {chipsOf(filter).length > 0 && (
            <button type="button" className={styles.clear} onClick={() => onChange(noFilter)}>
              Clear
            </button>
          )}
        </header>

        <div className={styles.kinds}>
          {kinds.map((kind) => (
            <button
              key={kind.label}
              type="button"
              aria-pressed={filter.kind === kind.value}
              onClick={() => onChange({ ...filter, kind: kind.value })}
            >
              {kind.label}
            </button>
          ))}
        </div>

        {onMap && filter.kind === null && (
          <label className={styles.layer}>
            <input
              type="checkbox"
              checked={conclusionsOn}
              onChange={(event) => onConclusionsOn(event.target.checked)}
            />
            <span>Conclusions on the map</span>
            <span className={styles.glyphs} aria-hidden="true">
              {conclusionTypes.map((style) => (
                <Glyph key={style.type} shape={style.shape} color={style.color} size={10} />
              ))}
            </span>
          </label>
        )}
        <div className={styles.adders}>
          <select
            aria-label="Add a source"
            value=""
            onChange={(event) => onChange(withValue(filter, 'source', event.target.value))}
          >
            <option value="">+ Source</option>
            {(catalog ?? [])
              .filter((entry) => entry.field === 'source' && !filter.sources.includes(entry.value))
              .map((entry) => (
                <option key={entry.value} value={entry.value}>
                  {sourceName(entry.value)} ({entry.count.toLocaleString('en')})
                </option>
              ))}
          </select>
          <select
            aria-label="Add a type"
            value=""
            onChange={(event) => onChange(withValue(filter, 'type', event.target.value))}
          >
            <option value="">+ Type</option>
            {typeFields.map(([field, label]) => (
              <optgroup key={field} label={label}>
                {(catalog ?? [])
                  .filter((entry) => entry.field === field && !filter.types.includes(entry.value))
                  .map((entry) => (
                    <option key={entry.value} value={entry.value}>
                      {entry.value} ({entry.count.toLocaleString('en')})
                    </option>
                  ))}
              </optgroup>
            ))}
          </select>
        </div>

        {chips.length > 0 && (
          <ul className={styles.chips}>
            {chips.map((chip) => (
              <li key={`${chip.field}:${chip.value}`}>
                <button
                  type="button"
                  title="Remove this filter"
                  data-dim={
                    (onMap && (chip.field === 'logical' || chip.field === 'entity')) || undefined
                  }
                  onClick={() => onChange(withoutValue(filter, chip.field, chip.value))}
                >
                  <code>{words(chip.field)}</code>
                  {chip.field === 'source' ? sourceName(chip.value) : chip.value}
                  <svg viewBox="0 0 20 20" aria-hidden="true">
                    <path d="m6.5 6.5 7 7M13.5 6.5l-7 7" />
                  </svg>
                </button>
              </li>
            ))}
          </ul>
        )}

        <IndexedNote sources={filter.sources} catalog={catalog} />

        <p className={styles.hint}>
          {searchOnly
            ? 'The map keeps source, type, kind and the time card’s window; logical place and entity apply to search.'
            : 'A click on a source, type, kind or date in a record’s details adds it here.'}
        </p>
      </section>
    </PanelFrame>
  );
}
