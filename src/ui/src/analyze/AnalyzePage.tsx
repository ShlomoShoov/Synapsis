/**
 * Analyze (route /analyze): where the user investigates the data. The night Earth fills the page in a sky of stars, the
 * events shine on it as lights coloured by category. Over it: the time card (top right), the key (bottom left), the
 * query panel (left), and the view buttons (right).
 *
 * The address can open a window, a moment of knowledge and a place: ?from=1970-01-01&to=1971-01-01 (the second date
 * excluded), &as_of=2026-10-01, &view=35.5,33.4,7 (longitude, latitude, zoom, and optionally pitch and bearing).
 */
import '@fontsource-variable/instrument-sans/wght.css';
import '@fontsource/fragment-mono/400.css';
import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { EventsKey } from './map/EventsKey';
import { GlobeMap, type Camera, type GlobeController, type MapLook } from './map/GlobeMap';
import { MapControls } from './map/MapControls';
import { useEvents } from './map/useEvents';
import { requestOf } from './map/viewport';
import { QueryPanel } from './query/QueryPanel';
import { TimeCard } from './time/TimeCard';
import { useLive } from './time/useLive';
import { fromDayInput, lastMonth, type TimeWindow } from './time/window';
import styles from './AnalyzePage.module.css';

export function AnalyzePage() {
  const [searchParams] = useSearchParams();
  // The moment the page opened: "today" for the timeline (a page left open over midnight keeps its day).
  const [now] = useState(() => Date.now());
  const [timeWindow, setTimeWindow] = useState<TimeWindow>(() =>
    windowFromAddress(searchParams, now),
  );
  const [asOf, setAsOf] = useState<number | undefined>(
    () => fromDayInput(searchParams.get('as_of') ?? '') ?? undefined,
  );
  const [initialView] = useState(() => viewFromAddress(searchParams));
  const [look, setLook] = useState<MapLook | null>(null);
  const [camera, setCamera] = useState({ bearing: 0, pitch: 0 });
  const [controller, setController] = useState<GlobeController | null>(null);

  const request = look ? requestOf(look.zoom, look.bounds) : null;
  // The read API has no as_of (EMERGENCY.md §12): the control stays, the map reads what is known now.
  const filters = { from: timeWindow.from, to: timeWindow.to };
  const { answer: events, pending, error } = useEvents(filters, request);
  const live = useLive(timeWindow, (time) => controller?.setLive(time));

  return (
    <div className={styles.page}>
      <GlobeMap
        initialView={initialView}
        lights={events?.lights ?? null}
        onLookChange={setLook}
        onCameraChange={setCamera}
        onReady={setController}
      />

      <header className={styles.header}>
        <Link className={styles.brand} to="/home">
          Synapsis
        </Link>
        <span className={styles.area}>Analyze</span>
      </header>

      <div className={styles.left}>
        <QueryPanel />
        <EventsKey events={events} />
      </div>

      <div className={styles.right}>
        <TimeCard
          window={timeWindow}
          onWindowChange={setTimeWindow}
          asOf={asOf}
          onAsOfChange={setAsOf}
          now={now}
          live={live}
          status={
            pending ? (
              <span className={styles.reading}>reading</span>
            ) : events ? (
              `${events.layer.events.toLocaleString('en')} shown`
            ) : null
          }
          liveNote={
            events?.layer.layer === 'cells'
              ? 'Too many events here to play one by one: zoom in or narrow the window.'
              : undefined
          }
        />
        {error && (
          <p className={styles.error} role="alert">
            {error.message}
          </p>
        )}
      </div>

      <div className={styles.controls}>
        <MapControls controller={controller} bearing={camera.bearing} pitch={camera.pitch} />
      </div>
    </div>
  );
}

/** The window named in the address (from, to: dates, to excluded), or the last month. */
function windowFromAddress(params: URLSearchParams, now: number): TimeWindow {
  const from = fromDayInput(params.get('from') ?? '');
  const to = fromDayInput(params.get('to') ?? '');
  if (from !== null && to !== null && from < to) return { from, to };
  return lastMonth(now);
}

/** The view named in the address: ?view=lon,lat,zoom[,pitch[,bearing]]. */
function viewFromAddress(params: URLSearchParams): Camera | undefined {
  const parts = (params.get('view') ?? '').split(',').map(Number);
  const [lon, lat, zoom, pitch = 0, bearing = 0] = parts;
  if (
    parts.length < 3 ||
    parts.some(Number.isNaN) ||
    lon === undefined ||
    lat === undefined ||
    zoom === undefined
  ) {
    return undefined;
  }
  return { center: [lon, lat], zoom, pitch, bearing };
}
