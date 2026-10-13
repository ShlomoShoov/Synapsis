/**
 * The ruler of time with the window on it.
 *
 * - Drag the window to slide through time; drag its edges to widen or narrow it.
 * - The ruler moves on its own: the wheel zooms it around the pointer, dragging its empty part pans it, and a click there
 *   brings the window to that moment.
 * - In LIVE, a glowing playhead runs through the window.
 * - Keyboard: three native range inputs (move the window, its start, its end), drawn as the window and its edges.
 *
 * Its stretch of time (the extent) belongs to the parent, so the parent can glide it to a window chosen elsewhere.
 */
import { useRef, type ChangeEvent, type PointerEvent, type WheelEvent } from 'react';
import { useElementWidth } from '../lib/useElementWidth';
import { clampExtent, extentAround, panned, ticksOf, zoomed, type Extent } from './ruler';
import { formatWindow, moved, resized, snapUnit, timelineStart, type TimeWindow } from './window';
import styles from './Ruler.module.css';

interface RulerProps {
  window: TimeWindow;
  onWindowChange: (window: TimeWindow) => void;
  extent: Extent;
  onExtentChange: (extent: Extent) => void;
  now: number;
  /** The latest moment the window may reach (the end of today). */
  latest: number;
  /** LIVE's playhead, when playing. */
  playhead?: number;
}

/** The ruler's margin at each end, in pixels. */
const inset = 14;

/** What a pointer is dragging. */
type Drag =
  | { kind: 'move' | 'from' | 'to'; startX: number; window: TimeWindow }
  | { kind: 'pan'; startX: number; extent: Extent; moved: boolean };

