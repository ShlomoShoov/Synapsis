import { describe, expect, it } from 'vitest';
import { requestOf, snapped, spanOf, widened, wrap } from './viewport';

describe('requestOf', () => {
  it('asks for the whole Earth from afar, with the zoom in whole steps', () => {
    expect(requestOf(2.6, { west: -60, east: 110, south: -45, north: 75 })).toEqual({ zoom: 2 });
    expect(requestOf(5.4, null)).toEqual({ zoom: 5 });
  });

  it('asks for the box in view, widened and snapped', () => {
    expect(requestOf(5.6, { west: 16.7, east: 40.2, south: 27.6, north: 39.4 })).toEqual({
      zoom: 5,
      bbox: { west: 0, east: 60, south: 20, north: 50 },
    });
  });

  it('asks for the whole Earth when the widened box would go round it', () => {
    expect(requestOf(3.2, { west: -55, east: 106, south: -16, north: 56 })).toEqual({ zoom: 3 });
  });

  it('keeps a box across the 180th meridian as west > east', () => {
    const request = requestOf(5, { west: 170, east: -170, south: 50, north: 60 });
    expect(request.bbox).toEqual({ west: 160, east: -160, south: 45, north: 65 });
  });

  it('asks the same question for nearly the same view', () => {
    const a = requestOf(6.2, { west: 10.1, east: 14.2, south: 44.0, north: 46.1 });
    const b = requestOf(6.4, { west: 10.3, east: 14.4, south: 44.2, north: 46.3 });
    expect(a).toEqual(b);
  });
});

describe('widened', () => {
  it('stays unwrapped: east is always greater than west', () => {
    const box = widened({ west: 170, east: -170, south: 0, north: 10 }, 0.5);
    expect(box.west).toBe(160);
    expect(box.east).toBe(200);
    expect(box.south).toBe(-5);
    expect(box.north).toBe(15);
  });
});

describe('snapped', () => {
  it('moves the edges outward onto a round grid (here 2°, the round step above an eighth of the box)', () => {
    expect(snapped({ west: 1.3, east: 9.6, south: 2.2, north: 7.9 })).toEqual({
      west: 0,
      east: 10,
      south: 2,
      north: 8,
    });
  });
});

describe('spanOf and wrap', () => {
  it('measures eastward and brings longitudes back into range', () => {
    expect(spanOf(170, -170)).toBe(20);
    expect(spanOf(-10, 10)).toBe(20);
    expect(wrap(200)).toBe(-160);
    expect(wrap(-190)).toBe(170);
    expect(wrap(180)).toBe(180);
  });
});
