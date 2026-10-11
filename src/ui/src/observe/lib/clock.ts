/**
 * A shared clock for views that show "now" (ages such as "4 min ago", the edge of a time window). Components read it
 * through useNow, an external store, so they never call Date.now() while drawing: every view that asks for the same
 * resolution sees the same moment, and redraws together when it advances.
 */
import { useSyncExternalStore } from 'react';

interface Tick {
  value: number;
  listeners: Set<() => void>;
  timer: number | undefined;
}

const ticks = new Map<number, Tick>();

function tickOf(resolutionMs: number): Tick {
  let tick = ticks.get(resolutionMs);
  if (!tick) {
    tick = { value: Date.now(), listeners: new Set(), timer: undefined };
    ticks.set(resolutionMs, tick);
  }
  return tick;
}

function subscribe(resolutionMs: number, listener: () => void): () => void {
  const tick = tickOf(resolutionMs);
  tick.listeners.add(listener);
  if (tick.timer === undefined) {
    tick.value = Date.now();
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
}

/** The current time, advancing every `resolutionMs` (the component redraws then). */
export function useNow(resolutionMs: number): number {
  return useSyncExternalStore(
    (listener) => subscribe(resolutionMs, listener),
    () => tickOf(resolutionMs).value,
  );
}
