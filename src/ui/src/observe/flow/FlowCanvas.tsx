/**
 * The 3D canvas of the live flow and its HTML labels. It creates the engine, keeps it sized to its box, stops it
 * when it is out of view, and moves every label to its light after each frame (directly in the DOM: labels move 60
 * times a second, React draws them only when the sources change).
 *
 * Pointing at a light opens its card; clicking it follows that source; double-clicking the scene resets the view.
 */
import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type ReactNode,
  type RefObject,
} from 'react';
import type { SourceInfo } from '../data/catalog';
import { domainById, type DomainId } from '../domains';
import controls from '../ui/controls.module.css';
import { FlowEngine } from './engine/FlowEngine';
import styles from './FlowSection.module.css';

/** What the canvas shows. */
export interface FlowCanvasProps {
  /** The sources on the ring. */
  sources: readonly SourceInfo[];
  /** Records each source sent over the last day: the pylons' heights. */
  volumes: ReadonlyMap<string, number>;
  /** The source being followed (null for none). */
  focus: string | null;
  onFocus: (source: string | null) => void;
  /** Receives the engine once it exists (and null when it is gone). */
  onEngine: (engine: FlowEngine | null) => void;
  /** Whether the canvas is on screen. */
  onVisible: (visible: boolean) => void;
  reducedMotion: boolean;
  /** Draws the card of the source under the pointer. */
  renderCard: (source: SourceInfo) => ReactNode;
  /** Text under the core's label: its current rate. */
  coreCaption: string;
  /** An element over the canvas (the arrivals feed) that labels should keep clear of. */
  avoid?: RefObject<HTMLElement | null>;
}

/** The space a source label takes, in pixels: Fragment Mono at 10.5 px is about 6.3 px a character. */
function labelBox(x: number, y: number, text: string) {
  return { left: x + 10, right: x + 14 + text.length * 6.3, top: y - 8, bottom: y + 8 };
}

type Box = ReturnType<typeof labelBox>;

const overlaps = (a: Box, b: Box) =>
  a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;

/** True when this browser can draw WebGL 2 (checked once, on a throwaway canvas). */
function webglAvailable(): boolean {
  try {
    return !!document.createElement('canvas').getContext('webgl2');
  } catch {
    return false;
  }
}

