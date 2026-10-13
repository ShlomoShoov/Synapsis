/**
 * The basemap: the Earth at night, a MapLibre style drawn from the files in assets/geo/ (Natural Earth, made by
 * scripts/make-basemap.sh) and the public elevation tiles, so the globe needs no tile server and looks the same
 * everywhere.
 *
 * From the bottom: the land (the background), its relief shading, the sea (which hides the shading offshore), a coast
 * that glows faintly, lakes and rivers, a faint graticule, the borders, then the labels. The events (LightsLayer) are
 * drawn above all of it. A blue atmosphere rims the globe; the stars are behind the map (Starfield). The heavy files
 * load only when the map is close enough to need them (lazySources). There is no 3D terrain: it costs every frame and
 * shows little on a night Earth.
 */
import type {
  ExpressionSpecification,
  FontFacesSpecification,
  LayerSpecification,
  StyleSpecification,
} from 'maplibre-gl';
import type { Feature, FeatureCollection, LineString } from 'geojson';
import sansUrl from '@fontsource-variable/instrument-sans/files/instrument-sans-latin-wght-normal.woff2?url';
import sansExtUrl from '@fontsource-variable/instrument-sans/files/instrument-sans-latin-ext-wght-normal.woff2?url';
import serifItalicUrl from '@fontsource-variable/newsreader/files/newsreader-latin-wght-italic.woff2?url';
import serifExtItalicUrl from '@fontsource-variable/newsreader/files/newsreader-latin-ext-wght-italic.woff2?url';
import { terrainTilesUrl } from '../config';
import { ground } from './palette';

/** Where the basemap files are served (assets/geo/ is copied to the site root). */
const geo = (name: string) => `/geo/${name}.json`;

/** An empty layer of features, for a source whose file is not loaded yet. */
const empty = { type: 'FeatureCollection' as const, features: [] };

/** A source whose file loads once the map reaches `fromZoom` (see GlobeMap). */
export interface LazySource {
  id: string;
  url: string;
  fromZoom: number;
}

/** The detailed files: the fine coast, every lake, and the states and provinces. */
export const lazySources: LazySource[] = [
  { id: 'sea-fine', url: geo('ocean-10m'), fromZoom: 4 },
  { id: 'lakes', url: geo('lakes'), fromZoom: 2.5 },
  { id: 'divisions', url: geo('states'), fromZoom: 2.5 },
];

/** The interface sans for places, the brand's serif (italic) for the seas, drawn from their own font files. */
export const fonts = {
  sans: 'Instrument Sans Regular',
  sansMedium: 'Instrument Sans Medium',
  serifItalic: 'Newsreader Italic',
};

const latin = ['U+0000-00FF', 'U+0131', 'U+0152-0153', 'U+2000-206F'];

const fontFaces: FontFacesSpecification = {
  [fonts.sans]: [{ url: sansUrl, 'unicode-range': latin }, { url: sansExtUrl }],
  [fonts.sansMedium]: [{ url: sansUrl, 'unicode-range': latin }, { url: sansExtUrl }],
  [fonts.serifItalic]: [
    { url: serifItalicUrl, 'unicode-range': latin },
    { url: serifExtItalicUrl },
  ],
};

/** The basemap's style. `relief` adds the shading of the land from the elevation tiles. */
export function basemapStyle({ relief }: { relief: boolean }): StyleSpecification {
  return {
    version: 8,
    projection: { type: 'globe' },
    'font-faces': fontFaces,
    sky: {
      'sky-color': ground.space,
      'horizon-color': ground.horizon,
      'sky-horizon-blend': 0.8,
      // MapLibre's air is lit like daylight from one side; the even glow around the Earth is drawn by GlobeMap.
      'atmosphere-blend': 0,
    },
    light: { anchor: 'viewport', position: [1.5, 200, 30], color: '#ffffff', intensity: 0.3 },
    sources: {
      ...(relief
        ? {
            elevation: {
              type: 'raster-dem',
              tiles: [terrainTilesUrl],
              encoding: 'terrarium',
              tileSize: 256,
              maxzoom: 11,
              attribution: 'Elevation: Mapzen Terrain Tiles (AWS Open Data)',
            },
          }
        : {}),
      'sea-coarse': { type: 'geojson', data: geo('ocean-110m'), maxzoom: 4 },
      'sea-mid': { type: 'geojson', data: geo('ocean-50m'), maxzoom: 7 },
      'sea-fine': { type: 'geojson', data: empty, maxzoom: 12 },
      lakes: { type: 'geojson', data: empty, maxzoom: 10 },
      rivers: { type: 'geojson', data: geo('rivers'), maxzoom: 8 },
      borders: { type: 'geojson', data: geo('borders'), maxzoom: 10 },
      divisions: { type: 'geojson', data: empty, maxzoom: 9 },
      graticule: { type: 'geojson', data: graticule(15), maxzoom: 4 },
      labels: { type: 'geojson', data: geo('labels'), maxzoom: 9, attribution: 'Natural Earth' },
    },
    layers: [
      { id: 'land', type: 'background', paint: { 'background-color': ground.land } },
      ...(relief ? [reliefLayer()] : []),
      ...seaLayers(),
      ...waterLayers(),
      {
        id: 'graticule',
        type: 'line',
        source: 'graticule',
        maxzoom: 6,
        paint: {
          'line-color': ground.graticule,
          'line-width': 0.6,
          'line-opacity': ['interpolate', ['linear'], ['zoom'], 0, 0.9, 4, 0.5, 6, 0],
        },
      },
      ...borderLayers(),
      ...labelLayers(),
    ],
  };
}

