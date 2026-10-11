/**
 * A shared clock for views that show "now" (ages such as "4 min ago", the edge of a time window). Components read it
 * through useNow, an external store, so they never call Date.now() while drawing: every view that asks for the same
 * resolution sees the same moment, and redraws together when it advances.
 */
import { useSyncExternalStore } from 'react';

/** One clock: a moment that advances every `resolutionMs`, and the views that follow it. */
interface Tick {
  value: number;
  listeners: Set<() => void>;
  timer: number | undefined;
  /** Stable functions for useSyncExternalStore (a new function on every render would resubscribe every render). */
  subscribe: (listener: () => void) => () => void;
  getSnapshot: () => number;
}

const ticks = new Map<number, Tick>();

function tickOf(resolutionMs: number): Tick {
  const existing = ticks.get(resolutionMs);
  if (existing) {
    return existing;
  }
  const tick: Tick = {
    value: Date.now(),
    listeners: new Set(),
    timer: undefined,
    subscribe: (listener) => {
      tick.listeners.add(listener);
      if (tick.timer === undefined) {
        // Waking after a pause: catch up first, if the moment held has gone stale.
        if (Date.now() - tick.value >= resolutionMs) {
          tick.value = Date.now();
        }
        tick.timer = window.setInterval(() => {
          tick.value = Date.now();
          for (const notify of tick.listeners) {
            notify();
          }
        }, resolutionMs);
      }
      return () => {
        tick.listeners.delete(listener);
        if (tick.listeners.size === 0) {
          window.clearInterval(tick.timer);
          tick.timer = undefined;
        }
      };
    },
    getSnapshot: () => tick.value,
  };
  ticks.set(resolutionMs, tick);
  return tick;
}

/** The current time, advancing every `resolutionMs` (the component redraws then). */
export function useNow(resolutionMs: number): number {
  const tick = tickOf(resolutionMs);
  return useSyncExternalStore(tick.subscribe, tick.getSnapshot);
}
