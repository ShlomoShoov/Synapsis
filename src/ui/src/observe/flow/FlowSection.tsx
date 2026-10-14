/**
 * Section 01, the live flow: every batch the API receives, drawn as an arc from its source to the core, live or
 * replayed from any moment of the past.
 *
 * - Live, it polls the ingestion log every two seconds (livePoller.ts) and plays what arrived.
 * - Replaying, it reads the log forward from the chosen moment (replay.ts) and plays each batch when the playhead
 *   passes it, at the chosen speed (playback.ts).
 * - Either way the director (director.ts) turns entries into arcs, the arrivals feed and the readouts, and the pylons
 *   show what each source sent over the 24 hours before the scene's moment.
 */
import { useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { bySourceOrder, sourceInfo, type SourceInfo } from '../data/catalog';
import { LivePoller } from '../data/livePoller';
import type { LogEntry } from '../data/model';
import { useObserveData } from '../data/observeContext';
import { useResource } from '../data/useResource';
import { useNow } from '../lib/clock';
import { ignoreShortcut } from '../lib/keyboard';
import { formatCompact } from '../lib/format';
import { DAY, MINUTE } from '../lib/time';
import { useReducedMotion } from '../lib/useReducedMotion';
import { useTimeFormats } from '../lib/timeZone';
import { BatchSheet } from '../shared/BatchSheet';
import { DomainDot } from '../shared/OutcomeBar';
import controls from '../ui/controls.module.css';
import { Icon } from '../ui/Icon';
import { FlowDirector } from './director';
import type { FlowEngine } from './engine/FlowEngine';
import { FlowCanvas } from './FlowCanvas';
import styles from './FlowSection.module.css';
import { ArrivalsFeed, Legend, Readouts, SourceCard } from './hud';
import { type Playback, speedLabel } from './playback';
import { ReplayFeeder } from './replay';
import { Timeline } from './Timeline';

/** The live flow section, on the page's clock (the pipeline below follows the same one). */
export function FlowSection({ playback }: { playback: Playback }) {
  const { log, catalog, mode, health, setMode } = useObserveData();
  const formats = useTimeFormats();
  const reducedMotion = useReducedMotion();
  const director = useMemo(() => new FlowDirector(), []);
  const feeder = useMemo(() => new ReplayFeeder(log), [log]);
  const play = useSyncExternalStore(playback.subscribe, playback.getSnapshot);
  const scene = useSyncExternalStore(director.subscribe, director.getSnapshot);
  const [engine, setEngine] = useState<FlowEngine | null>(null);
  const visible = useRef(false);
  const sectionRef = useRef<HTMLElement>(null);
  const feedBox = useRef<HTMLDivElement>(null);
  const [focus, setFocus] = useState<string | null>(null);
  const [windowMs, setWindowMs] = useState(DAY);
  const [openEntry, setOpenEntry] = useState<LogEntry | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  /** The moment the pylons describe while replaying (live, they follow the present). */
  const [volumeAt, setVolumeAt] = useState(0);
  const volumeMoment = play.mode === 'live' ? null : volumeAt;
  const now = useNow(10_000);

  /** Travels to a moment: the playback jumps, and the pylons follow at once. */
  const seekTo = (time: number) => {
    playback.seek(time);
    setVolumeAt(Math.round(time));
  };

  // The ring: every declared source, and any source the log named over the last 30 days.
  const history = useResource(
    `history:${mode}`,
    (signal) => log.sourceHistory({ to: Date.now(), lookbackMs: 30 * DAY }, signal),
    { refreshMs: 5 * MINUTE },
  );
  const ringSources = useMemo<SourceInfo[]>(() => {
    const declared = new Set(catalog.sources.map((source) => source.id));
    const named = (history.data ?? [])
      .filter((item) => !declared.has(item.source))
      .map((item) => sourceInfo(catalog, item.source));
    return [...catalog.sources, ...named].sort(bySourceOrder);
  }, [catalog, history.data]);

  // The pylons: what each source sent over the 24 hours before the scene's moment.
  const windows = useResource(
    `windows:${mode}:${volumeMoment ?? 'live'}`,
    (signal) => {
      const to = volumeMoment ?? Date.now();
      return log.sourceWindows({ range: { from: to - DAY, to } }, signal);
    },
    { refreshMs: volumeMoment === null ? 30_000 : undefined },
  );
  const windowOf = useMemo(
    () => new Map((windows.data ?? []).map((item) => [item.source, item])),
    [windows.data],
  );
  const volumes = useMemo(
    () => new Map((windows.data ?? []).map((item) => [item.source, item.received])),
    [windows.data],
  );

  useEffect(() => director.setEngine(engine), [director, engine]);
  useEffect(() => director.setFocus(focus), [director, focus]);
  useEffect(() => {
    director.setWindow(
      play.mode === 'live' ? 5 * MINUTE : Math.min(DAY, Math.max(5 * MINUTE, play.speed * 5_000)),
    );
  }, [director, play.mode, play.speed]);
  useEffect(() => () => feeder.dispose(), [feeder]);

  // Live: fill the feed with the latest batches (without arcs: they came before the scene opened), then poll.
  useEffect(() => {
    if (play.mode !== 'live') {
      return;
    }
    director.reset();
    const controller = new AbortController();
    const poller = new LivePoller(log, { since: Date.now() - MINUTE });
    const start = () =>
      poller.start(
        (entries) => {
          setProblem(null);
          director.play(entries, 1.8, visible.current);
        },
        (error) => setProblem(error instanceof Error ? error.message : String(error)),
      );
    log
      .entries(
        { range: { from: Date.now() - 30 * DAY, to: Date.now() + 1 }, order: 'desc', limit: 12 },
        controller.signal,
      )
      .then((page) => {
        if (controller.signal.aborted) {
          return;
        }
        const entries = [...page.entries].reverse();
        poller.remember(entries);
        director.play(entries, 0, false);
      })
      .catch(() => {})
      .finally(() => {
        if (!controller.signal.aborted) {
          start();
        }
      });
    return () => {
      controller.abort();
      poller.stop();
    };
  }, [play.mode, play.seekCount, log, director]);

  // Replay: after every jump, start reading from the new moment.
  useEffect(() => {
    if (play.mode !== 'replay') {
      return;
    }
    const time = playback.now();
    director.reset();
    feeder.reset(time, (error) =>
      setProblem(error instanceof Error ? error.message : String(error)),
    );
    const controller = new AbortController();
    log
      .entries(
        { range: { from: time - DAY, to: time }, order: 'desc', limit: 12 },
        controller.signal,
      )
      .then((page) => {
        if (!controller.signal.aborted) {
          director.play([...page.entries].reverse(), 0, false);
        }
      })
      .catch(() => {});
    return () => controller.abort();
    // The playhead's moment is read once per jump; play/pause and speed changes do not restart the replay.
  }, [play.mode, play.seekCount, playback, feeder, director, log]);

  // Every frame: hand the replay's due entries to the director, let it fire and refresh its readouts.
  useEffect(() => {
    let frame = 0;
    let lastVolumes = 0;
    const loop = (realTime: number) => {
      frame = requestAnimationFrame(loop);
      const state = playback.getSnapshot();
      const time = playback.now();
      if (state.mode === 'replay' && state.playing) {
        director.play(feeder.due(time, Math.max(MINUTE, state.speed * 4_000)), 0, visible.current);
        playback.catchUp();
        if (realTime - lastVolumes > 4_000) {
          lastVolumes = realTime;
          setVolumeAt(Math.round(time));
        }
      }
      director.tick(time, realTime);
    };
    frame = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(frame);
  }, [playback, feeder, director]);

  // Space plays and pauses, the arrows step, L returns to live, while the flow is on screen.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (!visible.current || ignoreShortcut(event)) {
        return;
      }
      const step = windowMs / 24;
      if (event.key === ' ') {
        event.preventDefault();
        playback.toggle();
      } else if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
        event.preventDefault();
        const time = playback.now() + (event.key === 'ArrowLeft' ? -step : step);
        playback.seek(time);
        setVolumeAt(Math.round(time));
      } else if (event.key === 'l' || event.key === 'L') {
        playback.goLive();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [playback, windowMs]);

  const unreachable = mode === 'live' && health.log !== null && !health.log.ok;
  const empty =
    !unreachable &&
    history.data !== undefined &&
    history.data.length === 0 &&
    scene.feed.length === 0;
  const perMinute = scene.stats.received / (scene.stats.windowMs / MINUTE);

  return (
    <section
      ref={sectionRef}
      id="flow"
      data-nav-section="flow"
      className={styles.section}
      aria-labelledby="flow-title"
    >
      <FlowCanvas
        sources={ringSources}
        volumes={volumes}
        focus={focus}
        onFocus={setFocus}
        onEngine={setEngine}
        onVisible={(value) => {
          visible.current = value;
          // Out of sight, the readouts stop re-rendering and looping animations pause (.section[data-offscreen]).
          director.setShown(value);
          sectionRef.current?.toggleAttribute('data-offscreen', !value);
        }}
        reducedMotion={reducedMotion}
        avoid={feedBox}
        coreCaption={`${formatCompact(Math.round(perMinute))} records / min`}
        renderCard={(source) => (
          <SourceCard
            source={source}
            window={windowOf.get(source.id)}
            now={volumeMoment ?? now}
            focused={focus === source.id}
          />
        )}
      />
      <div className={styles.vignette} aria-hidden="true" />

      <header className={styles.top}>
        <div className={styles.titleBlock}>
          <p className={styles.eyebrow}>
            <span>01</span>
            <span className={styles.eyebrowRule} aria-hidden="true" />
            <span>Observe</span>
          </p>
          <h2 id="flow-title" className={styles.title}>
            Live <em>flow</em>
          </h2>
          <p className={styles.status} data-mode={play.mode}>
            {play.mode === 'live' ? (
              <>
                <span className={styles.statusDot} aria-hidden="true" />
                Following the log as it is written
                <span className={styles.statusWhere}>
                  {mode === 'simulated' ? 'simulated data' : `polling ${log.location} every 2 s`}
                </span>
              </>
            ) : (
              <>
                <Icon name="clock" size={14} />
                {play.playing
                  ? `Replaying from ${formats.dateMinute(play.origin)}`
                  : `Paused at ${formats.dateTime(play.anchorTime)}`}
                <span className={styles.statusWhere}>
                  {play.playing ? `at ${speedLabel(play.speed)}` : 'press Space to play'}
                </span>
              </>
            )}
          </p>
          {focus ? (
            <button type="button" className={styles.focusChip} onClick={() => setFocus(null)}>
              <DomainDot domain={sourceInfo(catalog, focus).domain} size={7} />
              Following {sourceInfo(catalog, focus).short}
              <Icon name="close" size={12} />
            </button>
          ) : null}
        </div>
        <Readouts stats={scene.stats} totalSources={ringSources.length} />
      </header>

      <div ref={feedBox} className={styles.feedBox}>
        <ArrivalsFeed
          feed={scene.feed}
          sourceOf={(id) => sourceInfo(catalog, id)}
          onOpen={setOpenEntry}
          focus={focus}
        />
      </div>

      <div className={styles.viewControls}>
        <button
          type="button"
          className={controls.iconButton}
          onClick={() => engine?.zoomBy(0.85)}
          aria-label="Closer"
          title="Closer (or pinch)"
        >
          <Icon name="plus" />
        </button>
        <button
          type="button"
          className={controls.iconButton}
          onClick={() => engine?.zoomBy(1.18)}
          aria-label="Farther"
          title="Farther (or pinch)"
        >
          <Icon name="minus" />
        </button>
        <button
          type="button"
          className={controls.iconButton}
          onClick={() => engine?.resetView()}
          aria-label="Reset the view"
          title="Reset the view (or double-click the scene)"
        >
          <Icon name="fit" />
        </button>
      </div>

      <div className={styles.bottom}>
        <Timeline
          playback={playback}
          windowMs={windowMs}
          onWindowChange={setWindowMs}
          onSeek={seekTo}
        >
          <Legend />
        </Timeline>
      </div>

      {unreachable ? (
        <div className={styles.notice} role="alert">
          <Icon name="alert" size={18} />
          <div>
            <p className={styles.noticeTitle}>Elasticsearch does not answer</p>
            <p className={styles.noticeText}>
              Observe reads the ingestion log at <code>{log.location}</code>. Start the core with{' '}
              <code>docker compose -f deploy/core/compose.yml up -d</code>, or explore with
              simulated data meanwhile.
            </p>
          </div>
          <button type="button" className={controls.primary} onClick={() => setMode('simulated')}>
            Use simulated data
          </button>
        </div>
      ) : empty ? (
        <div className={styles.notice}>
          <Icon name="database" size={18} />
          <div>
            <p className={styles.noticeTitle}>No batches yet</p>
            <p className={styles.noticeText}>
              The log fills as soon as feeders send data:{' '}
              <code>docker compose -f deploy/feeders/compose.yml up -d</code>.
            </p>
          </div>
          <button type="button" className={controls.button} onClick={() => setMode('simulated')}>
            Try simulated data
          </button>
        </div>
      ) : problem && mode === 'live' ? (
        <div className={styles.notice} aria-live="polite">
          <Icon name="warning" size={18} />
          <p className={styles.noticeText}>{problem}</p>
        </div>
      ) : null}

      <BatchSheet entry={openEntry} onClose={() => setOpenEntry(null)} />
    </section>
  );
}
