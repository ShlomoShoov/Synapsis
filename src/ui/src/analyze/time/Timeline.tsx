/**
 * The main timeline, "What happened": the window of time whose events the map shows.
 *
 * - Drag the window to slide through time; drag its edges to widen or narrow it. The map follows while dragging.
 * - The ruler under it moves on its own: the wheel zooms it around the pointer, dragging its empty part pans it, and a
 *   click there brings the window to that moment.
 * - The presets and the two dates set the window exactly.
 * - Keyboard: on the window, ←/→ move it by a day (an hour when short), Page Up/Down by its own length; on an edge,
 *   ←/→ move that edge.
 */
import { useRef, useState, type ChangeEvent, type PointerEvent, type WheelEvent } from 'react';
import { useElementWidth } from '../lib/useElementWidth';
import { useReducedMotion } from '../lib/useReducedMotion';
import { clampExtent, extentAround, panned, ticksOf, zoomed, type Extent } from './ruler';
import {
  dayInputValue,
  describeLength,
  formatWindow,
  fromDayInput,
  lastMonth,
  moved,
  presetOf,
  presets,
  resized,
  snapUnit,
  timelineStart,
  type TimeWindow,
} from './window';
import styles from './Timeline.module.css';

interface TimelineProps {
  window: TimeWindow;
  onWindowChange: (window: TimeWindow) => void;
  /** The current time (ms), which the window cannot pass. */
  now: number;
  /** Shown at the right of the header: what the window holds, or that it is loading. */
  status?: React.ReactNode;
}

/** The ruler's margin at each end, in pixels. */
const rulerInset = 28;

/** What a pointer is dragging. */
type Drag =
  | { kind: 'move'; startX: number; window: TimeWindow }
  | { kind: 'from' | 'to'; startX: number; window: TimeWindow }
  | { kind: 'pan'; startX: number; extent: Extent; moved: boolean };

