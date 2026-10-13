/**
 * The map's buttons, one column of glass on the right: closer, farther, north up (the needle turns with the map), and
 * the whole Earth. The mouse does the same: scroll to zoom, drag to turn the globe, right-drag (or Ctrl + drag) to
 * tilt and turn the view.
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
      <div className={styles.group}>
        <button
          type="button"
          aria-label="Closer"
          title="Closer"
          onClick={() => controller?.zoomBy(1)}
        >
          <svg viewBox="0 0 20 20" aria-hidden="true">
            <path d="M10 5v10M5 10h10" />
          </svg>
        </button>
        <span className={styles.rule} />
        <button
          type="button"
          aria-label="Farther"
          title="Farther"
          onClick={() => controller?.zoomBy(-1)}
        >
          <svg viewBox="0 0 20 20" aria-hidden="true">
            <path d="M5 10h10" />
          </svg>
        </button>
      </div>
      <button
        type="button"
        className={styles.single}
        aria-label="North up, seen from above"
        title="North up, seen from above"
        data-active={turned || undefined}
        onClick={() => controller?.resetNorth()}
      >
        <svg viewBox="0 0 20 20" aria-hidden="true" style={{ rotate: `${-bearing}deg` }}>
          <path className={styles.north} d="M10 2.5 12.6 10H7.4z" />
          <path className={styles.south} d="M10 17.5 7.4 10h5.2z" />
        </svg>
      </button>
      <button
        type="button"
        className={styles.single}
        aria-label="The whole Earth"
        title="The whole Earth"
        onClick={() => controller?.showWholeEarth()}
      >
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <circle cx="10" cy="10" r="6.5" />
          <ellipse cx="10" cy="10" rx="2.8" ry="6.5" />
          <path d="M3.5 10h13" />
        </svg>
      </button>
    </fieldset>
  );
}
