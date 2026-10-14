/**
 * Honesty while the indexer catches up: for a chosen source, how many of its records the read routes can see (the
 * total of /search?source=…) against how many the API holds (the catalog's count). Pure.
 */

export interface Indexed {
  /** "indexed ~1.2M of 3.1M" */
  text: string;
  /** Fewer are searchable than the API holds. */
  indexing: boolean;
}

const compact = (n: number) =>
  n.toLocaleString('en', { notation: 'compact', maximumFractionDigits: n >= 1000 ? 1 : 0 });

/** The note for one source; null when the API's count is not known. */
export function indexedOf(searchable: number, held: number | undefined): Indexed | null {
  if (held === undefined || held <= 0) return null;
  if (searchable >= held) return { text: `all ${compact(held)} indexed`, indexing: false };
  return { text: `indexed ~${compact(searchable)} of ${compact(held)}`, indexing: true };
}
