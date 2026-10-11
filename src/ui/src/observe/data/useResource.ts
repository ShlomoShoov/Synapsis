/**
 * useResource: loads something asynchronous for a view, and keeps it fresh.
 *
 * - The load runs again whenever `key` changes (a text that names the question, such as its range and sources);
 *   the previous request is cancelled, so an old answer never overwrites a newer one.
 * - While a new answer is on its way the previous one stays on screen (`refreshing`), so charts hold their frame
 *   instead of flashing empty.
 * - `refreshMs` reloads on a timer while the page is visible.
 * - A null key loads nothing.
 *
 * Whether a load is pending is worked out from what has arrived (the key and generation of the last answer), not
 * stored: nothing is set while a load starts.
 */
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { isAbort } from './http';

/** The state of a resource. */
export interface Resource<T> {
  /** The latest answer (kept while a new one loads, and after a failed reload). */
  data: T | undefined;
  /** The error of the latest load for the current key, if it failed. */
  error: Error | null;
  /** True while a load runs and there is no answer yet. */
  loading: boolean;
  /** True while a load runs and an older answer is shown. */
  refreshing: boolean;
  /** When the shown answer arrived. */
  updatedAt: number | null;
  /** Loads again now. */
  reload: () => void;
}

/** What has arrived, and for which question. */
interface Arrived<T> {
  key: string | null;
  generation: number;
  data: T | undefined;
  error: Error | null;
  updatedAt: number | null;
}

/** Loads `load` for `key`; see the file comment. */
export function useResource<T>(
  key: string | null,
  load: (signal: AbortSignal) => Promise<T>,
  options: { refreshMs?: number } = {},
): Resource<T> {
  // The newest load function, read by the effect below (layout effects run first, so it is always current).
  const loadRef = useRef(load);
  useLayoutEffect(() => {
    loadRef.current = load;
  });
  const [generation, setGeneration] = useState(0);
  const [arrived, setArrived] = useState<Arrived<T>>({
    key: null,
    generation: -1,
    data: undefined,
    error: null,
    updatedAt: null,
  });
  const reload = useCallback(() => setGeneration((value) => value + 1), []);

  useEffect(() => {
    if (key === null) {
      return;
    }
    const controller = new AbortController();
    loadRef.current(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) {
          setArrived({ key, generation, data, error: null, updatedAt: Date.now() });
        }
      },
      (error: unknown) => {
        if (controller.signal.aborted || isAbort(error)) {
          return;
        }
        setArrived((previous) => ({
          ...previous,
          key,
          generation,
          error: error instanceof Error ? error : new Error(String(error)),
        }));
      },
    );
    return () => controller.abort();
  }, [key, generation]);

  const refreshMs = options.refreshMs;
  useEffect(() => {
    if (!refreshMs || key === null) {
      return;
    }
    const timer = window.setInterval(() => {
      if (document.visibilityState === 'visible') {
        reload();
      }
    }, refreshMs);
    return () => window.clearInterval(timer);
  }, [refreshMs, key, reload]);

  const pending = key !== null && (arrived.key !== key || arrived.generation !== generation);
  return {
    data: arrived.data,
    error: arrived.key === key ? arrived.error : null,
    loading: pending && arrived.data === undefined,
    refreshing: pending && arrived.data !== undefined,
    updatedAt: arrived.updatedAt,
    reload,
  };
}
