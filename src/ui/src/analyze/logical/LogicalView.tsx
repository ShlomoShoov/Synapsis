/**
 * The logical dimension: every logical place records are filed under (a vulnerability under
 * cyber.vendor_product / Apache / Log4j2), as a tree that opens one level per click. GET /logical gives the top level,
 * GET /logical?node=<key> the children of a node as it opens; the records filed at or below the chosen node are
 * GET /search?logical=<key>, beside the tree, "N of M".
 *
 * Opened with an object, the tree opens along the object's own path and chooses its place, so the same object is
 * followed from the map or the graph into the tree; a click on a record opens it in the object panel.
 */
import { useEffect, useRef, useState } from 'react';
import { fetchLogical, fetchSearch } from '../api/client';
import type { WireBucket, WireLogical } from '../api/contract';
import { ApiError } from '../api/http';
import type { Item } from '../api/model';
import type { Filters } from '../api/query';
import { useLatest } from '../lib/useLatest';
import { formatTime, headingOfItem, sourceName, typeWords, words } from '../object/describe';
import type { Focus } from '../object/useObject';
import styles from './LogicalView.module.css';

interface LogicalViewProps {
  /** The open object: its id, its name, and its logical place (null: it has none; undefined: not known yet). */
  object: { id: string; title: string; place: string | null | undefined } | null;
  onOpen: (focus: Focus) => void;
  /** The view's sources and types, sent with every question (a new filter makes a new tree). */
  filters: Filters;
}

type Level = { wire: WireLogical } | { error: Error };

/** The records listed first, then on request up to the API's most. */
const firstRecords = 50;
const mostRecords = 500;

