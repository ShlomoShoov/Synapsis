/**
 * The schema map: a frame onto the plane of package cards, which moves under a camera (camera.ts).
 *
 * - Drag anywhere to move; Ctrl or ⌘ with the wheel (or a trackpad pinch) zooms around the pointer, so the plain
 *   wheel still scrolls the page; double-click zooms in; the buttons zoom and fit.
 * - Far out (below camera.readableScale) the cards show their shape and the package names float above them.
 * - A focused row is brought into view, so the keyboard can travel the map.
 * The camera starts fitted to the whole map; `ref` lets the section fit, zoom and show a package or definition.
 */
import {
  memo,
  useEffect,
  useImperativeHandle,
  useLayoutEffect,
  useRef,
  useState,
  type CSSProperties,
  type Ref,
} from 'react';
import { domainById } from '../domains';
import { useElementSize } from '../lib/useElementSize';
import { useReducedMotion } from '../lib/useReducedMotion';
import { Icon } from '../ui/Icon';
import {
  between,
  centreOn,
  fitBox,
  panBy,
  readableScale,
  toScreen,
  zoomAt,
  type Camera,
} from './camera';
import type { Highlight, Target } from './focus';
import type { SchemaGraph } from './graph';
import type { MapLayout } from './layout';
import { PackageCard, type SourceLive } from './PackageCard';
import styles from './SchemaMap.module.css';
import { Threads } from './Threads';

/** What the section can ask of the map. */
export interface MapHandle {
  fit: () => void;
  zoomBy: (factor: number) => void;
  /** Brings a package or a definition to the middle, close enough to read. */
  show: (target: Target) => void;
}

/** The props of the map. */
export interface SchemaMapProps {
  graph: SchemaGraph;
  layout: MapLayout;
  highlight: Highlight;
  selected: Target | null;
  live: ReadonlyMap<string, SourceLive>;
  /** Pixels on the right covered by the inspector. */
  coveredRight: number;
  onSelect: (target: Target) => void;
  onHover: (target: Target | null) => void;
  onToggle: (name: string) => void;
  ref?: Ref<MapHandle>;
}

/** The plane: domain groups, threads and cards (memoised: moving the camera does not draw it again). */
const World = memo(function World({
  graph,
  layout,
  highlight,
  selected,
  live,
  onSelect,
  onHover,
  onToggle,
}: Omit<SchemaMapProps, 'coveredRight' | 'ref'>) {
  return (
    <>
      {layout.groups.map((group) => {
        const domain = domainById(group.domain);
        const dim =
          highlight.cards !== null && !group.members.some((name) => highlight.cards?.has(name));
        return (
          <div
            key={`${group.side}:${group.domain}:${group.x}`}
            className={styles.group}
            data-dim={dim || undefined}
            style={
              {
                left: group.x,
                top: group.y,
                width: group.width,
                height: group.height,
                '--domain': domain.color,
              } as CSSProperties
            }
          >
            <p className={styles.groupLabel}>
              {domain.name}
              <span className={styles.groupCount}>{group.members.length}</span>
            </p>
          </div>
        );
      })}
      <Threads graph={graph} layout={layout} highlight={highlight} />
      {layout.cards.map((card) => {
        const pkg = graph.packageByName.get(card.name);
        return pkg ? (
          <PackageCard
            key={card.name}
            card={card}
            pkg={pkg}
            graph={graph}
            hub={card.side === 'hub'}
            dim={highlight.cards !== null && !highlight.cards.has(card.name)}
            rows={highlight.rows}
            selected={selected}
            live={live}
            badge={null}
            onSelect={onSelect}
            onHover={onHover}
            onToggle={onToggle}
          />
        ) : null;
      })}
    </>
  );
});

/** The size of the dot grid on screen: about 28 map pixels, kept between 14 and 56 screen pixels. */
function gridSize(k: number): number {
  let size = 28 * k;
  while (size < 14) {
    size *= 2;
  }
  while (size > 56) {
    size /= 2;
  }
  return size;
}

