import { describe, expect, it } from 'vitest';
import type { Cell, Item } from '../api/model';
import { dayOf, joined, lightsOfCells, lightsOfPoints, seedOf, since } from './lights';
import { categoryOf, rgbOf } from './palette';
import { nearestLight } from './pick';

const item = (id: string, type: string, start: string, point?: [number, number]): Item => ({
  id,
  kind: 'event',
  type,
  sources: [],
  knownAt: '2026-01-01T00:00:00Z',
  label: id,
  labelFrom: 'generated',
  time: { start },
  place: point ? { point } : { country: 'US' },
  logical: [],
  measures: [],
  participants: [],
});

describe('categoryOf', () => {
  it('takes the most specific category a type starts with', () => {
    expect(categoryOf('SEC.ATTACK.BOMBING').name).toBe('Attacks');
    expect(categoryOf('SEC.CONFLICT.STATE_BASED').name).toBe('Armed conflict');
    expect(categoryOf('SEC.OTHER').name).toBe('Security');
    expect(categoryOf('GEO.EARTHQUAKE').key).toBe('GEO');
    expect(categoryOf('SECRET').key).toBe('OTHER');
  });

  it('reads colours', () => {
    expect(rgbOf('#ff0080')).toEqual([1, 0, 128 / 255]);
  });
});

describe('lightsOfPoints', () => {
  it('makes one light per located event, with its colour, day and a stable seed', () => {
    const set = lightsOfPoints([
      item('a', 'GEO.EARTHQUAKE', '1900-01-02T00:00:00Z', [0, 0]),
      item('b', 'GEO.EARTHQUAKE', '1970-01-01T00:00:00Z'),
    ]);
    expect(set.count).toBe(1);
    expect(set.ids).toEqual(['a']);
    expect(set.days[0]).toBe(1);
    expect(Array.from(set.positions)).toEqual([0.5, 0.5]);
    expect(set.seeds[0]).toBe(Math.fround(seedOf('a')));
    expect(set.items[0]?.id).toBe('a');
  });
});

describe('lightsOfCells', () => {
  it('makes larger lights for fuller hexagons, always shown', () => {
    const cell = (id: string, count: number): Cell => ({
      id,
      resolution: 2,
      count,
      center: [10, 10],
      boundary: [],
    });
    const set = lightsOfCells([cell('a', 1), cell('b', 100)]);
    expect(set.sizes[1]).toBeCloseTo(3.2);
    expect(set.sizes[0]).toBeCloseTo(1.22);
    expect(Number.isNaN(set.days[0])).toBe(true);
    expect(set.counts).toEqual([1, 100]);
  });
});

describe('joined and since', () => {
  it('adds new lights once and lets go of old ones', () => {
    const first = lightsOfPoints([item('a', 'TXT.POST', '1900-01-02T00:00:00Z', [0, 0])]);
    const second = lightsOfPoints([
      item('a', 'TXT.POST', '1900-01-02T00:00:00Z', [0, 0]),
      item('b', 'TXT.POST', '1900-01-05T00:00:00Z', [1, 1]),
    ]);
    const both = joined(first, second);
    expect(both.ids).toEqual(['a', 'b']);
    expect(since(both, 3).ids).toEqual(['b']);
    expect(since(both, 0)).toBe(both);
  });

  it('counts days from 1900', () => {
    expect(dayOf('1900-01-11T12:00:00Z')).toBe(10.5);
    expect(Number.isNaN(dayOf(undefined))).toBe(true);
  });
});

describe('nearestLight', () => {
  const project = ([lon, lat]: [number, number]) => ({ x: lon * 10, y: -lat * 10 });

  it('takes the nearest light within reach', () => {
    const places: [number, number][] = [
      [0, 0],
      [1, 0],
      [5, 5],
    ];
    expect(nearestLight(places, 9, 1, project, [0, 0])).toBe(1);
    expect(nearestLight(places, 30, 30, project, [0, 0])).toBe(-1);
  });

  it('skips lights behind the globe', () => {
    expect(nearestLight([[180, 0]], 1800, 0, project, [0, 0])).toBe(-1);
  });
});