/** The 3D canvas and its labels. */
export function FlowCanvas(props: FlowCanvasProps) {
  const {
    sources,
    volumes,
    focus,
    onFocus,
    onEngine,
    onVisible,
    reducedMotion,
    renderCard,
    coreCaption,
    avoid,
  } = props;
  const box = useRef<HTMLElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const engineRef = useRef<FlowEngine | null>(null);
  const labels = useRef(new Map<string, HTMLElement>());
  const sectorLabels = useRef(new Map<DomainId, HTMLElement>());
  const coreLabel = useRef<HTMLDivElement>(null);
  const card = useRef<HTMLDivElement>(null);
  const [hovered, setHovered] = useState<string | null>(null);
  const hoveredRef = useRef<string | null>(null);
  const [failed, setFailed] = useState(() => !webglAvailable());
  // The newest callbacks and the area to avoid, for the engine's long-lived listeners.
  const callbacks = useRef({ onEngine, onVisible, avoid });
  useLayoutEffect(() => {
    callbacks.current = { onEngine, onVisible, avoid };
  });

  // The engine lives as long as the canvas.
  useEffect(() => {
    const element = canvas.current;
    const container = box.current;
    if (!element || !container) {
      return;
    }
    let engine: FlowEngine;
    try {
      engine = new FlowEngine({ canvas: element, reducedMotion, onFrame: () => placeLabels() });
    } catch {
      // WebGL was offered but refused (a lost context, a blocked driver): say so in place of the scene.
      queueMicrotask(() => setFailed(true));
      return;
    }
    engineRef.current = engine;
    callbacks.current.onEngine(engine);

    const placeLabels = () => {
      // Labels are placed nearest first; one that would overlap a placed label, or the avoided area, fades out
      // until the turning camera gives it room again.
      const container = box.current?.getBoundingClientRect();
      const avoided = callbacks.current.avoid?.current?.getBoundingClientRect();
      const blocked: Box[] =
        avoided && container
          ? [
              {
                left: avoided.left - container.left - 12,
                right: avoided.right - container.left + 12,
                top: avoided.top - container.top - 12,
                bottom: avoided.bottom - container.top + 12,
              },
            ]
          : [];
      const placed: { id: string; label: HTMLElement; x: number; y: number; depth: number }[] = [];
      for (const [id, label] of labels.current) {
        const point = engine.projectSource(id);
        if (!point || !point.visible) {
          label.style.opacity = '0';
          continue;
        }
        placed.push({ id, label, x: point.x, y: point.y, depth: point.depth });
      }
      placed.sort((a, b) =>
        a.id === hoveredRef.current ? -1 : b.id === hoveredRef.current ? 1 : a.depth - b.depth,
      );
      for (const item of placed) {
        const area = labelBox(item.x, item.y, item.label.textContent ?? '');
        const strong = item.id === hoveredRef.current;
        const free = strong || !blocked.some((other) => overlaps(area, other));
        // Nearer lights read clearly; the far side of the ring recedes.
        const near = Math.min(1, Math.max(0, (46 - item.depth) / 18));
        item.label.style.opacity = free ? String(strong ? 1 : 0.3 + near * 0.6) : '0';
        item.label.style.transform = `translate3d(${item.x}px, ${item.y}px, 0)`;
        if (free) {
          blocked.push(area);
        }
      }
      for (const sector of engine.ring.sectors) {
        const label = sectorLabels.current.get(sector.domain);
        if (!label) {
          continue;
        }
        const point = engine.projectGround(11.75, (sector.start + sector.end) / 2);
        label.style.opacity = point.visible
          ? String(Math.min(1, Math.max(0.2, (40 - point.depth) / 18)))
          : '0';
        label.style.transform = `translate3d(${point.x}px, ${point.y}px, 0)`;
      }
      const core = engine.projectCore();
      if (coreLabel.current) {
        coreLabel.current.style.transform = `translate3d(${core.x}px, ${core.y}px, 0)`;
      }
      const cardElement = card.current;
      const hoveredId = hoveredRef.current;
      if (cardElement && hoveredId) {
        const point = engine.projectSource(hoveredId);
        if (point) {
          cardElement.style.transform = `translate3d(${point.x}px, ${point.y}px, 0)`;
        }
      }
    };

    const resize = new ResizeObserver(([entry]) => {
      if (entry) {
        engine.resize(entry.contentRect.width, entry.contentRect.height);
      }
    });
    resize.observe(container);
    let inView = false;
    const update = () => {
      const running = inView && document.visibilityState === 'visible';
      engine.setRunning(running);
      callbacks.current.onVisible(inView);
    };
    const view = new IntersectionObserver(([entry]) => {
      inView = !!entry?.isIntersecting;
      update();
    });
    view.observe(container);
    document.addEventListener('visibilitychange', update);
    return () => {
      resize.disconnect();
      view.disconnect();
      document.removeEventListener('visibilitychange', update);
      callbacks.current.onEngine(null);
      engineRef.current = null;
      engine.dispose();
    };
  }, [reducedMotion]);

  useEffect(() => {
    engineRef.current?.setSources(
      sources.map((source) => ({ id: source.id, domain: source.domain })),
    );
  }, [sources]);

  useEffect(() => {
    engineRef.current?.setVolumes(volumes);
  }, [volumes, sources]);

  useEffect(() => {
    engineRef.current?.setFocused(focus);
  }, [focus]);

  const pointAt = (x: number, y: number) => {
    const id = engineRef.current?.pick(x, y) ?? null;
    if (id !== hoveredRef.current) {
      hoveredRef.current = id;
      engineRef.current?.setHovered(id);
      setHovered(id);
    }
  };

  const hoveredSource = hovered ? sources.find((source) => source.id === hovered) : undefined;
  const sectorDomains = [...new Set(sources.map((source) => source.domain))];

  if (failed) {
    return (
      <div className={styles.noGl}>
        <p>This browser cannot draw the 3D scene (WebGL is off or unavailable).</p>
        <p>The arrivals, the readouts and the time-travel bar still work.</p>
      </div>
    );
  }

  return (
    <figure ref={box} className={styles.canvasBox}>
      <figcaption className={controls.srOnly}>
        A 3D view of the sources standing on a ring around the API core, grouped by domain. Each
        batch flies as an arc from its source to the core; refused records spark back. The arrivals
        list and the readouts give the same information as text.
      </figcaption>
      <canvas
        ref={canvas}
        className={styles.canvas}
        data-hovering={hovered ? '' : undefined}
        onPointerMove={(event) => {
          const rect = event.currentTarget.getBoundingClientRect();
          pointAt(event.clientX - rect.left, event.clientY - rect.top);
        }}
        onPointerLeave={() => pointAt(-1000, -1000)}
        onClick={() => {
          if (hoveredRef.current) {
            onFocus(hoveredRef.current === focus ? null : hoveredRef.current);
          }
        }}
        onDoubleClick={() => {
          if (!hoveredRef.current) {
            engineRef.current?.resetView();
          }
        }}
        aria-hidden="true"
      />
      <div className={styles.labels} aria-hidden="true">
        {sectorDomains.map((domain) => (
          <span
            key={domain}
            className={styles.sectorLabel}
            ref={(element) => {
              if (element) {
                sectorLabels.current.set(domain, element);
              } else {
                sectorLabels.current.delete(domain);
              }
            }}
          >
            {domainById(domain).name}
          </span>
        ))}
        {sources.map((source) => (
          <span
            key={source.id}
            className={styles.sourceLabel}
            data-dim={!volumes.get(source.id) || undefined}
            data-focus={focus === source.id || undefined}
            ref={(element) => {
              if (element) {
                labels.current.set(source.id, element);
              } else {
                labels.current.delete(source.id);
              }
            }}
          >
            {source.short}
          </span>
        ))}
        <div ref={coreLabel} className={styles.coreLabel}>
          <span className={styles.coreName}>Synapsis API</span>
          <span className={styles.coreCaption}>{coreCaption}</span>
        </div>
      </div>
      <div ref={card} className={styles.cardAnchor} data-open={hoveredSource ? '' : undefined}>
        {hoveredSource ? renderCard(hoveredSource) : null}
      </div>
    </figure>
  );
}
