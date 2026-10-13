import { describe, expect, it } from 'vitest';
import type { Cell, Item } from '../api/model';
import { blendColumns, closedRing, columnFeatures, columnsOf, pointFeatures } from './overlay';

const cell = (id: string, count: number): Cell => ({
  id,
  resolution: 2,
  count,
  center: [0, 0],
  boundary: [
    [0, 0],
    [1, 0],
    [1, 1],
  ],
});

describe('columnsOf', () => {
  it('scales by the square root of the share of the largest, in edges of the hexagon', () => {
    const [small, large] = columnsOf([cell('a', 4), cell('b', 16)], 16);
    expect(large!.value).toBe(1);
    expect(small!.value).toBe(0.5);
    // The tallest is 2.5 edges at resolution 2 (an edge is 158 km); a quarter of the events stands about half as high.
    expect(large!.height).toBe(158_000 * 2.5);
    expect(small!.height).toBeCloseTo(158_000 * (0.25 + 2.25 * 0.5));
  });
});

describe('blendColumns', () => {
  it('grows new columns, sinks gone ones and moves the rest', () => {
    const from = columnsOf([cell('stays', 10), cell('goes', 10)], 10);
    const to = columnsOf([cell('stays', 5), cell('comes', 10)], 10);
    const half = blendColumns(from, to, 0.5);
    const byId = new Map(half.map((column) => [column.id, column]));
    expect(byId.get('comes')!.height).toBeCloseTo(to[1]!.height / 2);
    expect(byId.get('goes')!.height).toBeCloseTo(from[1]!.height / 2);
    expect(byId.get('stays')!.height).toBeCloseTo((from[0]!.height + to[0]!.height) / 2);
    expect(blendColumns(from, to, 1)).toBe(to);
  });
});

describe('closedRing', () => {
  it('closes the ring', () => {
    const ring = closedRing([
      [0, 0],
      [1, 0],
      [1, 1],
    ]);
    expect(ring.at(-1)).toEqual([0, 0]);
  });

  it('unwraps a hexagon across the 180th meridian', () => {
    const ring = closedRing([
      [179, 50],
      [-179.5, 51],
      [-179, 52],
      [178.5, 52],
    ]);
    expect(ring.map(([lon]) => lon)).toEqual([179, 180.5, 181, 178.5, 179]);
  });
});

describe('features', () => {
  it('makes polygons with the properties the layers read', () => {
    const [feature] = columnFeatures(columnsOf([cell('a', 3)], 3)).features;
    expect(feature!.id).toBe('a');
    expect(feature!.properties).toMatchObject({ id: 'a', count: 3, value: 1, lon: 0, lat: 0 });
  });

  it('makes points only for items with a point, coloured by domain', () => {
    const item = (id: string, point?: [number, number]): Item => ({
      id,
      kind: 'event',
      type: 'GEO.EARTHQUAKE',
      sources: [],
      knownAt: '2026-01-01T00:00:00Z',
      label: id,
      labelFrom: 'generated',
      place: point ? { point } : { country: 'US' },
      logical: [],
      measures: [],
      participants: [],
    });
    const { features } = pointFeatures([item('a', [10, 20]), item('b')]);
    expect(features).toHaveLength(1);
    expect(features[0]!.properties).toMatchObject({ id: 'a', domain: 'GEO', color: '#1f7a8c' });
  });
});
