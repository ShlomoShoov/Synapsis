/**
 * Every question Analyze asks the API, as it was sent and as it was answered: the route, its parameters one by one, how
 * long it took, the status, and the answer exactly as it came (or that it came from memory). The query panel shows it.
 * Kept in memory, the newest first, up to `kept` entries.
 */
import { useSyncExternalStore } from 'react';

export interface QueryRecord {
  id: number;
  /** "GET /analyze/map" */
  route: string;
  /** The parameters in the order they were sent (a list filter appears once per value). */
  params: [string, string][];
  /** The full address. */
  url: string;
  startedAt: number;
  state: 'running' | 'done' | 'failed' | 'memory';
  /** How long the answer took (ms); 0 from memory. */
  ms?: number;
  status?: number;
  /** The answer's size in bytes, as received. */
  bytes?: number;
  /** The answer as it came, parsed. */
  answer?: unknown;
  error?: string;
}

const kept = 80;
let records: QueryRecord[] = [];
let nextId = 1;
const listeners = new Set<() => void>();

function publish(next: QueryRecord[]) {
  records = next.slice(0, kept);
  for (const listener of listeners) listener();
}

/** Notes a request as it leaves; returns its id. */
export function logStart(url: string): number {
  const id = nextId++;
  publish([{ id, ...describe(url), url, startedAt: Date.now(), state: 'running' }, ...records]);
  return id;
}

/** Notes how a request ended. */
export function logEnd(id: number, end: Partial<QueryRecord>) {
  publish(records.map((record) => (record.id === id ? { ...record, ...end } : record)));
}

/** Notes a question answered from memory (asked before; nothing sent). */
export function logMemory(url: string, answer: unknown) {
  const id = nextId++;
  publish([
    { id, ...describe(url), url, startedAt: Date.now(), state: 'memory', ms: 0, answer },
    ...records,
  ]);
}

export function clearLog() {
  publish([]);
}

/** The log, newest first, kept current. */
export function useQueryLog(): QueryRecord[] {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => records,
  );
}

/** "GET /analyze/map" and the parameters, from an address. */
export function describe(url: string): { route: string; params: [string, string][] } {
  const parsed = new URL(url, 'http://localhost');
  return {
    route: `GET ${decodeURIComponent(parsed.pathname)}`,
    params: [...parsed.searchParams.entries()],
  };
}