/** Shading of the land from the elevation tiles: mountains catch a cold light from the north-west. */
function reliefLayer(): LayerSpecification {
  return {
    id: 'relief',
    type: 'hillshade',
    source: 'elevation',
    paint: {
      'hillshade-shadow-color': ground.reliefShadow,
      'hillshade-highlight-color': ground.reliefHighlight,
      'hillshade-accent-color': ground.reliefShadow,
      'hillshade-exaggeration': ['interpolate', ['linear'], ['zoom'], 0, 0.75, 5, 0.6, 10, 0.45],
      'hillshade-illumination-direction': 315,
    },
  };
}

/** The sea at three scales, and its coast: a thin bright line in a wider soft glow. */
function seaLayers(): LayerSpecification[] {
  const scales = [
    { source: 'sea-coarse', minzoom: 0, maxzoom: 2.5 },
    { source: 'sea-mid', minzoom: 2.5, maxzoom: 4.5 },
    { source: 'sea-fine', minzoom: 4.5, maxzoom: 24 },
  ];
  // Until the fine coast has loaded, the middle one keeps drawing: it stays on below the fine one.
  return scales.flatMap(({ source, minzoom, maxzoom }) => [
    {
      id: source,
      type: 'fill' as const,
      source,
      minzoom,
      maxzoom: source === 'sea-mid' ? 24 : maxzoom,
      paint: { 'fill-color': ground.sea },
    },
    {
      id: `${source}-glow`,
      type: 'line' as const,
      source,
      minzoom,
      maxzoom,
      paint: {
        'line-color': ground.coastGlow,
        'line-width': ['interpolate', ['linear'], ['zoom'], 0, 2, 6, 5],
        'line-blur': ['interpolate', ['linear'], ['zoom'], 0, 2, 6, 5],
        'line-opacity': 0.22,
      },
    },
    {
      id: `${source}-coast`,
      type: 'line' as const,
      source,
      minzoom,
      maxzoom,
      paint: {
        'line-color': ground.coast,
        'line-width': ['interpolate', ['linear'], ['zoom'], 0, 0.5, 6, 0.9, 10, 1.2],
      },
    },
  ]);
}

/** Lakes and rivers, which appear as the map comes closer. */
function waterLayers(): LayerSpecification[] {
  return [
    {
      id: 'lakes',
      type: 'fill',
      source: 'lakes',
      minzoom: 2.5,
      filter: ['<=', ['get', 'r'], ['+', ['zoom'], 2]],
      paint: { 'fill-color': ground.sea, 'fill-outline-color': ground.coast },
    },
    {
      id: 'rivers',
      type: 'line',
      source: 'rivers',
      minzoom: 3,
      filter: ['<=', ['get', 'r'], ['+', ['zoom'], 3]],
      layout: { 'line-cap': 'round', 'line-join': 'round' },
      paint: {
        'line-color': ground.river,
        'line-width': ['interpolate', ['linear'], ['zoom'], 3, 0.5, 8, 1.4],
      },
    },
  ];
}

