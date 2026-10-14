/** Under the chosen sources: how much of each the read routes can see yet, with a quiet note while it grows. */
import { useEffect, useState } from 'react';
import { fetchSearch } from '../api/client';
import type { WireCatalogEntry } from '../api/contract';
import { indexedOf } from './indexed';
import styles from './FilterCard.module.css';

export function IndexedNote({
  sources,
  catalog,
}: {
  sources: string[];
  catalog: WireCatalogEntry[] | undefined;
}) {
  const [searchable, setSearchable] = useState<Record<string, number>>({});
  const key = sources.join(',');

  useEffect(() => {
    if (!key) return undefined;
    const controller = new AbortController();
    for (const source of key.split(',')) {
      fetchSearch({ sources: [source] }, { limit: 1 }, controller.signal)
        .then((answer) => setSearchable((was) => ({ ...was, [source]: answer.total })))
        .catch(() => undefined);
    }
    return () => controller.abort();
  }, [key]);

  const notes = sources
    .map((source) => {
      const held = catalog?.find((e) => e.field === 'source' && e.value === source)?.count;
      const seen = searchable[source];
      return seen === undefined ? null : { source, note: indexedOf(seen, held) };
    })
    .filter((row) => row?.note);
  if (notes.length === 0) return null;
  const indexing = notes.some((row) => row?.note?.indexing);
  return (
    <p className={styles.indexed} aria-live="polite">
      {notes.map((row) => (
        <span key={row!.source}>
          {row!.source.replace(/^src:/, '')}: {row!.note!.text}
        </span>
      ))}
      {indexing && <em>Still indexing: the map and lists show what is searchable so far.</em>}
    </p>
  );
}
