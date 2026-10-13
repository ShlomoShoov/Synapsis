/**
 * The map's style is checked with MapLibre's own validator: an invalid layer is otherwise dropped silently at run time.
 */
import { validateStyleMin, type StyleSpecification } from '@maplibre/maplibre-gl-style-spec';
import { describe, expect, it } from 'vitest';
import { basemapStyle, firstLabelLayer, graticule, lazySources } from './basemap';
import { cellLayers, cellSource, pointLayers, pointSources } from './layers';

const empty = {
  type: 'geojson' as const,
  data: { type: 'FeatureCollection' as const, features: [] },
};

describe('the basemap style', () => {
  it.each([true, false])('is valid (relief %s)', (relief) => {
    expect(validateStyleMin(basemapStyle({ relief }))).toEqual([]);
  });

  it('has the label layer the events go under, and a source for each lazy file', () => {
    const style = basemapStyle({ relief: true });
    expect(style.layers.some((layer) => layer.id === firstLabelLayer)).toBe(true);
    for (const source of lazySources) {
      expect(style.sources[source.id]).toBeDefined();
    }
  });
});

describe('the event layers', () => {
  it('are valid over their sources', () => {
    const style: StyleSpecification = {
      version: 8,
      sources: Object.fromEntries([cellSource, ...pointSources].map((id) => [id, empty])),
      layers: [...cellLayers(), ...pointSources.flatMap((source) => pointLayers(source))],
    };
    expect(validateStyleMin(style)).toEqual([]);
  });
});

describe('graticule', () => {
  it('draws meridians and parallels every step', () => {
    const lines = graticule(30).features;
    // 12 meridians (-180 to 150) and 6 parallels (-75 to 75).
    expect(lines).toHaveLength(12 + 6);
  });
});