/** The borders of countries (dashed where disputed) and, closer, of their divisions. */
function borderLayers(): LayerSpecification[] {
  const width: ExpressionSpecification = [
    'interpolate',
    ['linear'],
    ['zoom'],
    0,
    0.5,
    4,
    0.8,
    8,
    1.3,
  ];
  return [
    {
      id: 'divisions',
      type: 'line',
      source: 'divisions',
      minzoom: 2.5,
      filter: ['<=', ['get', 'z'], ['zoom']],
      layout: { 'line-join': 'round' },
      paint: {
        'line-color': ground.division,
        'line-width': ['interpolate', ['linear'], ['zoom'], 3, 0.4, 8, 0.9],
        'line-dasharray': [3, 2],
      },
    },
    {
      id: 'borders',
      type: 'line',
      source: 'borders',
      filter: ['==', ['get', 'd'], 0],
      layout: { 'line-join': 'round' },
      paint: { 'line-color': ground.border, 'line-width': width, 'line-opacity': 0.75 },
    },
    {
      id: 'borders-disputed',
      type: 'line',
      source: 'borders',
      filter: ['==', ['get', 'd'], 1],
      paint: {
        'line-color': ground.border,
        'line-width': width,
        'line-dasharray': [2, 2],
        'line-opacity': 0.75,
      },
    },
  ];
}

/** Labels shown from their own zoom on (z, from the file), most important first (r). */
const fromItsZoom: ExpressionSpecification = ['<=', ['get', 'z'], ['zoom']];

function labelLayers(): LayerSpecification[] {
  const halo = {
    'text-halo-color': ground.labelHalo,
    'text-halo-width': 1.2,
    'text-halo-blur': 0.6,
  };
  return [
    {
      id: 'label-sea',
      type: 'symbol',
      source: 'labels',
      filter: ['all', ['in', ['get', 'k'], ['literal', ['ocean', 'sea']]], fromItsZoom],
      layout: {
        'text-field': ['get', 'n'],
        'text-font': [fonts.serifItalic],
        'text-size': [
          'interpolate',
          ['linear'],
          ['zoom'],
          0,
          ['match', ['get', 'k'], 'ocean', 12, 10],
          5,
          ['match', ['get', 'k'], 'ocean', 17, 13],
        ],
        'text-letter-spacing': ['match', ['get', 'k'], 'ocean', 0.35, 0.12],
        'text-transform': ['match', ['get', 'k'], 'ocean', 'uppercase', 'none'],
        'text-max-width': 8,
        'symbol-sort-key': ['get', 'r'],
      },
      paint: { 'text-color': ground.seaLabel, ...halo },
    },
    {
      id: 'label-city-dot',
      type: 'circle',
      source: 'labels',
      minzoom: 3,
      filter: ['all', ['==', ['get', 'k'], 'city'], fromItsZoom],
      paint: {
        'circle-radius': ['match', ['get', 'p'], 2, 2.2, 1.6],
        'circle-color': ground.label,
        'circle-opacity': 0.7,
      },
    },
    {
      id: 'label-city',
      type: 'symbol',
      source: 'labels',
      minzoom: 3,
      filter: ['all', ['==', ['get', 'k'], 'city'], fromItsZoom],
      layout: {
        'text-field': ['get', 'n'],
        'text-font': [fonts.sans],
        'text-size': ['interpolate', ['linear'], ['zoom'], 3, 10.5, 8, 13],
        'text-anchor': 'left',
        'text-offset': [0.6, 0],
        'text-max-width': 9,
        'symbol-sort-key': ['get', 'r'],
      },
      paint: { 'text-color': ground.label, 'text-opacity': 0.85, ...halo },
    },
    {
      id: 'label-country',
      type: 'symbol',
      source: 'labels',
      filter: ['all', ['==', ['get', 'k'], 'country'], fromItsZoom],
      layout: {
        'text-field': ['get', 'n'],
        'text-font': [fonts.sansMedium],
        'text-size': ['interpolate', ['linear'], ['zoom'], 1, 9.5, 4, 12, 7, 15],
        'text-transform': 'uppercase',
        'text-letter-spacing': 0.18,
        'text-max-width': 7,
        'symbol-sort-key': ['get', 'r'],
      },
      paint: { 'text-color': ground.label, 'text-opacity': 0.6, ...halo },
    },
  ];
}

/** Lines of latitude and longitude every `step` degrees, as GeoJSON (the poles left out). */
export function graticule(step: number): FeatureCollection<LineString> {
  const features: Feature<LineString>[] = [];
  const line = (coordinates: [number, number][]): Feature<LineString> => ({
    type: 'Feature',
    properties: {},
    geometry: { type: 'LineString', coordinates },
  });
  for (let lon = -180; lon < 180; lon += step) {
    const meridian: [number, number][] = [];
    for (let lat = -80; lat <= 80; lat += 2.5) meridian.push([lon, lat]);
    features.push(line(meridian));
  }
  for (let lat = -75; lat <= 75; lat += step) {
    const parallel: [number, number][] = [];
    for (let lon = -180; lon <= 180; lon += 2.5) parallel.push([lon, lat]);
    features.push(line(parallel));
  }
  return { type: 'FeatureCollection', features };
}
