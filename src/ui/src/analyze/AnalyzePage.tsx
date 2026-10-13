/**
 * Analyze (route /analyze): where the user investigates the data. The globe fills the page; above it, the key (top
 * right), the view buttons (right), and the main timeline, "What happened" (bottom). The time window starts on the last
 * month; ?from=1970-01-01&to=1971-01-01 in the address opens another (the second date excluded), and
 * ?view=35.5,33.4,7 opens the map at a place (longitude, latitude, zoom, and optionally pitch and bearing).
 */
import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { GlobeMap, type Camera, type GlobeController, type MapLook } from './map/GlobeMap';
import { MapControls } from './map/MapControls';
import { MapLegend } from './map/MapLegend';
import { useMapLayer } from './map/useMapLayer';
import { requestOf } from './map/viewport';
import { Timeline } from './time/Timeline';
import { fromDayInput, lastMonth, type TimeWindow } from './time/window';
import styles from './AnalyzePage.module.css';

export function AnalyzePage() {
  const [searchParams] = useSearchParams();
  // The moment the page opened: "today" for the timeline (a page left open over midnight keeps its day).
  const [now] = useState(() => Date.now());
  const [timeWindow, setTimeWindow] = useState<TimeWindow>(() =>
    windowFromAddress(searchParams, now),
  );
  const [initialView] = useState(() => viewFromAddress(searchParams));
  const [look, setLook] = useState<MapLook | null>(null);
  const [camera, setCamera] = useState({ bearing: 0, pitch: 0 });
  const [controller, setController] = useState<GlobeController | null>(null);

  const request = look ? requestOf(look.zoom, look.bounds) : null;
  const { layer, pending, error } = useMapLayer(
    { from: timeWindow.from, to: timeWindow.to },
    request,
  );

  return (
    <div className={styles.page}>
      <GlobeMap
        initialView={initialView}
        layer={layer}
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

      <div className={styles.side}>
        <MapLegend layer={layer} />
        {error && (
          <p className={styles.error} role="alert">
            {error.message}
          </p>
        )}
      </div>

      <div className={styles.controls}>
        <MapControls controller={controller} bearing={camera.bearing} pitch={camera.pitch} />
      </div>

      <div className={styles.bottom}>
        <Timeline
          window={timeWindow}
          onWindowChange={setTimeWindow}
          now={now}
          status={
            pending ? (
              <span className={styles.loading}>Reading…</span>
            ) : layer ? (
              `${layer.events.toLocaleString('en')} on the map`
            ) : null
          }
        />
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
