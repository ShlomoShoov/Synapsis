/**
 * The map layers that draw the events (overlay.ts makes their data). Hexagons: a soft footprint on the ground and a
 * column above it. Points: a halo, then the dot in its domain's colour with a paper ring. Two sets of point layers, so
 * a new answer fades in over the old one (see GlobeMap).
 */
import type { ExpressionSpecification, LayerSpecification } from 'maplibre-gl';
import { cellRamp, ground } from './palette';

export const cellSource = 'events-cells';
export const pointSources = ['events-points-a', 'events-points-b'] as const;
export type PointSource = (typeof pointSources)[number];

/** The ramp read by both the footprint and the column: value 0 (one event) to 1 (the most in view). */
const rampColor: ExpressionSpecification = [
  'interpolate',
  ['linear'],
  ['get', 'value'],
  ...cellRamp.flatMap((color, index) => [index / (cellRamp.length - 1), color]),
];

/** A hovered or selected hexagon or point is drawn darker (feature state "hover"). */
const hovered: ExpressionSpecification = ['boolean', ['feature-state', 'hover'], false];

export function cellLayers(): LayerSpecification[] {
  return [
    {
      id: 'cells-footprint',
      type: 'fill',
      source: cellSource,
      paint: {
        'fill-color': rampColor,
        'fill-opacity': ['interpolate', ['linear'], ['zoom'], 0, 0.35, 6, 0.25],
      },
    },
    {
      id: 'cells-columns',
      type: 'fill-extrusion',
      source: cellSource,
      paint: {
        'fill-extrusion-color': ['case', hovered, ground.label, rampColor],
        'fill-extrusion-height': ['get', 'height'],
        'fill-extrusion-base': 0,
        'fill-extrusion-opacity': 0.92,
        'fill-extrusion-vertical-gradient': true,
      },
    },
  ];
}

/** A size that grows with the zoom: `scale` times 4 px at zoom 3, 6 px at zoom 8, 8 px at zoom 12. */
function byZoom(scale: number, hoverScale = scale): ExpressionSpecification {
  const size = (px: number): ExpressionSpecification | number =>
    hoverScale === scale ? px * scale : ['case', hovered, px * hoverScale, px * scale];
  return ['interpolate', ['linear'], ['zoom'], 3, size(4), 8, size(6), 12, size(8)];
}

export function pointLayers(source: PointSource): LayerSpecification[] {
  return [
    {
      id: `${source}-halo`,
      type: 'circle',
      source,
      paint: {
        'circle-color': ['get', 'color'],
        'circle-radius': byZoom(2.6),
        'circle-blur': 1,
        'circle-opacity': 0,
        'circle-opacity-transition': { duration: 450, delay: 0 },
        'circle-pitch-alignment': 'map',
      },
    },
    {
      id: `${source}-dot`,
      type: 'circle',
      source,
      paint: {
        'circle-color': ['case', hovered, ground.label, ['get', 'color']],
        'circle-radius': byZoom(1, 1.5),
        'circle-stroke-color': ground.land,
        'circle-stroke-width': 1.5,
        'circle-opacity': 0,
        'circle-stroke-opacity': 0,
        'circle-opacity-transition': { duration: 450, delay: 0 },
        'circle-stroke-opacity-transition': { duration: 450, delay: 0 },
        'circle-pitch-alignment': 'map',
      },
    },
  ];
}

/** The opacity of a point set's layers when it is the one shown (1) or the one fading out (0). */
export function pointOpacity(shown: boolean): { halo: number; dot: number } {
  return shown ? { halo: 0.28, dot: 1 } : { halo: 0, dot: 0 };
}
