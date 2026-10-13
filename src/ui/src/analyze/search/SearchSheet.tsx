/**
 * The search window: every parameter GET /search takes (the words or a meaning, kind, sources, types, from, to, a
 * logical place, an entity, how many), starting from the view's filter. The results come as the API ranks them, "N of
 * M"; they can be put in another order (only the ones received). A click on a result goes to it: on the map when it
 * has a place, else in the logical tree when it is filed there; its details and its graph are always one click away.
 */
import { useState } from 'react';
import { fetchSearch } from '../api/client';
import type { WireCatalogEntry } from '../api/contract';
import { ApiError } from '../api/http';
import type { Item, RecordKind } from '../api/model';
import { useLatest } from '../lib/useLatest';
import { formatTime, headingOfItem, sourceName, typeWords, words } from '../object/describe';
import type { ViewFilter } from '../filter/viewFilter';
import { fromDayInput } from '../time/window';
import styles from './SearchSheet.module.css';

interface SearchSheetProps {
  /** The view's filter, which the form starts from. */
  start: ViewFilter;
  catalog: WireCatalogEntry[] | undefined;
  onResult: (item: Item) => void;
  onClose: () => void;
}

interface Form {
  q: string;
  semantic: boolean;
  kind: RecordKind | '';
  sources: string[];
  types: string[];
  from: string;
  to: string;
  logical: string;
  entity: string;
  limit: number;
}

type Order = 'api' | 'newest' | 'oldest' | 'name';

const limits = [50, 100, 200, 500];