export function LogicalView({ object, onOpen, filters }: LogicalViewProps) {
  const [levels, setLevels] = useState<Map<string, Level>>(() => new Map());
  const [open, setOpen] = useState<Set<string>>(() => new Set(['']));
  const [chosen, setChosen] = useState<string | null>(null);
  const [limit, setLimit] = useState(firstRecords);

  // A new object: the tree opens along its path and chooses its place.
  const [followed, setFollowed] = useState<string | null>(null);
  const place = object?.place ?? null;
  if (place !== followed) {
    setFollowed(place);
    if (place) {
      setOpen((keys) => new Set([...keys, '', ...prefixesOf(place)]));
      setChosen(place);
      setLimit(firstRecords);
    }
  }

  // Each open level is read once (the top level is the key '').
  const asked = useRef(new Set<string>());
  useEffect(() => {
    for (const key of open) {
      if (asked.current.has(key)) continue;
      asked.current.add(key);
      fetchLogical(filters, key || undefined).then(
        (wire) => setLevels((all) => new Map(all).set(key, { wire })),
        (error: unknown) =>
          setLevels((all) =>
            new Map(all).set(key, {
              error: error instanceof Error ? error : new Error(String(error)),
            }),
          ),
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- a new filter remakes the view (its key)
  }, [open]);

  const records = useLatest(chosen === null ? null : `${chosen}|${limit}`, (signal) =>
    fetchSearch(filters, { logical: chosen!, limit }, signal),
  );

  const choose = (key: string) => {
    if (key === chosen) {
      setOpen((keys) => toggled(keys, key));
      return;
    }
    setChosen(key);
    setLimit(firstRecords);
    setOpen((keys) => new Set([...keys, key]));
  };

  const top = levels.get('');
  const onPath = new Set(place ? prefixesOf(place) : []);
  const answer = records.pending || records.error ? undefined : records.answer;

  return (
    <div className={styles.stage}>
      <section className={styles.tree} aria-label="Logical places">
        <header className={styles.head}>
          <p className={styles.kicker}>Logical places</p>
          <h2 className={styles.title}>Where records are filed</h2>
          <p className={styles.sub}>
            {top && 'wire' in top
              ? `${top.wire.buckets.length.toLocaleString('en')} spaces, the most records first. A click opens one level.`
              : 'Reading the spaces…'}
          </p>
          {object && object.place === null && (
            <p className={styles.note}>
              <b>{object.title}</b> is not filed in a logical place.
            </p>
          )}
        </header>
        <ul className={styles.rows}>
          <Children
            parent=""
            depth={0}
            levels={levels}
            open={open}
            chosen={chosen}
            onPath={onPath}
            onChoose={choose}
          />
        </ul>
      </section>

      {chosen !== null && (
        <section className={styles.records} aria-label="Records filed here">
          <header className={styles.head}>
            <p className={styles.kicker}>Filed under</p>
            <nav className={styles.crumbs} aria-label="Path">
              {prefixesOf(chosen).map((key) => (
                <button
                  key={key}
                  type="button"
                  aria-current={key === chosen || undefined}
                  onClick={() => choose(key)}
                >
                  {nameOf(key)}
                </button>
              ))}
            </nav>
            <p className={styles.sub}>
              {answer
                ? `${answer.items.length.toLocaleString('en')} of ${answer.total.toLocaleString('en')} records, the newest first`
                : records.pending
                  ? 'Reading the records…'
                  : ''}
            </p>
          </header>
          <div className={styles.list}>
            {records.error && <Failure error={records.error} />}
            {answer?.items.map((item) => (
              <RecordRow
                key={item.id}
                item={item}
                current={item.id === object?.id}
                onOpen={() =>
                  onOpen({
                    id: item.id,
                    heading: headingOfItem(item),
                    point: item.place?.point,
                    logical: item.logical[0],
                  })
                }
              />
            ))}
            {answer && answer.total > answer.items.length && limit < mostRecords && (
              <button type="button" className={styles.more} onClick={() => setLimit(mostRecords)}>
                Show up to {Math.min(answer.total, mostRecords).toLocaleString('en')}
              </button>
            )}
          </div>
        </section>
      )}
    </div>
  );
}

/** The children of a node: one level of the tree, and the open ones' children below them. */
function Children({
  parent,
  depth,
  levels,
  open,
  chosen,
  onPath,
  onChoose,
}: {
  parent: string;
  depth: number;
  levels: Map<string, Level>;
  open: Set<string>;
  chosen: string | null;
  onPath: Set<string>;
  onChoose: (key: string) => void;
}) {
  const level = levels.get(parent);
  if (!level) {
    return (
      <li className={styles.reading} style={{ '--depth': depth } as React.CSSProperties}>
        <i />
        <i />
      </li>
    );
  }
  if ('error' in level) {
    return (
      <li className={styles.levelNote} style={{ '--depth': depth } as React.CSSProperties}>
        {level.error instanceof ApiError && level.error.status === 501
          ? 'Not in the fixtures: the development mode has no saved answer for this level.'
          : level.error.message}
      </li>
    );
  }
  const { buckets, sum_other_doc_count: other } = level.wire;
  const most = buckets.reduce((max, bucket) => Math.max(max, bucket.records.value), 1);
  return (
    <>
      {buckets.map((bucket, index) => (
        <Node
          key={bucket.key}
          bucket={bucket}
          index={index}
          share={bucket.records.value / most}
          depth={depth}
          levels={levels}
          open={open}
          chosen={chosen}
          onPath={onPath}
          onChoose={onChoose}
        />
      ))}
      {other > 0 && (
        <li className={styles.levelNote} style={{ '--depth': depth } as React.CSSProperties}>
          And more: {other.toLocaleString('en')} documents in places not listed (the API sends the
          1,000 largest of a level).
        </li>
      )}
    </>
  );
}

function Node({
  bucket,
  index,
  share,
  depth,
  levels,
  open,
  chosen,
  onPath,
  onChoose,
}: {
  bucket: WireBucket;
  index: number;
  share: number;
  depth: number;
  levels: Map<string, Level>;
  open: Set<string>;
  chosen: string | null;
  onPath: Set<string>;
  onChoose: (key: string) => void;
}) {
  const key = bucket.key;
  const isOpen = open.has(key);
  const children = levels.get(key);
  const leaf = children !== undefined && 'wire' in children && children.wire.buckets.length === 0;
  return (
    <li>
      <button
        type="button"
        className={styles.node}
        style={
          {
            '--depth': depth,
            '--share': share,
            animationDelay: `${Math.min(index, 12) * 22}ms`,
          } as React.CSSProperties
        }
        aria-expanded={leaf ? undefined : isOpen}
        aria-current={key === chosen || undefined}
        data-chosen={key === chosen || undefined}
        data-path={onPath.has(key) || undefined}
        onClick={() => onChoose(key)}
      >
        <svg viewBox="0 0 20 20" aria-hidden="true" data-leaf={leaf || undefined}>
          <path d="m8 5 5 5-5 5" />
        </svg>
        <span className={styles.name}>
          {nameOf(key)}
          {depth === 0 && <code>{key}</code>}
        </span>
        <b>{bucket.records.value.toLocaleString('en')}</b>
      </button>
      {isOpen && !leaf && (
        <ul className={styles.rows} style={{ '--guide': 17 + depth * 18 } as React.CSSProperties}>
          <Children
            parent={key}
            depth={depth + 1}
            levels={levels}
            open={open}
            chosen={chosen}
            onPath={onPath}
            onChoose={onChoose}
          />
        </ul>
      )}
    </li>
  );
}

function RecordRow({
  item,
  current,
  onOpen,
}: {
  item: Item;
  current: boolean;
  onOpen: () => void;
}) {
  const heading = headingOfItem(item);
  const detail = [
    typeWords(item.type),
    item.time?.start ? formatTime(item.time.start, item.time.precision) : undefined,
    item.sources[0] ? sourceName(item.sources[0]) : undefined,
  ]
    .filter(Boolean)
    .join(' · ');
  return (
    <button
      type="button"
      className={styles.record}
      data-current={current || undefined}
      onClick={onOpen}
    >
      <i style={{ background: heading.color, boxShadow: `0 0 8px ${heading.color}` }} />
      <span className={styles.recordLabel}>{heading.title}</span>
      <span className={styles.recordDetail}>{detail}</span>
      <svg viewBox="0 0 20 20" aria-hidden="true">
        <path d="m8 5 5 5-5 5" />
      </svg>
    </button>
  );
}

function Failure({ error }: { error: Error }) {
  return (
    <p className={styles.levelNote}>
      {error instanceof ApiError && error.status === 501
        ? 'Not in the fixtures: the development mode has no saved answer for these records.'
        : error.message}
    </p>
  );
}

/** "cyber.vendor_product/Apache/Log4j2" → its prefixes, the space first. */
function prefixesOf(key: string): string[] {
  const parts = key.split('/');
  return parts.map((_, index) => parts.slice(0, index + 1).join('/'));
}

/** What a node is called: its last part; a space ("cyber.vendor_product") in words. */
function nameOf(key: string): string {
  const last = key.split('/').at(-1) ?? key;
  return key.includes('/') ? last : words(last.replace('.', ' › ').replace(/_/g, ' '));
}

function toggled(set: Set<string>, key: string): Set<string> {
  const next = new Set(set);
  if (next.has(key)) next.delete(key);
  else next.add(key);
  return next;
}
