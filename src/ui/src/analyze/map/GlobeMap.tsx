/**
 * The globe: the night Earth (basemap.ts) in a sky of stars (starfield.ts), with the events as twinkling lights
 * (LightsLayer.ts) above it.
 *
 * The component owns the map. It reports where the map looks once a move ends (onLookChange) and draws the lights it
 * is given; a new set fades in over the old one. Hovering a light names it; clicking an event picks it (onPick),
 * clicking a light of many events flies into it. LIVE's playhead goes straight to the lights layer through the
 * controller, frame by frame, without rendering React.
 */
import { AttributionControl, Map as MapLibre, setWorkerUrl } from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url';
import { useEffect, useRef, useState } from 'react';
import type { BBox } from '../api/query';
import { useReducedMotion } from '../lib/useReducedMotion';
import { formatMoment } from '../time/window';
import { basemapStyle, lazySources } from './basemap';
import type { LightSet } from './lights';
import { LightsLayer, type LiveTime } from './LightsLayer';
import { categoryOf, densityColor } from './palette';
import { nearestLight } from './pick';
import { starShifts, starTile, tileSizes } from './starfield';
import styles from './GlobeMap.module.css';

// MapLibre finds its worker by a name it builds at run time, which the bundler cannot see: the worker is bundled
// here and its address given.
setWorkerUrl(workerUrl);

/** Where the map looks. bounds is null while the whole Earth is in view. */
export interface MapLook {
  zoom: number;
  center: [number, number];
  bounds: BBox | null;
}

/** Where the camera is. */
export interface Camera {
  center: [number, number];
  zoom: number;
  pitch?: number;
  bearing?: number;
}

/** What the page can ask of the map. */
export interface GlobeController {
  zoomBy: (delta: number) => void;
  /** Back to the whole Earth, north up, seen from straight above. */
  showWholeEarth: () => void;
  /** North up and the camera straight down, keeping the place. */
  resetNorth: () => void;
  /** LIVE: show the lights by their age at the playhead; null shows them all. */
  setLive: (live: LiveTime | null) => void;
}

/** What the hover label shows. */
interface Hover {
  x: number;
  y: number;
  title: string;
  color: string;
  lines: string[];
}

interface GlobeMapProps {
  /** Where the map opens; the whole Earth when not given. */
  initialView?: Camera;
  /** The lights to draw; null while the first answer is on its way. */
  lights: LightSet | null;
  onLookChange: (look: MapLook) => void;
  /** An event was clicked. */
  onPick?: (id: string) => void;
  /** The map's bearing and pitch, for the compass. */
  onCameraChange?: (camera: { bearing: number; pitch: number }) => void;
  /** The map is drawn and can be steered. */
  onReady?: (controller: GlobeController) => void;
}

/** The radius the glow is drawn at (GlobeMap.module.css), scaled to the globe's. */
const glowRadius = 400;

/** The first view: the whole Earth, Europe, Africa and Asia facing. */
const firstView: Camera = { center: [28, 22], zoom: 2.1 };

