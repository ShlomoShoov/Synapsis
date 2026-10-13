/**
 * useObject: what the API knows of one record, read when it is opened: each source's full record (GET /records/{id},
 * MongoDB) and its connections (GET /graph?id=, Neo4j), side by side. A null id reads nothing.
 */
import { fetchGraph, fetchRecord } from '../api/client';
import type { WireGraph, WireRecordAnswer } from '../api/contract';
import type { LngLat } from '../api/model';
import { useLatest, type Latest } from '../lib/useLatest';
import type { Heading } from './describe';

/** The record being looked at, and what was known of it when it was opened (from the map or a link). */
export interface Focus {
  id: string;
  heading?: Heading;
  point?: LngLat;
  /** The logical place it is filed under (its deepest key), when known. */
  logical?: string;
}

export interface ObjectAnswers {
  records: Latest<WireRecordAnswer[]>;
  graph: Latest<WireGraph>;
}

export function useObject(id: string | null): ObjectAnswers {
  const records = useLatest<WireRecordAnswer[]>(id, (signal) => fetchRecord(id!, signal));
  const graph = useLatest<WireGraph>(id, (signal) => fetchGraph(id!, undefined, signal));
  return { records, graph };
}

/** The answer when it belongs to the record asked for now (not the one before, kept while the next arrives). */
export function current<A>(latest: Latest<A>): A | undefined {
  return latest.pending || latest.error ? undefined : latest.answer;
}
