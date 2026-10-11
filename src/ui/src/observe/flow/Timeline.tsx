/**
 * The time-travel bar under the live flow. It shows how much arrived over a window of time (1 hour to 30 days),
 * with refused records in red, and a playhead at the scene's moment. Drag or click the bar to travel to a moment
 * and replay from there; play, pause, step back and change the speed with the buttons (or Space, the arrow keys and
 * L for live); return to the present with "Return to live".
 *
 * The bars are drawn once per refresh and slid smoothly as time passes; the playhead, the clock and the range input
 * underneath (keyboards and screen readers use it) follow the scene every frame through the DOM, so the bar costs
 * nothing while it waits.
 */
import { scaleTime, scaleUtc } from 'd3-scale';
import {
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
  type PointerEvent,
  type ReactNode,
} from 'react';
import { useObserveData } from '../data/observeContext';
import { useResource } from '../data/useResource';
import { useNow } from '../lib/clock';
import { formatCount, formatSpan } from '../lib/format';
import { DAY, HOUR, niceInterval } from '../lib/time';
import { useTimeFormats } from '../lib/timeZone';
import { useElementSize } from '../lib/useElementSize';
import { Popover, Segmented } from '../ui/controls';
import controls from '../ui/controls.module.css';
import { Icon } from '../ui/Icon';
import { speedLabel, speeds, type Playback } from './playback';
import styles from './Timeline.module.css';

/** The windows the bar can show. */
const windows = [
  { value: String(HOUR), label: '1 h' },
  { value: String(6 * HOUR), label: '6 h' },
  { value: String(DAY), label: '24 h' },
  { value: String(7 * DAY), label: '7 d' },
  { value: String(30 * DAY), label: '30 d' },
] as const;

const barHeight = 46;

/** A point of the track under the pointer: where it is, which interval it is in, what moment it means. */
interface TrackPoint {
  x: number;
  index: number;
  time: number;
}

