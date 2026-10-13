import { describe, expect, it } from 'vitest';
import { itemOf, mapLayerOf } from './adapt';
import type { WireMapAnswer, WireRecordSummary } from './contract';

const event: WireRecordSummary = {
  id: 'evt:gtd:197001060001',
  kind: 'event',
  type: 'SEC.ATTACK.BOMBING',
  sources: ['src:start:gtd'],
  known_at: '2026-10-12T17:45:05Z',
  label: 'Bombing · Denver',
  label_from: 'generated',
  time_start: '1970-01-06T00:00:00Z',
  time_precision: 'day',
  point: [-104.99, 39.74],
  uncertainty_km: 5,
  country: 'US',
  subdivision: 'US-CO',
  measures: [{ name: 'killed', value: 0 }, { name: 'broken' }],
  participants: [{ id: 'ent:gtd-group:x', label: 'X' }],
};

describe('itemOf', () => {
  it('gathers the time and the place, and keeps only whole measures', () => {
    const item = itemOf(event);
    expect(item.time).toEqual({ start: '1970-01-06T00:00:00Z', end: undefined, precision: 'day' });
    expect(item.place).toEqual({
      point: [-104.99, 39.74],
      uncertaintyKm: 5,
      country: 'US',
      subdivision: 'US-CO',
      areaName: undefined,
    });
    expect(item.measures).toEqual([{ name: 'killed', value: 0, unit: undefined }]);
    expect(item.logical).toEqual([]);
  });

  it('gives an entity no time and no place', () => {
    const item = itemOf({
      id: 'ent:kev-vendor:apache',
      kind: 'entity',
      type: 'Vendor',
      sources: ['src:cisa:kev'],
      known_at: '2026-10-01T00:00:00Z',
      label: 'Apache',
      label_from: 'name',
    });
    expect(item.time).toBeUndefined();
    expect(item.place).toBeUndefined();
    expect(item.participants).toEqual([]);
  });
});

describe('mapLayerOf', () => {
  it('reads a layer of cells and fills the missing lists', () => {
    const wire: WireMapAnswer = {
      as_of: '2026-10-13T09:11:20Z',
      layer: 'cells',
      resolution: 2,
      events: 26,
      total: 1,
      cells: [
        {
          cell: '822a17fffffffff',
          resolution: 2,
          count: 26,
          center: [-73.78, 40.86],
          boundary: [[-73, 42]],
        },
      ],
    };
    const layer = mapLayerOf(wire);
    expect(layer.cells[0]).toEqual({
      id: '822a17fffffffff',
      resolution: 2,
      count: 26,
      center: [-73.78, 40.86],
      boundary: [[-73, 42]],
    });
    expect(layer.points).toEqual([]);
    expect(layer.omitted).toEqual([]);
  });
});
