/**
 * The state of a legend's toggles: which series the reader turned off.
 */
import { useState } from 'react';

/** The hidden series, and a function that turns one on or off. */
export function useHiddenSeries(): [ReadonlySet<string>, (key: string) => void] {
  const [hidden, setHidden] = useState<ReadonlySet<string>>(new Set());
  const toggle = (key: string) =>
    setHidden((previous) => {
      const next = new Set(previous);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  return [hidden, toggle];
}