/** The map. */
export function SchemaMap({ ref, coveredRight, ...world }: SchemaMapProps) {
  const { layout } = world;
  const [frame, setFrame] = useState<HTMLDivElement | null>(null);
  const size = useElementSize(frame);
  const reducedMotion = useReducedMotion();
  const [camera, setCamera] = useState<Camera | null>(null);
  const [touched, setTouched] = useState(false);
  const view = { width: size.width, height: size.height, coveredRight };
  // Until the viewer moves it, the camera is fitted to the whole map.
  const shown = camera ?? (size.width > 0 ? fitBox(layout.bounds, view, 28, 0.9) : null);
  const latest = useRef<Camera | null>(null);
  const flight = useRef(0);
  useLayoutEffect(() => {
    latest.current = shown;
  });

  /** Moves the camera to `to`, smoothly unless motion is reduced. */
  const fly = (to: Camera) => {
    cancelAnimationFrame(flight.current);
    const from = latest.current;
    if (!from || reducedMotion) {
      setCamera(to);
      return;
    }
    const start = performance.now();
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / 460);
      const eased = t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
      setCamera(between(from, to, eased));
      if (t < 1) {
        flight.current = requestAnimationFrame(step);
      }
    };
    flight.current = requestAnimationFrame(step);
  };
  // The gesture listeners are set once per frame element; they reach the latest camera and `fly` through refs.
  const flyRef = useRef(fly);
  const covered = useRef(coveredRight);
  useLayoutEffect(() => {
    flyRef.current = fly;
    covered.current = coveredRight;
  });
  // A flight in progress ends with the map, not when the inspector opens.
  useEffect(() => () => cancelAnimationFrame(flight.current), []);

  const middle = () => ({ x: (view.width - coveredRight) / 2, y: view.height / 2 });
  const fit = () => fly(fitBox(layout.bounds, view, 28, 0.9));
  const zoomBy = (factor: number) => {
    const current = latest.current;
    if (current) {
      fly(zoomAt(current, factor, middle()));
    }
  };
  const show = (target: Target) => {
    const current = latest.current;
    const name =
      target.type === 'package' ? target.name : world.graph.definitions.get(target.id)?.packageName;
    const card = name ? layout.cardOf.get(name) : undefined;
    if (!current || !card) {
      return;
    }
    const y =
      target.type === 'definition'
        ? (card.anchors.get(target.id) ?? card.height / 2)
        : card.height / 2;
    const close =
      target.type === 'definition' ? 0.95 : Math.min(0.85, (view.height - 80) / card.height);
    fly(centreOn({ x: card.x + card.width / 2, y: card.y + y }, view, Math.max(current.k, close)));
  };
  useImperativeHandle(ref, () => ({ fit, zoomBy, show }));

  // Gestures: native listeners, so a Ctrl + wheel zoom can stop the browser's own page zoom.
  useEffect(() => {
    if (!frame) {
      return;
    }
    let drag: { id: number; x: number; y: number; moved: boolean } | null = null;
    let swallowClick = false;
    const move = (update: (camera: Camera) => Camera) => {
      cancelAnimationFrame(flight.current);
      setTouched(true);
      setCamera((camera) => {
        const base = camera ?? latest.current;
        return base ? update(base) : camera;
      });
    };
    const local = (event: { clientX: number; clientY: number }) => {
      const box = frame.getBoundingClientRect();
      return { x: event.clientX - box.left, y: event.clientY - box.top };
    };
    const onPointerDown = (event: PointerEvent) => {
      if (event.button === 0 && !(event.target as Element).closest('[data-map-ui]')) {
        drag = { id: event.pointerId, x: event.clientX, y: event.clientY, moved: false };
      }
    };
    const onPointerMove = (event: PointerEvent) => {
      if (!drag || event.pointerId !== drag.id) {
        return;
      }
      const dx = event.clientX - drag.x;
      const dy = event.clientY - drag.y;
      if (!drag.moved && Math.hypot(dx, dy) < 4) {
        return;
      }
      if (!drag.moved) {
        drag.moved = true;
        frame.setPointerCapture(event.pointerId);
        frame.setAttribute('data-panning', '');
      }
      drag.x = event.clientX;
      drag.y = event.clientY;
      move((camera) => panBy(camera, dx, dy));
    };
    const onPointerUp = (event: PointerEvent) => {
      if (drag && event.pointerId === drag.id) {
        swallowClick = drag.moved;
        frame.removeAttribute('data-panning');
        drag = null;
      }
    };
    // A drag that started on a row must not also select it.
    const onClick = (event: MouseEvent) => {
      if (swallowClick) {
        swallowClick = false;
        event.stopPropagation();
        event.preventDefault();
      }
    };
    const onWheel = (event: WheelEvent) => {
      if (!(event.ctrlKey || event.metaKey)) {
        return;
      }
      event.preventDefault();
      const factor = Math.exp(-event.deltaY * (event.deltaMode === 1 ? 0.05 : 0.0022));
      const point = local(event);
      move((camera) => zoomAt(camera, factor, point));
    };
    const onDoubleClick = (event: MouseEvent) => {
      if ((event.target as Element).closest('button, [data-map-ui]')) {
        return;
      }
      const current = latest.current;
      if (current) {
        setTouched(true);
        flyRef.current(zoomAt(current, 1.8, local(event)));
      }
    };
    // Keyboard travel: a focused card or row is brought into view.
    const onFocusIn = (event: FocusEvent) => {
      const element = event.target as Element;
      if (!element.closest('[data-card]')) {
        return;
      }
      const box = frame.getBoundingClientRect();
      const rect = element.getBoundingClientRect();
      const margin = 40;
      const right = box.right - covered.current;
      const dx =
        rect.right > right - margin
          ? right - margin - rect.right
          : rect.left < box.left + margin
            ? box.left + margin - rect.left
            : 0;
      const dy =
        rect.bottom > box.bottom - margin
          ? box.bottom - margin - rect.bottom
          : rect.top < box.top + margin
            ? box.top + margin - rect.top
            : 0;
      if (dx || dy) {
        move((camera) => panBy(camera, dx, dy));
      }
    };
    frame.addEventListener('pointerdown', onPointerDown);
    frame.addEventListener('pointermove', onPointerMove);
    frame.addEventListener('pointerup', onPointerUp);
    frame.addEventListener('pointercancel', onPointerUp);
    frame.addEventListener('click', onClick, true);
    frame.addEventListener('wheel', onWheel, { passive: false });
    frame.addEventListener('dblclick', onDoubleClick);
    frame.addEventListener('focusin', onFocusIn);
    return () => {
      frame.removeEventListener('pointerdown', onPointerDown);
      frame.removeEventListener('pointermove', onPointerMove);
      frame.removeEventListener('pointerup', onPointerUp);
      frame.removeEventListener('pointercancel', onPointerUp);
      frame.removeEventListener('click', onClick, true);
      frame.removeEventListener('wheel', onWheel);
      frame.removeEventListener('dblclick', onDoubleClick);
      frame.removeEventListener('focusin', onFocusIn);
    };
  }, [frame]);

  const far = shown ? shown.k < readableScale : true;
  const grid = shown ? gridSize(shown.k) : 28;
  const frameStyle: CSSProperties = shown
    ? {
        backgroundSize: `${grid}px ${grid}px`,
        backgroundPosition: `${shown.tx % grid}px ${shown.ty % grid}px`,
      }
    : {};

  return (
    <div ref={setFrame} className={styles.frame} style={frameStyle}>
      {shown ? (
        <div
          className={styles.world}
          data-far={far || undefined}
          style={{ transform: `translate(${shown.tx}px, ${shown.ty}px) scale(${shown.k})` }}
        >
          <World {...world} />
        </div>
      ) : null}

      {shown && far ? (
        <div className={styles.labels} aria-hidden="true">
          {layout.groups.map((group) => {
            const at = toScreen(shown, { x: group.x + 14, y: group.y + 12 });
            return (
              <span
                key={`${group.side}:${group.domain}:${group.x}`}
                className={styles.floatGroup}
                style={
                  {
                    left: at.x,
                    top: at.y,
                    '--domain': domainById(group.domain).color,
                  } as CSSProperties
                }
              >
                {domainById(group.domain).short}
              </span>
            );
          })}
          {layout.cards.map((card) => {
            const at = toScreen(shown, { x: card.x + 8, y: card.y + 10 });
            return (
              <span
                key={card.name}
                className={styles.floatLabel}
                data-hub={card.side === 'hub' || undefined}
                data-dim={
                  (world.highlight.cards !== null && !world.highlight.cards.has(card.name)) ||
                  undefined
                }
                style={{ left: at.x, top: at.y, maxWidth: Math.max(60, card.width * shown.k + 40) }}
              >
                {card.name}
              </span>
            );
          })}
        </div>
      ) : null}

      <p className={styles.hint} data-gone={touched || undefined}>
        <span>
          <b>Drag</b> to move
        </span>
        <span>
          <b>Ctrl / ⌘ + scroll</b> or pinch to zoom
        </span>
        <span>
          <b>Click</b> a row for its details
        </span>
      </p>

      <div className={styles.instruments} style={{ right: 16 + coveredRight }} data-map-ui="">
        <div className={styles.zoom}>
          <button
            type="button"
            className={styles.zoomButton}
            onClick={() => zoomBy(1 / 1.4)}
            aria-label="Zoom out"
            title="Zoom out (−)"
          >
            <Icon name="minus" size={14} />
          </button>
          <button
            type="button"
            className={styles.zoomButton}
            onClick={() => zoomBy(1 / (latest.current?.k ?? 1))}
            title="Actual size"
          >
            {shown ? `${Math.round(shown.k * 100)}%` : '—'}
          </button>
          <button
            type="button"
            className={styles.zoomButton}
            onClick={() => zoomBy(1.4)}
            aria-label="Zoom in"
            title="Zoom in (+)"
          >
            <Icon name="plus" size={14} />
          </button>
          <button
            type="button"
            className={styles.zoomButton}
            onClick={fit}
            aria-label="Fit the whole map"
            title="Fit the whole map (0)"
          >
            <Icon name="fit" size={14} />
          </button>
        </div>
      </div>
    </div>
  );
}
