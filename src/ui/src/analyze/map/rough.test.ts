import { cellToParent, latLngToCell } from 'h3-js';
import { describe, expect, it } from 'vitest';
import type { Cell, MapLayer } from '../api/model';
import { withRough } from './rough';

const cellOf = (id: string, resolution: number, count: number): Cell => ({
  id,
  resolution,
  count,
  center: [0, 0],
  boundary: [],
});

const layerOf = (resolution: number, cells: Cell[]): MapLayer => ({
  layer: 'cells',
  resolution,
  events: cells.reduce((sum, cell) => sum + cell.count, 0),
  total: cells.length,
  omitted: [],
  points: [],
  cells,
});

// Kandahar: 234 UCDP events placed by their province have a cell at resolution 3 and none at 4.
const fine = latLngToCell(31.6, 65.7, 4);
const parent = cellToParent(fine, 3);
const grandparent = cellToParent(fine, 2);

describe('withRough', () => {
  it('keeps the events a finer cell lost as a rough light at the coarse cell', () => {
    const r4 = layerOf(4, [cellOf(fine, 4, 40)]);
    const r3 = layerOf(3, [cellOf(parent, 3, 274)]);
    const layer = withRough(r4, [r3]);
    expect(layer.cells).toHaveLength(2);
    expect(layer.cells[1]).toMatchObject({ id: parent, count: 234, rough: true });
    expect(layer.events).toBe(274);
  });

  it('adds nothing where the finer cells hold every event', () => {
    const r4 = layerOf(4, [cellOf(fine, 4, 40)]);
    const layer = withRough(r4, [layerOf(3, [cellOf(parent, 3, 40)])]);
    expect(layer.cells).toHaveLength(1);
    expect(layer).toEqual(r4);
  });

  it('counts each coarser resolution against the next finer one', () => {
    const r4 = layerOf(4, [cellOf(fine, 4, 10)]);
    const r3 = layerOf(3, [cellOf(parent, 3, 30)]);
    const r2 = layerOf(2, [cellOf(grandparent, 2, 35)]);
    const rough = withRough(r4, [r2, r3]).cells.filter((cell) => cell.rough);
    expect(rough.map((cell) => [cell.resolution, cell.count])).toEqual([
      [2, 5],
      [3, 20],
    ]);
  });

  it('ignores a small difference between estimated counts', () => {
    const r4 = layerOf(4, [cellOf(fine, 4, 9950)]);
    const layer = withRough(r4, [layerOf(3, [cellOf(parent, 3, 10000)])]);
    expect(layer.cells.filter((cell) => cell.rough)).toEqual([]);
  });

  it('changes nothing without coarser layers (resolution 2)', () => {
    const r2 = layerOf(2, [cellOf(grandparent, 2, 7)]);
    expect(withRough(r2, [])).toBe(r2);
  });
});