/** The time-travel bar. */
export function Timeline({
  playback,
  windowMs,
  onWindowChange,
  onSeek,
  children,
}: {
  playback: Playback;
  windowMs: number;
  onWindowChange: (windowMs: number) => void;
  /** Travels to a moment (the section resets the scene and plays from there). */
  onSeek: (time: number) => void;
  /** Shown under the bar, between the window choice and the zone (the legend). */
  children?: ReactNode;
}) {
  const state = useSyncExternalStore(playback.subscribe, playback.getSnapshot);
  const { log, mode } = useObserveData();
  const formats = useTimeFormats();
  const [track, setTrack] = useState<HTMLDivElement | null>(null);
  const { width } = useElementSize(track);
  const intervalMs = niceInterval(windowMs, Math.max(24, Math.floor(width / 6)));
  // The window's end when the bars were read: they are read again every 15 seconds, and slid in between.
  const readAt = useNow(15_000);

  const activity = useResource(
    width > 0 ? `activity:${mode}:${windowMs}:${intervalMs}:${readAt}` : null,
    (signal) =>
      log.activity({ range: { from: readAt - windowMs, to: readAt }, intervalMs }, signal),
  );
  const buckets = activity.data;
  const peak = useMemo(
    () => Math.max(1, ...(buckets ?? []).map((bucket) => bucket.received)),
    [buckets],
  );

  const slide = useRef<HTMLDivElement>(null);
  const slideTicks = useRef<HTMLDivElement>(null);
  const played = useRef<HTMLDivElement>(null);
  const head = useRef<HTMLDivElement>(null);
  const clock = useRef<HTMLSpanElement>(null);
  const range = useRef<HTMLInputElement>(null);
  const [hover, setHover] = useState<TrackPoint | null>(null);
  const [drag, setDrag] = useState<TrackPoint | null>(null);

  // Every frame: slide the bars with time, move the playhead, write the clock, keep the range input in step.
  useEffect(() => {
    let frame = 0;
    const draw = () => {
      frame = requestAnimationFrame(draw);
      if (width <= 0) {
        return;
      }
      const now = Date.now();
      const shift = ((now - readAt) / windowMs) * width;
      for (const layer of [slide.current, slideTicks.current]) {
        if (layer) {
          layer.style.transform = `translate3d(${-shift}px, 0, 0)`;
        }
      }
      const scene = playback.now();
      const x = Math.min(width, Math.max(0, ((scene - (now - windowMs)) / windowMs) * width));
      if (head.current) {
        head.current.style.transform = `translate3d(${x}px, 0, 0)`;
      }
      if (played.current) {
        played.current.style.width = `${x + shift}px`;
      }
      if (clock.current) {
        clock.current.textContent = formats.dateTime(scene);
      }
      const input = range.current;
      if (input && document.activeElement !== input) {
        input.min = String(now - windowMs);
        input.max = String(now);
        input.value = String(Math.round(scene));
        input.setAttribute(
          'aria-valuetext',
          playback.getSnapshot().mode === 'live' ? 'Live' : formats.dateTime(scene),
        );
      }
    };
    draw();
    return () => cancelAnimationFrame(frame);
  }, [width, windowMs, readAt, playback, formats]);

  /** The point of the track at `x` pixels from its left edge. */
  const pointAt = (x: number): TrackPoint => {
    const clamped = Math.min(width, Math.max(0, x));
    const time = Date.now() - windowMs + (clamped / width) * windowMs;
    const index =
      buckets?.findIndex((bucket) => time >= bucket.start && time < bucket.start + intervalMs) ??
      -1;
    return { x: clamped, index, time };
  };

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    event.currentTarget.setPointerCapture(event.pointerId);
    setDrag(pointAt(event.nativeEvent.offsetX));
  };
  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const point = pointAt(event.nativeEvent.offsetX);
    if (drag !== null) {
      setDrag(point);
    }
    setHover((previous) =>
      previous?.index === point.index && Math.abs(previous.x - point.x) < 1 ? previous : point,
    );
  };
  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    if (drag !== null) {
      onSeek(pointAt(event.nativeEvent.offsetX).time);
      setDrag(null);
    }
  };

  const ticks = useMemo(() => {
    if (width <= 0) {
      return [];
    }
    const scale = (formats.utc ? scaleUtc : scaleTime)()
      .domain([new Date(readAt - windowMs), new Date(readAt + 60_000)])
      .range([0, width + (60_000 / windowMs) * width]);
    return scale.ticks(Math.max(3, Math.floor(width / 120))).map((date) => ({
      x: scale(date),
      text: windowMs > DAY ? formats.day(date.getTime()) : formats.minute(date.getTime()),
    }));
  }, [width, readAt, windowMs, formats]);

  const step = windowMs / 24;
  const hovered = hover && hover.index >= 0 ? buckets?.[hover.index] : undefined;

  return (
    <div className={styles.timeline} data-mode={state.mode}>
      <div className={styles.transport}>
        <button
          type="button"
          className={controls.iconButton}
          onClick={() => onSeek(Math.max(readAt - windowMs, playback.now() - step))}
          aria-label={`Back ${formatSpan(step)}`}
          title={`Back ${formatSpan(step)} (←)`}
        >
          <Icon name="back" />
        </button>
        <button
          type="button"
          className={styles.play}
          onClick={() => playback.toggle()}
          aria-label={state.mode === 'live' || state.playing ? 'Pause' : 'Play'}
          title="Play or pause (Space)"
        >
          <Icon name={state.mode === 'live' || state.playing ? 'pause' : 'play'} size={15} />
        </button>
        <Popover
          label="Replay speed"
          align="start"
          trigger={({ open, toggle, id }) => (
            <button
              type="button"
              className={styles.speed}
              onClick={toggle}
              aria-expanded={open}
              aria-controls={id}
              title="Replay speed"
            >
              {speedLabel(state.speed)}
              <Icon name="chevronDown" size={12} />
            </button>
          )}
        >
          {(close) => (
            <ul className={styles.menu}>
              {speeds.map((speed) => (
                <li key={speed}>
                  <button
                    type="button"
                    className={styles.menuItem}
                    aria-pressed={state.speed === speed}
                    onClick={() => {
                      playback.setSpeed(speed);
                      close();
                    }}
                  >
                    <span>{speedLabel(speed)}</span>
                    <span className={styles.menuNote}>
                      {speed === 1 ? 'real time' : `${formatSpan(speed * 1000)} per second`}
                    </span>
                    {state.speed === speed ? <Icon name="check" size={14} /> : null}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Popover>
      </div>

      <div
        ref={setTrack}
        className={styles.track}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerLeave={() => setHover(null)}
      >
        <input
          ref={range}
          type="range"
          className={styles.range}
          aria-label="Moment of the scene"
          step={Math.round(step)}
          defaultValue={0}
          onChange={(event) => onSeek(Number(event.target.value))}
        />
        <div className={styles.bars} data-loading={activity.loading || undefined}>
          <div ref={slide} className={styles.slide}>
            <Bars
              buckets={buckets}
              from={readAt - windowMs}
              windowMs={windowMs}
              intervalMs={intervalMs}
              width={width}
              peak={peak}
            />
            <div ref={played} className={styles.played}>
              <Bars
                buckets={buckets}
                from={readAt - windowMs}
                windowMs={windowMs}
                intervalMs={intervalMs}
                width={width}
                peak={peak}
                bright
              />
            </div>
          </div>
        </div>
        <div className={styles.ticks} aria-hidden="true">
          <div ref={slideTicks} className={styles.slide}>
            {ticks.map((tick) => (
              <span key={tick.x} className={styles.tick} style={{ left: tick.x }}>
                {tick.text}
              </span>
            ))}
          </div>
        </div>
        <div ref={head} className={styles.head} aria-hidden="true">
          <span className={styles.headKnob} />
        </div>
        {drag !== null ? (
          <div
            className={styles.dragLine}
            style={{ transform: `translateX(${drag.x}px)` }}
            aria-hidden="true"
          >
            <span className={styles.dragLabel}>{formats.dateTime(drag.time)}</span>
          </div>
        ) : hover ? (
          <div
            className={styles.hoverLine}
            style={{ transform: `translateX(${hover.x}px)` }}
            aria-hidden="true"
          >
            <span className={styles.hoverLabel}>
              <span>{formats.dateMinute(hover.time)}</span>
              {hovered ? (
                <span className={styles.hoverCounts}>
                  {formatCount(hovered.received)} records
                  {hovered.rejected > 0 ? (
                    <em> · {formatCount(hovered.rejected)} refused</em>
                  ) : null}
                </span>
              ) : null}
            </span>
          </div>
        ) : null}
      </div>

      <div className={styles.now}>
        {state.mode === 'live' ? (
          <span className={styles.liveBadge}>
            <span className={styles.liveDot} aria-hidden="true" />
            Live
          </span>
        ) : (
          <button
            type="button"
            className={styles.goLive}
            onClick={() => playback.goLive()}
            title="Return to live (L)"
          >
            <Icon name="live" size={15} />
            Return to live
          </button>
        )}
        <span ref={clock} className={styles.clock} />
      </div>

      <div className={styles.windowRow}>
        <Segmented
          small
          label="Window of the time-travel bar"
          value={String(windowMs)}
          onChange={(value) => onWindowChange(Number(value))}
          options={windows}
        />
        <JumpTo windowMs={windowMs} onSeek={onSeek} />
      </div>
      <div className={styles.footer}>{children}</div>
      <div className={styles.zone}>
        {state.mode === 'live' ? 'following the log' : `replay · ${formats.zone}`}
      </div>
    </div>
  );
}

/** The bars of the histogram: records per interval (square root, so quiet intervals still show), refused in red. */
function Bars({
  buckets,
  from,
  windowMs,
  intervalMs,
  width,
  peak,
  bright = false,
}: {
  buckets: readonly { start: number; received: number; rejected: number }[] | undefined;
  from: number;
  windowMs: number;
  intervalMs: number;
  width: number;
  peak: number;
  bright?: boolean;
}) {
  if (!buckets || width <= 0) {
    return null;
  }
  const barWidth = Math.max(1, (intervalMs / windowMs) * width - 2);
  return (
    <svg
      className={styles.svg}
      width={width + barWidth + 4}
      height={barHeight}
      data-bright={bright || undefined}
      aria-hidden="true"
    >
      {buckets.map((bucket) => {
        if (bucket.received <= 0) {
          return null;
        }
        const x = ((bucket.start - from) / windowMs) * width;
        const h = Math.max(2, Math.sqrt(bucket.received / peak) * barHeight);
        const red =
          bucket.rejected > 0
            ? Math.max(2, h * Math.min(1, (bucket.rejected / bucket.received) * 6))
            : 0;
        return (
          <g key={bucket.start}>
            <rect
              className={styles.bar}
              x={x}
              y={barHeight - h}
              width={barWidth}
              height={h}
              rx={1}
            />
            {red > 0 ? (
              <rect
                className={styles.barRefused}
                x={x}
                y={barHeight - h}
                width={barWidth}
                height={Math.min(h, red)}
                rx={1}
              />
            ) : null}
          </g>
        );
      })}
    </svg>
  );
}

/** Travel to a moment typed in: a date and a time, in the chosen zone. */
function JumpTo({ windowMs, onSeek }: { windowMs: number; onSeek: (time: number) => void }) {
  const formats = useTimeFormats();
  const now = useNow(60_000);
  const [value, setValue] = useState('');
  return (
    <Popover
      label="Travel to a moment"
      align="start"
      trigger={({ open, toggle, id }) => (
        <button
          type="button"
          className={controls.ghost}
          onClick={toggle}
          aria-expanded={open}
          aria-controls={id}
          style={{ height: 28, fontSize: 12.5 }}
        >
          <Icon name="clock" size={14} />
          Jump to…
        </button>
      )}
    >
      {(close) => (
        <form
          className={styles.jump}
          onSubmit={(event) => {
            event.preventDefault();
            const time = parseLocal(value, formats.utc);
            if (Number.isFinite(time)) {
              onSeek(time);
              close();
            }
          }}
        >
          <label className={styles.jumpLabel}>
            Moment ({formats.zone})
            <input
              className={styles.jumpInput}
              type="datetime-local"
              step={1}
              value={value}
              max={toLocalInput(now, formats.utc)}
              onChange={(event) => setValue(event.target.value)}
              required
            />
          </label>
          <p className={styles.jumpNote}>
            The bar shows the last {formatSpan(windowMs)}; any earlier moment works too.
          </p>
          <button type="submit" className={controls.primary}>
            Travel there
          </button>
        </form>
      )}
    </Popover>
  );
}

/** A datetime-local value read in the zone shown. */
function parseLocal(value: string, utc: boolean): number {
  if (!value) {
    return Number.NaN;
  }
  return utc ? Date.parse(`${value}Z`) : new Date(value).getTime();
}

/** A moment as a datetime-local value in the zone shown. */
function toLocalInput(time: number, utc: boolean): string {
  const date = new Date(utc ? time : time - new Date(time).getTimezoneOffset() * 60_000);
  return date.toISOString().slice(0, 19);
}
