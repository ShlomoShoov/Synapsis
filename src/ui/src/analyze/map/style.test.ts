/**
 * The map's style is checked with MapLibre's own validator: an invalid layer is otherwise dropped silently at run time.
 */
import { validateStyleMin } from '@maplibre/maplibre-gl-style-spec';
import { describe, expect, it } from 'vitest';
import { basemapStyle, graticule, lazySources } from './basemap';

describe('the basemap style', () => {
  it.each([true, false])('is valid (relief %s)', (relief) => {
    expect(validateStyleMin(basemapStyle({ relief }))).toEqual([]);
  });

  it('has a source for each file loaded later', () => {
    const style = basemapStyle({ relief: true });
    for (const source of lazySources) {
      expect(style.sources[source.id]).toBeDefined();
    }
  });
});

describe('graticule', () => {
  it('draws meridians and parallels every step', () => {
    const lines = graticule(30).features;
    // 12 meridians (-180 to 150) and 6 parallels (-75 to 75).
    expect(lines).toHaveLength(12 + 6);
  });
});