export function Ruler({
  window,
  onWindowChange,
  extent: extentState,
  onExtentChange,
  now,
  latest,
  playhead,
}: RulerProps) {
  const trackRef = useRef<HTMLDivElement>(null);
  const width = useElementWidth(trackRef);
  const drag = useRef<Drag | null>(null);

  // The ruler always shows the window: when the window leaves it (a date typed, LIVE), the ruler follows.
  const extent = containing(extentState, window, now);
  const span = extent.end - extent.start;
  const inner = Math.max(width - 2 * inset, 1);
  const xOf = (ms: number) => inset + ((ms - extent.start) / span) * inner;
  const msOf = (x: number) => extent.start + ((x - inset) / inner) * span;
  const { major, minor } = ticksOf(extent, inner, 64);
  const left = xOf(window.from);
  const right = xOf(window.to);
  const nowX = xOf(now);
  const label = formatWindow(window);
  const unit = snapUnit(window.to - window.from);
  const length = window.to - window.from;

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) return;
    const part = (event.target as HTMLElement).dataset.part;
    if (part === 'from' || part === 'to') {
      drag.current = { kind: part, startX: event.clientX, window };
    } else if (part === 'window') {
      drag.current = { kind: 'move', startX: event.clientX, window };
    } else {
      drag.current = { kind: 'pan', startX: event.clientX, extent, moved: false };
    }
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const current = drag.current;
    if (!current) return;
    const dx = event.clientX - current.startX;
    const dt = (dx / inner) * span;
    if (current.kind === 'pan') {
      if (Math.abs(dx) > 3) current.moved = true;
      onExtentChange(panned(current.extent, -dt, now));
    } else if (current.kind === 'move') {
      const next = moved(current.window, dt, latest);
      if (next.from !== window.from) onWindowChange(next);
    } else {
      const at = (current.kind === 'from' ? current.window.from : current.window.to) + dt;
      const next = resized(current.window, current.kind, at, latest);
      if (next.from !== window.from || next.to !== window.to) onWindowChange(next);
    }
  };

  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    const current = drag.current;
    drag.current = null;
    if (current?.kind === 'pan' && !current.moved) {
      // A click on the ruler brings the window's middle to that moment.
      const rect = event.currentTarget.getBoundingClientRect();
      const at = msOf(event.clientX - rect.left);
      onWindowChange(moved(window, at - (window.from + length / 2), latest));
    }
  };

  const onWheel = (event: WheelEvent<HTMLDivElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    if (Math.abs(event.deltaX) > Math.abs(event.deltaY)) {
      onExtentChange(panned(extent, (event.deltaX / inner) * span, now));
    } else {
      const factor = Math.exp(event.deltaY * 0.0022);
      onExtentChange(zoomed(extent, factor, msOf(event.clientX - rect.left), now));
    }
  };

  const onMoveInput = (event: ChangeEvent<HTMLInputElement>) =>
    onWindowChange(moved(window, Number(event.currentTarget.value) - window.from, latest));
  const onEdgeInput = (edge: 'from' | 'to') => (event: ChangeEvent<HTMLInputElement>) =>
    onWindowChange(resized(window, edge, Number(event.currentTarget.value), latest));

  return (
    <div
      ref={trackRef}
      className={styles.track}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={() => (drag.current = null)}
      onWheel={onWheel}
    >
      {width > 0 && (
        <>
          <div className={styles.future} style={{ left: Math.max(nowX, 0) }} />
          {minor.map((at) => (
            <span
              key={at}
              className={styles.minor}
              style={{ transform: `translateX(${xOf(at)}px)` }}
            />
          ))}
          {major
            .filter((tick) => xOf(tick.at) >= 6 && xOf(tick.at) <= width - 6)
            .filter((tick) => Math.abs(xOf(tick.at) - nowX) > 30)
            .map((tick) => (
              <span
                key={tick.at}
                className={styles.major}
                data-strong={tick.strong || undefined}
                style={{ transform: `translateX(${xOf(tick.at)}px)` }}
              >
                <span className={styles.tickLabel}>{tick.label}</span>
              </span>
            ))}
          {nowX >= inset && nowX <= width && (
            <span className={styles.now} style={{ transform: `translateX(${nowX}px)` }}>
              <span>now</span>
            </span>
          )}
          <input
            className={styles.hidden}
            data-control="window"
            type="range"
            aria-label="Move the time window"
            aria-valuetext={`${label.from} to ${label.to}`}
            min={timelineStart}
            max={latest - length}
            step={unit}
            value={window.from}
            onChange={onMoveInput}
          />
          <input
            className={styles.hidden}
            data-control="from"
            type="range"
            aria-label="Start of the time window"
            aria-valuetext={label.from}
            min={timelineStart}
            max={window.to - unit}
            step={unit}
            value={window.from}
            onChange={onEdgeInput('from')}
          />
          <input
            className={styles.hidden}
            data-control="to"
            type="range"
            aria-label="End of the time window"
            aria-valuetext={label.to}
            min={window.from + unit}
            max={latest}
            step={unit}
            value={window.to}
            onChange={onEdgeInput('to')}
          />
          <div
            className={styles.window}
            data-part="window"
            data-narrow={right - left < 34 || undefined}
            style={{ left, width: Math.max(right - left, 2) }}
          >
            <div className={styles.edge} data-part="from" data-side="from" />
            <div className={styles.edge} data-part="to" data-side="to" />
          </div>
          {playhead !== undefined && (
            <span
              className={styles.playhead}
              style={{ transform: `translateX(${xOf(playhead)}px)` }}
            />
          )}
        </>
      )}
    </div>
  );
}

/** The extent, moved (not resized) so the window is inside it; widened if the window is longer than it. */
function containing(extent: Extent, window: TimeWindow, now: number): Extent {
  const span = extent.end - extent.start;
  const length = window.to - window.from;
  if (length > span * 0.92) return extentAround(window, now);
  const margin = span * 0.04;
  if (window.from < extent.start + margin) {
    return clampExtent({ start: window.from - margin, end: window.from - margin + span }, now);
  }
  if (window.to > extent.end - margin) {
    return clampExtent({ start: window.to + margin - span, end: window.to + margin }, now);
  }
  return extent;
}
