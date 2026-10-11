/**
 * Remembering a viewer's choices (the data mode, the dashboard layout) in this browser. Storage can be missing or
 * refuse (private windows, blocked site data): every read falls back to the default and every write may silently
 * do nothing, so nothing depends on it.
 */

const prefix = 'synapsis.observe.';

/** The value stored under `key`, or `fallback` when there is none or it cannot be read. */
export function readStored<T>(key: string, fallback: T): T {
  try {
    const text = window.localStorage.getItem(prefix + key);
    return text === null ? fallback : (JSON.parse(text) as T);
  } catch {
    return fallback;
  }
}

/** Stores `value` under `key` (or removes it, for undefined). */
export function writeStored(key: string, value: unknown): void {
  try {
    if (value === undefined) {
      window.localStorage.removeItem(prefix + key);
    } else {
      window.localStorage.setItem(prefix + key, JSON.stringify(value));
    }
  } catch {
    // Storage is unavailable: the choice lasts for this visit only.
  }
}