export function SearchSheet({ start, catalog, onResult, onClose }: SearchSheetProps) {
  const [form, setForm] = useState<Form>(() => ({
    q: '',
    semantic: false,
    kind: start.kind ?? '',
    sources: start.sources,
    types: start.types,
    from: '',
    to: '',
    logical: start.logical ?? '',
    entity: start.entity ?? '',
    limit: 50,
  }));
  const [asked, setAsked] = useState<Form | null>(null);
  const [order, setOrder] = useState<Order>('api');
  const set = (part: Partial<Form>) => setForm((current) => ({ ...current, ...part }));

  const results = useLatest(asked ? JSON.stringify(asked) : null, (signal) =>
    fetchSearch(
      {
        from: fromDayInput(asked!.from) ?? undefined,
        to: fromDayInput(asked!.to) ?? undefined,
        sources: asked!.sources,
        types: asked!.types,
      },
      {
        q: asked!.q,
        semantic: asked!.semantic && asked!.q.trim() !== '',
        kind: asked!.kind || undefined,
        logical: asked!.logical.trim() || undefined,
        entity: asked!.entity.trim() || undefined,
        limit: asked!.limit,
      },
      signal,
    ),
  );
  const answer = results.pending || results.error ? undefined : results.answer;
  const items = answer ? ordered(answer.items, order) : [];
  const ranking = !asked?.q.trim()
    ? 'the newest first'
    : asked.semantic
      ? 'the nearest in meaning first'
      : 'the best match first';

  return (
    <>
      <div className={styles.backdrop} onClick={onClose} aria-hidden="true" />
      <section className={styles.sheet} aria-label="Search">
        <form
          className={styles.form}
          onSubmit={(event) => {
            event.preventDefault();
            setAsked(form);
          }}
        >
          <div className={styles.query}>
            <svg viewBox="0 0 20 20" aria-hidden="true">
              <circle cx="8.5" cy="8.5" r="5" />
              <path d="m12.5 12.5 4 4" />
            </svg>
            <input
              // eslint-disable-next-line jsx-a11y/no-autofocus -- the window opens to be typed in
              autoFocus
              value={form.q}
              placeholder="Words, a name, an id… or nothing for the filtered list"
              aria-label="Search for"
              onChange={(event) => set({ q: event.target.value })}
            />
            <div className={styles.mode}>
              {[false, true].map((semantic) => (
                <button
                  key={String(semantic)}
                  type="button"
                  aria-pressed={form.semantic === semantic}
                  title={semantic ? 'By meaning (needs words)' : 'Each word as the start of a word'}
                  onClick={() => set({ semantic })}
                >
                  {semantic ? 'Meaning' : 'Words'}
                </button>
              ))}
            </div>
            <button type="submit" className={styles.go}>
              Search
            </button>
          </div>

          <div className={styles.params}>
            <label>
              <span>Kind</span>
              <select
                value={form.kind}
                onChange={(event) => set({ kind: event.target.value as RecordKind | '' })}
              >
                <option value="">Any</option>
                <option value="event">Events</option>
                <option value="entity">Entities</option>
                <option value="conclusion">Conclusions</option>
              </select>
            </label>
            <label>
              <span>From</span>
              <input
                type="date"
                value={form.from}
                onChange={(event) => set({ from: event.target.value })}
              />
            </label>
            <label>
              <span>To (excluded)</span>
              <input
                type="date"
                value={form.to}
                onChange={(event) => set({ to: event.target.value })}
              />
            </label>
            <label>
              <span>How many</span>
              <select
                value={form.limit}
                onChange={(event) => set({ limit: Number(event.target.value) })}
              >
                {limits.map((limit) => (
                  <option key={limit} value={limit}>
                    {limit}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>Source</span>
              <select
                value=""
                onChange={(event) => set({ sources: [...form.sources, event.target.value] })}
              >
                <option value="">Add…</option>
                {(catalog ?? [])
                  .filter(
                    (entry) => entry.field === 'source' && !form.sources.includes(entry.value),
                  )
                  .map((entry) => (
                    <option key={entry.value} value={entry.value}>
                      {sourceName(entry.value)}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              <span>Type</span>
              <select
                value=""
                onChange={(event) => set({ types: [...form.types, event.target.value] })}
              >
                <option value="">Add…</option>
                {(catalog ?? [])
                  .filter(
                    (entry) => entry.field.endsWith('_type') && !form.types.includes(entry.value),
                  )
                  .map((entry) => (
                    <option key={`${entry.field}:${entry.value}`} value={entry.value}>
                      {entry.value}
                    </option>
                  ))}
              </select>
            </label>
            <label className={styles.wide}>
              <span>Logical place</span>
              <input
                value={form.logical}
                placeholder="cyber.vendor_product/Apache"
                onChange={(event) => set({ logical: event.target.value })}
              />
            </label>
            <label className={styles.wide}>
              <span>Entity (its events)</span>
              <input
                value={form.entity}
                placeholder="ent:kev-product:apache/log4j2"
                onChange={(event) => set({ entity: event.target.value })}
              />
            </label>
          </div>

          {(form.sources.length > 0 || form.types.length > 0) && (
            <ul className={styles.chips}>
              {form.sources.map((source) => (
                <li key={`s:${source}`}>
                  <button
                    type="button"
                    onClick={() => set({ sources: form.sources.filter((s) => s !== source) })}
                  >
                    <code>source</code>
                    {sourceName(source)} ×
                  </button>
                </li>
              ))}
              {form.types.map((type) => (
                <li key={`t:${type}`}>
                  <button
                    type="button"
                    onClick={() => set({ types: form.types.filter((t) => t !== type) })}
                  >
                    <code>type</code>
                    {type} ×
                  </button>
                </li>
              ))}
            </ul>
          )}
        </form>

        <div className={styles.results}>
          {!asked && (
            <p className={styles.quiet}>
              The form starts from the view’s filters. Enter searches; Esc closes.
            </p>
          )}
          {results.pending && <p className={styles.quiet}>Searching…</p>}
          {results.error && (
            <p className={styles.failed}>
              {results.error instanceof ApiError && results.error.status === 501
                ? 'Not in the fixtures: the development mode has no saved answer for this search.'
                : results.error.message}
            </p>
          )}
          {answer && (
            <header className={styles.count}>
              <span>
                <b>{answer.items.length.toLocaleString('en')}</b> of{' '}
                {answer.total.toLocaleString('en')} {answer.total === 1 ? 'record' : 'records'},{' '}
                {order === 'api' ? ranking : `${answer.items.length} received, re-ordered`}
              </span>
              <select
                aria-label="Order"
                value={order}
                onChange={(event) => setOrder(event.target.value as Order)}
              >
                <option value="api">As the API ranks</option>
                <option value="newest">Newest first</option>
                <option value="oldest">Oldest first</option>
                <option value="name">By name</option>
              </select>
            </header>
          )}
          {answer && answer.items.length === 0 && <p className={styles.quiet}>Nothing matches.</p>}
          {items.map((item) => (
            <Result key={item.id} item={item} onClick={() => onResult(item)} />
          ))}
        </div>
      </section>
    </>
  );
}

function Result({ item, onClick }: { item: Item; onClick: () => void }) {
  const heading = headingOfItem(item);
  const goes = item.place?.point ? 'map' : item.logical.length > 0 ? 'tree' : 'details';
  return (
    <button type="button" className={styles.result} onClick={onClick}>
      <i style={{ background: heading.color, boxShadow: `0 0 8px ${heading.color}` }} />
      <span className={styles.label}>{heading.title}</span>
      <span className={styles.detail}>
        {[
          words(item.kind),
          typeWords(item.type),
          item.time?.start ? formatTime(item.time.start, item.time.precision) : undefined,
          item.sources[0] ? sourceName(item.sources[0]) : undefined,
        ]
          .filter(Boolean)
          .join(' · ')}
      </span>
      <span className={styles.goes} data-goes={goes}>
        {goes === 'map' ? 'On the map' : goes === 'tree' ? 'In the tree' : 'Details'}
      </span>
    </button>
  );
}

/** The received results in another order (never more than were received). */
function ordered(items: Item[], order: Order): Item[] {
  if (order === 'api') return items;
  const time = (item: Item) => (item.time?.start ? Date.parse(item.time.start) : Number.NaN);
  const sorted = [...items];
  if (order === 'name') {
    sorted.sort((a, b) => headingOfItem(a).title.localeCompare(headingOfItem(b).title));
  } else {
    const sign = order === 'newest' ? -1 : 1;
    sorted.sort((a, b) => {
      const [ta, tb] = [time(a), time(b)];
      if (Number.isNaN(ta)) return Number.isNaN(tb) ? 0 : 1;
      if (Number.isNaN(tb)) return -1;
      return sign * (ta - tb);
    });
  }
  return sorted;
}
