/**
 * The map's buttons, on the right edge: closer, farther, north up (the needle turns with the map), and the whole
 * Earth. The mouse does the same: scroll to zoom, drag to move, right-drag (or Ctrl + drag) to turn and tilt.
 */
import type { GlobeController } from './GlobeMap';
import styles from './MapControls.module.css';

interface MapControlsProps {
  controller: GlobeController | null;
  bearing: number;
  pitch: number;
}

export function MapControls({ controller, bearing, pitch }: MapControlsProps) {
  const turned = Math.abs(bearing) > 0.5 || pitch > 0.5;
  return (
    <fieldset className={styles.controls}>
      <legend className={styles.hidden}>Map view</legend>
      <button
        type="button"
        aria-label="Zoom in"
        title="Zoom in"
        onClick={() => controller?.zoomBy(1)}
      >
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path d="M10 4v12M4 10h12" />
        </svg>
      </button>
      <button
        type="button"
        aria-label="Zoom out"
        title="Zoom out"
        onClick={() => controller?.zoomBy(-1)}
      >
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path d="M4 10h12" />
        </svg>
      </button>
      <button
        type="button"
        aria-label="North up, seen from above"
        title="North up, seen from above"
        data-active={turned || undefined}
        onClick={() => controller?.resetNorth()}
      >
        <svg viewBox="0 0 20 20" aria-hidden="true" style={{ rotate: `${-bearing}deg` }}>
          <path className={styles.north} d="M10 2.5 13 10H7z" />
          <path d="M10 17.5 7 10h6z" />
        </svg>
      </button>
      <button
        type="button"
        aria-label="The whole Earth"
        title="The whole Earth"
        onClick={() => controller?.showWholeEarth()}
      >
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <circle cx="10" cy="10" r="7" />
          <ellipse cx="10" cy="10" rx="3" ry="7" />
          <path d="M3 10h14" />
        </svg>
      </button>
    </fieldset>
  );
}
