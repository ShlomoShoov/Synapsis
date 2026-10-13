/**
 * The globe: MapLibre with the basemap (basemap.ts) and the events (layers.ts) above it.
 *
 * The component owns the map. It reports where the map looks once a move ends (onViewChange) and draws the layer it is
 * given: hexagon columns change height smoothly from one answer to the next, points fade over. Hovering a column or a
 * point names it; clicking a column flies into it, clicking a point picks it (onPick).
 */
import {
  AttributionControl,
  Map as MapLibre,
  setWorkerUrl,
  type MapGeoJSONFeature,
} from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url';
import { useEffect, useRef, useState } from 'react';
import type { MapLayer } from '../api/model';
import { formatMoment } from '../time/window';
import type { BBox } from '../api/query';
import { useReducedMotion } from '../lib/useReducedMotion';
import { basemapStyle, firstLabelLayer, lazySources } from './basemap';
import {
  cellLayers,
  cellSource,
  pointLayers,
  pointOpacity,
  pointSources,
  type PointSource,
} from './layers';
import { blendColumns, columnFeatures, columnsOf, pointFeatures, type Column } from './overlay';
import styles from './GlobeMap.module.css';

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
}

/** What the hover label shows. */
interface Hover {
  x: number;
  y: number;
  title: string;
  detail: string;
}

interface GlobeMapProps {
  /** Where the map opens; the whole Earth when not given. */
  initialView?: Camera;
  /** The events to draw; undefined while the first answer is on its way. */
  layer: MapLayer | undefined;
  onLookChange: (look: MapLook) => void;
  /** A point was clicked. */
  onPick?: (id: string) => void;
  /** The map's bearing and pitch, for the compass. */
  onCameraChange?: (camera: { bearing: number; pitch: number }) => void;
  /** The map is drawn and can be steered. */
  onReady?: (controller: GlobeController) => void;
}

// MapLibre finds its worker by a name it builds at run time, which the bundler cannot see: the worker is bundled
// here and its address given.
setWorkerUrl(workerUrl);

/** The first view: the whole Earth, Europe, Africa and Asia facing. */
const firstView: Camera = { center: [24, 24], zoom: 2.05 };

/** How long the columns take to reach a new answer. */
const morphMs = 650;