export function Timeline({ window, onWindowChange, now, status }: TimelineProps) {
  const trackRef = useRef<HTMLDivElement>(null);
  const width = useElementWidth(trackRef);
  const reducedMotion = useReducedMotion();
  const [extentState, setExtent] = useState<Extent>(() => extentAround(window, now));
  const [editing, setEditing] = useState<'from' | 'to' | null>(null);
  const drag = useRef<Drag | null>(null);
  const tween = useRef(0);
  /** The window ends at the end of today at the latest. */
  const latest = lastMonth(now).to;

  // The ruler always shows the window: when the window leaves it (a date typed, LIVE), the ruler follows.
  const extent = containing(extentState, window, now);
  const span = extent.end - extent.start;
  // The ruler runs inside a margin, so the labels at its ends stay readable.
  const inner = Math.max(width - 2 * rulerInset, 1);
  const xOf = (ms: number) => rulerInset + ((ms - extent.start) / span) * inner;
  const msOf = (x: number) => extent.start + ((x - rulerInset) / inner) * span;
  const { major, minor } = ticksOf(extent, inner);
  const left = xOf(window.from);
  const right = xOf(window.to);
  const nowX = xOf(now);
  const label = formatWindow(window);
  const activePreset = presetOf(window, now);

  /** Glides the ruler to a new extent. */
  const glideTo = (target: Extent) => {
    cancelAnimationFrame(tween.current);
    if (reducedMotion) {
      setExtent(target);
      return;
    }
    const from = extent;
    const started = performance.now();
    const step = (time: number) => {
      const t = Math.min(1, (time - started) / 520);
      const eased = 1 - (1 - t) ** 3;
      setExtent({
        start: from.start + (target.start - from.start) * eased,
        end: from.end + (target.end - from.end) * eased,
      });
      if (t < 1) tween.current = requestAnimationFrame(step);
    };
    tween.current = requestAnimationFrame(step);
  };

  const choose = (next: TimeWindow) => {
    onWindowChange(next);
    glideTo(extentAround(next, now));
  };

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) return;
    const target = event.target as HTMLElement;
    const part = target.dataset.part;
    cancelAnimationFrame(tween.current);
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
      setExtent(panned(current.extent, -dt, now));
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
      const length = window.to - window.from;
      onWindowChange(moved(window, at - (window.from + length / 2), latest));
    }
  };

  const onWheel = (event: WheelEvent<HTMLDivElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    cancelAnimationFrame(tween.current);
    if (Math.abs(event.deltaX) > Math.abs(event.deltaY)) {
      setExtent(panned(extent, (event.deltaX / inner) * span, now));
    } else {
      const factor = Math.exp(event.deltaY * 0.0022);
      setExtent(zoomed(extent, factor, msOf(event.clientX - rect.left), now));
    }
  };

  // The keyboard works on three native range inputs (hidden; their focus shows on the drawn window and edges).
  const unit = snapUnit(window.to - window.from);
  const length = window.to - window.from;
  const onMoveInput = (event: ChangeEvent<HTMLInputElement>) =>
    onWindowChange(moved(window, Number(event.currentTarget.value) - window.from, latest));
  const onEdgeInput = (edge: 'from' | 'to') => (event: ChangeEvent<HTMLInputElement>) =>
    onWindowChange(resized(window, edge, Number(event.currentTarget.value), latest));

  const onDate = (edge: 'from' | 'to', value: string) => {
    const day = fromDayInput(value);
    if (day === null) return;
    // The second date is the last day included: the window ends at the start of the next day.
    const next =
      edge === 'from'
        ? { from: Math.min(day, window.to - 86_400_000), to: window.to }
        : { from: window.from, to: Math.max(day + 86_400_000, window.from + 86_400_000) };
    setEditing(null);
    choose({ from: next.from, to: Math.min(next.to, latest) });
  };

  return (
    <section className={styles.timeline} aria-label="What happened: the time window of the events">
      <header className={styles.header}>
        <span className={styles.title}>What happened</span>
        <span className={styles.dates}>
          <DateField
            label="From"
            text={label.from}
            value={dayInputValue(window.from)}
            editing={editing === 'from'}
            onEdit={() => setEditing('from')}
            onDone={(value) => (value ? onDate('from', value) : setEditing(null))}
          />
          <span className={styles.dash} aria-hidden="true">
            –
          </span>
          <DateField
            label="To"
            text={label.to}
            value={dayInputValue(window.to - 86_400_000)}
            editing={editing === 'to'}
            onEdit={() => setEditing('to')}
            onDone={(value) => (value ? onDate('to', value) : setEditing(null))}
          />
          <span className={styles.length}>{describeLength(window)}</span>
        </span>
        <fieldset className={styles.presets}>
          <legend className={styles.hidden}>Choose a period</legend>
          {presets.map((preset) => (
            <button
              key={preset.id}
              type="button"
              className={styles.preset}
              aria-pressed={activePreset === preset.id}
              onClick={() => choose(preset.window(now))}
            >
              {preset.label}
            </button>
          ))}
        </fieldset>
        <span className={styles.status}>{status}</span>
      </header>

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
              .filter((tick) => xOf(tick.at) >= 8 && xOf(tick.at) <= width - 8)
              .filter((tick) => Math.abs(xOf(tick.at) - nowX) > 44)
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
            {nowX >= rulerInset && nowX <= width - rulerInset / 2 && (
              <span className={styles.now} style={{ transform: `translateX(${nowX}px)` }}>
                <span>Today</span>
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
              data-narrow={right - left < 40 || undefined}
              style={{ left, width: Math.max(right - left, 2) }}
            >
              <div className={styles.edge} data-part="from" data-side="from" />
              <div className={styles.edge} data-part="to" data-side="to" />
            </div>
          </>
        )}
      </div>
    </section>
  );
}

/** One end of the window as text; a click turns it into a date field. */
function DateField({
  label,
  text,
  value,
  editing,
  onEdit,
  onDone,
}: {
  label: string;
  text: string;
  value: string;
  editing: boolean;
  onEdit: () => void;
  onDone: (value: string | null) => void;
}) {
  if (editing) {
    return (
      <input
        className={styles.dateInput}
        type="date"
        aria-label={label}
        defaultValue={value}
        // eslint-disable-next-line jsx-a11y/no-autofocus -- the field replaces the button that was just clicked
        autoFocus
        onBlur={(event) => onDone(event.currentTarget.value || null)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') onDone(event.currentTarget.value || null);
          if (event.key === 'Escape') onDone(null);
        }}
      />
    );
  }
  return (
    <button
      type="button"
      className={styles.date}
      onClick={onEdit}
      aria-label={`${label}: ${text}. Change`}
    >
      {text}
    </button>
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