export function GlobeMap({
  initialView,
  lights,
  onLookChange,
  onPick,
  onCameraChange,
  onReady,
}: GlobeMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const farRef = useRef<HTMLDivElement>(null);
  const nearRef = useRef<HTMLDivElement>(null);
  const behindRef = useRef<HTMLDivElement>(null);
  const frontRef = useRef<HTMLDivElement>(null);
  const layerRef = useRef<LightsLayer | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [hover, setHover] = useState<Hover | null>(null);
  const reducedMotion = useReducedMotion();
  // The opening view is read once, when the map is made.
  const opening = useRef(initialView);

  // The newest callbacks, read by the map's listeners (set after each render, before the map can call them).
  const callbacks = useRef({ onLookChange, onPick, onCameraChange, onReady });
  useEffect(() => {
    callbacks.current = { onLookChange, onPick, onCameraChange, onReady };
  });

  // Create the map once.
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    const map = new MapLibre({
      container,
      style: basemapStyle({ relief: true }),
      center: opening.current?.center ?? firstView.center,
      zoom: opening.current?.zoom ?? firstView.zoom,
      pitch: opening.current?.pitch ?? 0,
      bearing: opening.current?.bearing ?? 0,
      minZoom: 0.6,
      maxZoom: 13,
      maxPitch: 72,
      attributionControl: false,
      canvasContextAttributes: { antialias: true },
    });
    if (import.meta.env.DEV) {
      // The map in the browser console while developing (and for the screenshot scripts).
      (window as unknown as { analyzeMap?: MapLibre }).analyzeMap = map;
    }
    map.addControl(new AttributionControl({ compact: true }), 'bottom-right');
    const layer = new LightsLayer();
    layerRef.current = layer;

    // The stars: drawn once, then slid as the globe turns.
    const far = farRef.current;
    const near = nearRef.current;
    if (far && near) {
      far.style.backgroundImage = `url(${starTile(tileSizes.far, 520, 7, false)})`;
      near.style.backgroundImage = `url(${starTile(tileSizes.near, 70, 11, true)})`;
    }
    // The air around the Earth: a blue glow behind its rim and a thinner one over its edge, placed where the globe is.
    // Stars and glow move by transforms only, so a moving globe repaints nothing but the map.
    const behind = behindRef.current;
    const front = frontRef.current;
    const slideStars = () => {
      const center = map.getCenter();
      const shifts = starShifts(center.lng, center.lat);
      if (far) far.style.transform = shifts.far;
      if (near) near.style.transform = shifts.near;
      const disc = globeDisc(map);
      for (const glow of [behind, front]) {
        if (!glow) continue;
        glow.style.opacity = disc ? '1' : '0';
        if (disc) {
          glow.style.transform = `translate3d(${disc.x}px, ${disc.y}px, 0) scale(${disc.radius / glowRadius})`;
        }
      }
    };
    slideStars();
    map.on('resize', slideStars);

    const report = () => callbacks.current.onLookChange(lookOf(map));
    const loaded = new Set<string>();
    const loadDetail = () => {
      for (const source of lazySources) {
        const geojson = map.getSource(source.id);
        if (
          map.getZoom() >= source.fromZoom &&
          geojson?.type === 'geojson' &&
          !loaded.has(source.id)
        ) {
          loaded.add(source.id);
          (geojson as unknown as { setData: (data: string) => void }).setData(source.url);
        }
      }
    };

    map.on('load', () => {
      map.addLayer(layer);
      slideStars();
      report();
      loadDetail();
      callbacks.current.onReady?.({
        zoomBy: (delta) => map.easeTo({ zoom: map.getZoom() + delta, duration: 450 }),
        showWholeEarth: () => map.flyTo({ ...firstView, bearing: 0, pitch: 0, duration: 1800 }),
        resetNorth: () => map.easeTo({ bearing: 0, pitch: 0, duration: 600 }),
        setLive: (live) => layer.setLive(live),
      });
    });
    map.on('move', slideStars);
    map.on('moveend', () => {
      report();
      loadDetail();
    });
    const camera = () =>
      callbacks.current.onCameraChange?.({ bearing: map.getBearing(), pitch: map.getPitch() });
    map.on('rotate', camera);
    map.on('pitch', camera);
    map.on('error', (event) => {
      const message = event.error?.message ?? '';
      if (/webgl/i.test(message)) {
        setFailure('This browser cannot draw the 3D map (WebGL 2 is off or unavailable).');
      } else {
        // A style or source problem: worth seeing while developing; the map goes on without that part.
        console.error('Analyze map:', event.error);
      }
    });

    // Hover: the light nearest the pointer, looked for once per frame at most.
    let frame = 0;
    let pointer: { x: number; y: number } | null = null;
    const lightAt = (x: number, y: number) => {
      const set = layer.shown();
      if (!set) return -1;
      const center = map.getCenter();
      return nearestLight(set.places, x, y, (place) => map.project(place), [
        center.lng,
        center.lat,
      ]);
    };
    const look = () => {
      frame = 0;
      const set = layer.shown();
      if (!pointer || !set) return;
      const index = lightAt(pointer.x, pointer.y);
      layer.setHover(index);
      map.getCanvas().style.cursor = index >= 0 ? 'pointer' : '';
      setHover(index >= 0 ? hoverOf(set, index, pointer.x, pointer.y) : null);
    };
    map.on('mousemove', (event) => {
      pointer = event.point;
      if (!frame) frame = requestAnimationFrame(look);
    });
    map.on('movestart', () => setHover(null));
    map.getCanvas().addEventListener('mouseleave', () => {
      pointer = null;
      layer.setHover(-1);
      setHover(null);
    });
    map.on('click', (event) => {
      const set = layer.shown();
      const index = lightAt(event.point.x, event.point.y);
      if (!set || index < 0) return;
      if (set.items[index]) {
        callbacks.current.onPick?.(set.ids[index]!);
      } else {
        map.flyTo({
          center: set.places[index]!,
          zoom: Math.min(map.getZoom() + 2.5, 9),
          duration: 1400,
        });
      }
    });

    return () => {
      cancelAnimationFrame(frame);
      layerRef.current = null;
      map.remove();
    };
  }, []);

  useEffect(() => {
    if (lights) layerRef.current?.setLights(lights);
  }, [lights]);

  useEffect(() => {
    layerRef.current?.setStill(reducedMotion);
  }, [reducedMotion]);

  return (
    <div className={styles.stage}>
      <div ref={farRef} className={styles.stars} />
      <div ref={nearRef} className={styles.stars} data-near="" />
      <div ref={behindRef} className={styles.glowBehind} />
      <div ref={containerRef} className={styles.map} />
      <div ref={frontRef} className={styles.glowFront} />
      {hover && (
        <div
          className={styles.hover}
          style={{ transform: `translate(${hover.x + 16}px, ${hover.y + 16}px)` }}
        >
          <span className={styles.hoverTitle}>
            <i style={{ background: hover.color, boxShadow: `0 0 10px ${hover.color}` }} />
            {hover.title}
          </span>
          {hover.lines.map((line) => (
            <span key={line} className={styles.hoverLine}>
              {line}
            </span>
          ))}
        </div>
      )}
      {failure && <p className={styles.failure}>{failure}</p>}
    </div>
  );
}

