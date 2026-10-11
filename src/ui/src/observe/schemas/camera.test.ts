import { describe, expect, it } from 'vitest';
import {
  between,
  centreOn,
  clampScale,
  fitBox,
  maxScale,
  reveal,
  toMap,
  toScreen,
  zoomAt,
} from './camera';

describe('the map camera', () => {
  it('turns map points into screen points and back', () => {
    const camera = { k: 0.5, tx: 100, ty: -40 };
    expect(toScreen(camera, { x: 200, y: 100 })).toEqual({ x: 200, y: 10 });
    expect(toMap(camera, { x: 200, y: 10 })).toEqual({ x: 200, y: 100 });
  });

  it('zooms around the pointer, which stays over the same point of the map', () => {
    const camera = { k: 1, tx: 0, ty: 0 };
    const zoomed = zoomAt(camera, 2, { x: 300, y: 200 });
    expect(zoomed.k).toBe(2);
    expect(toMap(zoomed, { x: 300, y: 200 })).toEqual({ x: 300, y: 200 });
    expect(zoomAt(camera, 100, { x: 0, y: 0 }).k).toBe(maxScale);
    expect(clampScale(0)).toBeGreaterThan(0);
  });

  it('fits a box into the free part of the frame', () => {
    const frame = { width: 1000, height: 600, coveredRight: 200 };
    const camera = fitBox({ x: -500, y: -250, width: 1000, height: 500 }, frame, 0);
    expect(camera.k).toBe(0.8);
    // The box's middle shows in the middle of the 800 free pixels.
    expect(toScreen(camera, { x: 0, y: 0 })).toEqual({ x: 400, y: 300 });
    expect(centreOn({ x: 10, y: 10 }, { width: 100, height: 100 }, 1)).toEqual({
      k: 1,
      tx: 40,
      ty: 40,
    });
  });

  it('moves just enough to reveal a box, and not at all when it shows', () => {
    const frame = { width: 1000, height: 600 };
    const camera = { k: 1, tx: 0, ty: 0 };
    expect(reveal(camera, { x: 100, y: 100, width: 50, height: 50 }, frame)).toBe(camera);
    const moved = reveal(camera, { x: 1100, y: 100, width: 100, height: 50 }, frame, 20);
    expect(toScreen(moved, { x: 1200, y: 0 }).x).toBe(980);
  });

  it('travels between two cameras, reaching each end', () => {
    const a = { k: 0.5, tx: 0, ty: 0 };
    const b = { k: 2, tx: -400, ty: -200 };
    expect(between(a, b, 0)).toEqual(a);
    const end = between(a, b, 1);
    expect(end.k).toBeCloseTo(2);
    expect(end.tx).toBeCloseTo(-400);
    expect(between(a, b, 0.5).k).toBeCloseTo(1);
  });
});
