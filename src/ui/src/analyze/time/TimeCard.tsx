/**
 * The time card, top right: the two times of Analyze in one small panel.
 *
 * - "When it happened" (from/to), the main one: the dates, the ruler with the window, and presets. It starts on the
 *   last month.
 * - LIVE: travel through the window. The playhead runs from its start; events light up as it reaches them and fade.
 * - "Seen as of" (as_of), the secondary one: what the system knew on a date, like reading that day's newspaper. It
 *   starts on today.
 */
import { useRef, useState } from 'react';
import { utcYear } from 'd3-time';
import type { Live } from './useLive';
import { speeds } from './live';
import { Ruler } from './Ruler';
import { extentAround, type Extent } from './ruler';
import {
  dayInputValue,
  dayMs,
  describeLength,
  formatMoment,
  formatWindow,
  fromDayInput,
  lastMonth,
  presetOf,
  presets,
  type TimeWindow,
} from './window';
import styles from './TimeCard.module.css';

interface TimeCardProps {
  window: TimeWindow;
  onWindowChange: (window: TimeWindow) => void;
  /** What was known by then (ms); undefined = now. */
  asOf: number | undefined;
  onAsOfChange: (asOf: number | undefined) => void;
  now: number;
  live: Live;
  /** Shown at the end of the presets row: what the window holds, or that it is loading. */
  status?: React.ReactNode;
  /** A note under LIVE (why it cannot play everything, for instance). */
  liveNote?: string;
}

/** How far back "Seen as of" reaches. */
const asOfYears = 10;

const shortPresets: Record<string, string> = {
  month: '1M',
  year: '1Y',
  decade: '10Y',
  all: 'All',
};

export function TimeCard({
  window,
  onWindowChange,
  asOf,
  onAsOfChange,
  now,
  live,
  status,
  liveNote,
}: TimeCardProps) {
  const [extent, setExtent] = useState<Extent>(() => extentAround(window, now));
  const [editing, setEditing] = useState<'from' | 'to' | null>(null);
  const tween = useRef(0);
  const today = lastMonth(now).to - dayMs; // the start of today
  const latest = today + dayMs; // windows end at the end of today
  const label = formatWindow(window);
  const activePreset = presetOf(window, now);

  /** Glides the ruler to show a window chosen elsewhere (a preset, typed dates). */
  const choose = (next: TimeWindow) => {
    onWindowChange(next);
    cancelAnimationFrame(tween.current);
    const from = extent;
    const target = extentAround(next, now);
    const started = performance.now();
    const step = (time: number) => {
      const t = Math.min(1, (time - started) / 480);
      const eased = 1 - (1 - t) ** 3;
      setExtent({
        start: from.start + (target.start - from.start) * eased,
        end: from.end + (target.end - from.end) * eased,
      });
      if (t < 1) tween.current = requestAnimationFrame(step);
    };
    tween.current = requestAnimationFrame(step);
  };

  const onDate = (edge: 'from' | 'to', value: string | null) => {
    setEditing(null);
    const day = value ? fromDayInput(value) : null;
    if (day === null) return;
    // The second date is the last day included: the window ends at the start of the next day.
    const next =
      edge === 'from'
        ? { from: Math.min(day, window.to - dayMs), to: window.to }
        : { from: window.from, to: Math.max(day + dayMs, window.from + dayMs) };
    choose({ from: next.from, to: Math.min(next.to, latest) });
  };

  const asOfStart = utcYear.offset(new Date(today), -asOfYears).getTime();

  return (
    <section className={styles.card} aria-label="Time" data-live={live.on || undefined}>
      <header className={styles.top}>
        <span className={styles.kicker}>
          {live.on ? 'Travelling through time' : 'When it happened'}
        </span>
        <button
          type="button"
          className={styles.liveButton}
          aria-pressed={live.on}
          onClick={live.on ? live.stop : live.start}
          title={
            live.on
              ? 'Leave LIVE'
              : 'Travel through the window: events appear and fade as time passes'
          }
        >
          <span className={styles.liveDot} />
          Live
        </button>
      </header>

      {live.on ? (
        <div className={styles.playRow}>
          <button
            type="button"
            className={styles.play}
            onClick={live.toggle}
            aria-label={live.playing ? 'Pause' : 'Play'}
          >
            <svg viewBox="0 0 20 20" aria-hidden="true">
              {live.playing ? (
                <path d="M6 4h3v12H6zM11 4h3v12h-3z" />
              ) : (
                <path d="M6 4l10 6-10 6z" />
              )}
            </svg>
          </button>
          <span className={styles.head}>{formatMoment(live.head)}</span>
          <select
            className={styles.speed}
            value={live.speed}
            aria-label="Speed"
            onChange={(event) => live.setSpeed(event.currentTarget.value as typeof live.speed)}
          >
            {speeds.map((speed) => (
              <option key={speed.id} value={speed.id}>
                {speed.label}
              </option>
            ))}
          </select>
        </div>
      ) : (
        <div className={styles.dates}>
          <DateField
            label="From"
            text={label.from}
            value={dayInputValue(window.from)}
            editing={editing === 'from'}
            onEdit={() => setEditing('from')}
            onDone={(value) => onDate('from', value)}
          />
          <span className={styles.dash} aria-hidden="true">
            →
          </span>
          <DateField
            label="To"
            text={label.to}
            value={dayInputValue(window.to - dayMs)}
            editing={editing === 'to'}
            onEdit={() => setEditing('to')}
            onDone={(value) => onDate('to', value)}
          />
        </div>
      )}

      <Ruler
        window={window}
        onWindowChange={onWindowChange}
        extent={extent}
        onExtentChange={setExtent}
        now={now}
        latest={latest}
        playhead={live.on ? live.head : undefined}
      />

      <div className={styles.row}>
        <fieldset className={styles.presets}>
          <legend className={styles.hidden}>Choose a period</legend>
          {presets.map((preset) => (
            <button
              key={preset.id}
              type="button"
              aria-pressed={activePreset === preset.id}
              title={preset.label}
              onClick={() => choose(preset.window(now))}
            >
              {shortPresets[preset.id] ?? preset.label}
            </button>
          ))}
        </fieldset>
        <span className={styles.length}>{describeLength(window)}</span>
        <span className={styles.status}>{status}</span>
      </div>
      {live.on && liveNote && <p className={styles.note}>{liveNote}</p>}

      <div className={styles.asOf} data-past={asOf !== undefined || undefined}>
        <span className={styles.kicker}>Seen as of</span>
        <span className={styles.asOfValue}>
          {asOf === undefined ? 'Today' : formatMoment(asOf)}
        </span>
        <input
          type="range"
          className={styles.asOfRange}
          aria-label="Seen as of: what the system knew on this date"
          aria-valuetext={asOf === undefined ? 'Today' : formatMoment(asOf)}
          min={asOfStart}
          max={today}
          step={dayMs}
          value={asOf ?? today}
          onChange={(event) => {
            const value = Number(event.currentTarget.value);
            onAsOfChange(value >= today ? undefined : value);
          }}
        />
        {asOf !== undefined && (
          <button type="button" className={styles.reset} onClick={() => onAsOfChange(undefined)}>
            Today
          </button>
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
