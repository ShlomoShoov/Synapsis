/**
 * useLive: the LIVE playback. Starting it puts the playhead at the window's start and runs it to the end; the playhead
 * goes to the map every frame (onFrame) without rendering React, and the shown date is refreshed a few times a second.
 */
import { useEffect, useRef, useState } from 'react';
import type { LiveTime } from '../map/LightsLayer';
import { advanced, speedFor, trailOf, type SpeedId } from './live';
import type { TimeWindow } from './window';

export interface Live {
  on: boolean;
  playing: boolean;
  /** The playhead as shown (refreshed a few times a second). */
  head: number;
  speed: SpeedId;
  start: () => void;
  stop: () => void;
  toggle: () => void;
  setSpeed: (speed: SpeedId) => void;
}

/** How often the shown date is refreshed while playing. */
const shownEveryMs = 90;

export function useLive(window: TimeWindow, onFrame: (live: LiveTime | null) => void): Live {
  const [on, setOn] = useState(false);
  const [playing, setPlaying] = useState(false);
  const [shown, setShown] = useState(window.from);
  const [chosenSpeed, setSpeed] = useState<SpeedId | null>(null);
  const speed = chosenSpeed ?? speedFor(window.to - window.from);
  const head = useRef(window.from);
  const frameRef = useRef(onFrame);
  useEffect(() => {
    frameRef.current = onFrame;
  });

  // Keep the playhead inside the window when the window changes.
  useEffect(() => {
    if (!on) return;
    head.current = Math.min(Math.max(head.current, window.from), window.to);
    frameRef.current({ head: head.current, trail: trailOf(speed) });
  }, [on, window.from, window.to, speed]);

  useEffect(() => {
    if (!playing) return;
    let frame = 0;
    let last = performance.now();
    let lastShown = 0;
    const step = (now: number) => {
      const next = advanced(head.current, now - last, speed, window.to);
      last = now;
      head.current = next.head;
      frameRef.current({ head: next.head, trail: trailOf(speed) });
      if (now - lastShown > shownEveryMs || next.ended) {
        lastShown = now;
        setShown(next.head);
      }
      if (next.ended) {
        setPlaying(false);
        return;
      }
      frame = requestAnimationFrame(step);
    };
    frame = requestAnimationFrame(step);
    return () => cancelAnimationFrame(frame);
  }, [playing, speed, window.to]);

  return {
    on,
    playing,
    head: on ? shown : window.from,
    speed,
    start: () => {
      head.current = window.from;
      setShown(window.from);
      setOn(true);
      setPlaying(true);
    },
    stop: () => {
      setOn(false);
      setPlaying(false);
      frameRef.current(null);
    },
    toggle: () => {
      if (head.current >= window.to) head.current = window.from;
      setPlaying((value) => !value);
    },
    setSpeed,
  };
}