export function GlobeMap({
  initialView,
  layer,
  onLookChange,
  onPick,
  onCameraChange,
  onReady,
}: GlobeMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MapLibre | null>(null);
  const [ready, setReady] = useState(false);
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
    mapRef.current = map;
    map.addControl(new AttributionControl({ compact: true }), 'bottom-left');
    // The timeline covers the bottom of the page: the Earth's centre sits in the free part above it.
    map.setPadding({ top: 0, bottom: 150, left: 0, right: 0 });

    const report = () => callbacks.current.onLookChange(lookOf(map));
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
    const loaded = new Set<string>();

    map.on('load', () => {
      addEventLayers(map);
      setReady(true);
      report();
      loadDetail();
      callbacks.current.onReady?.({
        zoomBy: (delta) => map.easeTo({ zoom: map.getZoom() + delta, duration: 400 }),
        showWholeEarth: () => map.flyTo({ ...firstView, bearing: 0, pitch: 0, duration: 1800 }),
        resetNorth: () => map.easeTo({ bearing: 0, pitch: 0, duration: 600 }),
      });
    });
    map.on('moveend', () => {
      report();
      loadDetail();
    });
    map.on('rotate', () =>
      callbacks.current.onCameraChange?.({ bearing: map.getBearing(), pitch: map.getPitch() }),
    );
    map.on('pitch', () =>
      callbacks.current.onCameraChange?.({ bearing: map.getBearing(), pitch: map.getPitch() }),
    );
    map.on('error', (event) => {
      const message = event.error?.message ?? '';
      if (/webgl/i.test(message)) {
        setFailure('This browser cannot draw the 3D map (WebGL 2 is off or unavailable).');
      } else {
        // A style or source problem: worth seeing while developing; the map goes on without that part.
        console.error('Analyze map:', event.error);
      }
    });

    // Hover: name what is under the pointer and light it.
    let lit: { source: string; id: string | number } | null = null;
    const light = (feature: MapGeoJSONFeature | null) => {
      if (lit) map.setFeatureState(lit, { hover: false });
      lit =
        feature?.id !== undefined && feature ? { source: feature.source, id: feature.id } : null;
      if (lit) map.setFeatureState(lit, { hover: true });
    };
    const pickable = () =>
      ['cells-columns', ...pointSources.map((source) => `${source}-dot`)].filter((id) =>
        map.getLayer(id),
      );
    map.on('mousemove', (event) => {
      const [feature] = map.queryRenderedFeatures(event.point, { layers: pickable() });
      light(feature ?? null);
      map.getCanvas().style.cursor = feature ? 'pointer' : '';
      setHover(feature ? hoverOf(feature, event.point.x, event.point.y) : null);
    });
    map.getCanvas().addEventListener('mouseleave', () => {
      light(null);
      setHover(null);
    });
    map.on('click', (event) => {
      const [feature] = map.queryRenderedFeatures(event.point, { layers: pickable() });
      if (!feature) return;
      if (feature.source === cellSource) {
        const center: [number, number] = [
          Number(feature.properties.lon),
          Number(feature.properties.lat),
        ];
        map.flyTo({ center, zoom: Math.min(map.getZoom() + 2.5, 9), duration: 1400 });
      } else {
        callbacks.current.onPick?.(String(feature.properties.id));
      }
    });

    return () => {
      mapRef.current = null;
      map.remove();
    };
  }, []);

  // Draw each new layer: columns change height, points fade over.
  const shown = useRef<{ columns: Column[]; pointSource: PointSource | null }>({
    columns: [],
    pointSource: null,
  });
  useEffect(() => {
    const map = mapRef.current;
    if (!ready || !map || !layer) return;
    const scaleMax = layer.cells.reduce((max, cell) => Math.max(max, cell.count), 1);
    const target = layer.layer === 'cells' ? columnsOf(layer.cells, scaleMax) : [];
    const from = shown.current.columns;
    const cells = map.getSource(cellSource) as unknown as { setData: (data: unknown) => void };

    let frame = 0;
    const started = performance.now();
    const step = (now: number) => {
      const t = reducedMotion ? 1 : Math.min(1, (now - started) / morphMs);
      const columns = blendColumns(from, target, easeInOut(t));
      shown.current.columns = columns;
      cells.setData(columnFeatures(columns));
      if (t < 1) frame = requestAnimationFrame(step);
    };
    frame = requestAnimationFrame(step);

    // Points: fill the set not shown, then swap which one is visible.
    const next: PointSource =
      shown.current.pointSource === pointSources[0] ? pointSources[1] : pointSources[0];
    const previous = shown.current.pointSource;
    (map.getSource(next) as unknown as { setData: (data: unknown) => void }).setData(
      pointFeatures(layer.layer === 'points' ? layer.points : []),
    );
    setPointOpacity(map, next, true);
    if (previous) setPointOpacity(map, previous, false);
    shown.current.pointSource = next;

    return () => cancelAnimationFrame(frame);
  }, [layer, ready, reducedMotion]);

  return (
    <div className={styles.stage}>
      <div ref={containerRef} className={styles.map} />
      {hover && (
        <div
          className={styles.hover}
          style={{ transform: `translate(${hover.x + 14}px, ${hover.y + 14}px)` }}
        >
          <strong>{hover.title}</strong>
          <span>{hover.detail}</span>
        </div>
      )}
      {failure && <p className={styles.failure}>{failure}</p>}
    </div>
  );
}

/** Adds the sources and layers of the events, above the basemap. */
function addEventLayers(map: MapLibre) {
  const empty = { type: 'FeatureCollection' as const, features: [] };
  // Under the labels, so the names of places stay readable over the columns.
  map.addSource(cellSource, { type: 'geojson', data: empty, promoteId: 'id' });
  for (const layer of cellLayers()) map.addLayer(layer, firstLabelLayer);
  for (const source of pointSources) {
    map.addSource(source, { type: 'geojson', data: empty, promoteId: 'id' });
    for (const layer of pointLayers(source)) map.addLayer(layer, firstLabelLayer);
  }
}

function setPointOpacity(map: MapLibre, source: PointSource, shown: boolean) {
  const opacity = pointOpacity(shown);
  map.setPaintProperty(`${source}-halo`, 'circle-opacity', opacity.halo);
  map.setPaintProperty(`${source}-dot`, 'circle-opacity', opacity.dot);
  map.setPaintProperty(`${source}-dot`, 'circle-stroke-opacity', opacity.dot);
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

function hoverOf(feature: MapGeoJSONFeature, x: number, y: number): Hover {
  const properties = feature.properties;
  if (feature.source === cellSource) {
    const count = Number(properties.count);
    return {
      x,
      y,
      title: `${count.toLocaleString('en')} ${count === 1 ? 'event' : 'events'}`,
      detail: 'Click to look closer',
    };
  }
  const start = String(properties.start ?? '');
  const day = start ? formatMoment(Date.parse(start)) : '';
  const label = String(properties.label);
  const type = String(properties.type);
  // A generated label already begins with the type ("type · where · what"): it is not said twice.
  const detail = [label.startsWith(type) ? '' : type, day].filter(Boolean).join(' · ');
  return { x, y, title: label, detail };
}

function easeInOut(t: number): number {
  return t < 0.5 ? 4 * t * t * t : 1 - (-2 * t + 2) ** 3 / 2;
}
