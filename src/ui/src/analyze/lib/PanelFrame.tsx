/**
 * A left side panel's frame: its right edge drags to resize it (arrow keys too; a double click gives back the first
 * width), and the button on that edge maximises it to the whole screen and back (Esc restores). The width is kept in
 * this browser. The panel inside takes its width from --panel-width.
 */
import {
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type PointerEvent as ReactPointerEvent,
  type ReactNode,
} from 'react';
import styles from './PanelFrame.module.css';

interface PanelFrameProps {
  /** Names the kept width. */
  name: string;
  /** What the panel is, for the controls' labels. */
  label: string;
  width: number;
  minWidth?: number;
  /** The panel fills its row's height (the query panel). */
  fill?: boolean;
  children: ReactNode;
}

const keyOf = (name: string) => `synapsis.analyze.panel.${name}`;

function keptWidth(name: string): number | null {
  try {
    const kept = Number(localStorage.getItem(keyOf(name)));
    return Number.isFinite(kept) && kept > 0 ? kept : null;
  } catch {
    return null;
  }
}

function keepWidth(name: string, width: number) {
  try {
    localStorage.setItem(keyOf(name), String(Math.round(width)));
  } catch {
    // Not kept: the panel opens at its first width next time.
  }
}

export function PanelFrame({
  name,
  label,
  width: first,
  minWidth = 220,
  fill,
  children,
}: PanelFrameProps) {
  const [width, setWidth] = useState(() => keptWidth(name) ?? first);
  const [maximised, setMaximised] = useState(false);
  const frameRef = useRef<HTMLDivElement>(null);
  const widest = () => Math.max(minWidth, window.innerWidth - 120);
  const resize = (next: number) => {
    const clamped = Math.round(Math.min(Math.max(next, minWidth), widest()));
    setWidth(clamped);
    keepWidth(name, clamped);
  };

  // Esc restores a maximised panel (before the page's own Esc, which closes the object).
  useEffect(() => {
    if (!maximised) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      event.stopPropagation();
      setMaximised(false);
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [maximised]);

  const onPointerDown = (event: ReactPointerEvent<HTMLButtonElement>) => {
    if (event.button !== 0 || event.target !== event.currentTarget) return;
    event.preventDefault();
    const handle = event.currentTarget;
    const startX = event.clientX;
    const startWidth = frameRef.current?.getBoundingClientRect().width ?? width;
    handle.setPointerCapture(event.pointerId);
    const move = (moved: PointerEvent) => resize(startWidth + moved.clientX - startX);
    const end = () => {
      handle.removeEventListener('pointermove', move);
      handle.removeEventListener('pointerup', end);
      handle.removeEventListener('pointercancel', end);
    };
    handle.addEventListener('pointermove', move);
    handle.addEventListener('pointerup', end);
    handle.addEventListener('pointercancel', end);
  };

  return (
    <div
      ref={frameRef}
      className={styles.frame}
      data-max={maximised || undefined}
      data-fill={fill || undefined}
      style={{ '--panel-width': maximised ? '100%' : `${width}px` } as CSSProperties}
    >
      {children}
      {!maximised && (
        <button
          type="button"
          className={styles.handle}
          aria-label={`Resize ${label} (arrow keys)`}
          title="Drag to resize; double-click for the first width"
          onPointerDown={onPointerDown}
          onDoubleClick={() => resize(first)}
          onKeyDown={(event) => {
            if (event.key === 'ArrowLeft') resize(width - 24);
            else if (event.key === 'ArrowRight') resize(width + 24);
          }}
        />
      )}
      <button
        type="button"
        className={styles.max}
        onClick={() => setMaximised((on) => !on)}
        aria-label={maximised ? `Restore ${label} (Esc)` : `Maximise ${label}`}
        title={maximised ? 'Restore (Esc)' : 'Full screen'}
      >
        <svg viewBox="0 0 20 20" aria-hidden="true">
          {maximised ? (
            <path d="M8 3v5H3M12 17v-5h5M8 8 3 3M12 12l5 5" />
          ) : (
            <path d="M3 8V3h5M17 12v5h-5M3 3l5 5M17 17l-5-5" />
          )}
        </svg>
      </button>
    </div>
  );
}