/**
 * Where the globe's disc is on the screen: its centre and radius in pixels (the distance to a point a quarter of the
 * way round the Earth from the centre of the view). Null once the Earth fills the screen and no rim is in view.
 */
function globeDisc(map: MapLibre): { x: number; y: number; radius: number } | null {
  if (map.getZoom() > 4.5) return null;
  const center = map.getCenter();
  const middle = map.project(center);
  const lat = center.lat > 0 ? center.lat - 89.5 : center.lat + 89.5;
  const edge = map.project([center.lng, lat]);
  const radius = Math.hypot(edge.x - middle.x, edge.y - middle.y);
  return Number.isFinite(radius) && radius > 10 ? { x: middle.x, y: middle.y, radius } : null;
}

/** Where the map looks: the box in view, or null when it spans (almost) every longitude. */
function lookOf(map: MapLibre): MapLook {
  const bounds = map.getBounds();
  const center = map.getCenter();
  const west = bounds.getWest();
  const east = bounds.getEast();
  const whole = east - west >= 300 || bounds.getNorth() - bounds.getSouth() >= 150;
  return {
    zoom: map.getZoom(),
    center: [center.lng, center.lat],
    bounds: whole
      ? null
      : {
          west: wrapLon(west),
          east: wrapLon(east),
          south: Math.max(-90, bounds.getSouth()),
          north: Math.min(90, bounds.getNorth()),
        },
  };
}

function wrapLon(lon: number): number {
  return ((((lon + 180) % 360) + 360) % 360) - 180;
}

/** The hover label of a light: an event (what, its category, when, where) or a light of many events. */
function hoverOf(set: LightSet, index: number, x: number, y: number): Hover {
  const item = set.items[index];
  if (!item) {
    const count = set.counts[index] ?? 0;
    return {
      x,
      y,
      title: `${count.toLocaleString('en')} ${count === 1 ? 'event' : 'events'} here`,
      color: densityColor,
      lines: ['Click to look closer'],
    };
  }
  const category = categoryOf(item.type);
  const when = item.time?.start ? formatMoment(Date.parse(item.time.start)) : '';
  const where = item.place?.areaName ?? item.place?.country ?? '';
  // A generated label already begins with the type ("type · where · what"): it is not said twice.
  const title =
    item.labelFrom === 'generated' ? item.label.split(' · ').slice(1).join(' · ') : item.label;
  return {
    x,
    y,
    title: title || item.label,
    color: category.color,
    lines: [`${category.name} · ${item.type}`, [when, where].filter(Boolean).join(' · ')].filter(
      Boolean,
    ),
  };
}
