import { describe, expect, it } from 'vitest';
import { starShifts, tileSizes } from './starfield';

describe('starShifts', () => {
  it('moves the near stars faster than the far ones, always within one tile', () => {
    const shifts = starShifts(10, 0);
    expect(shifts.far).toBe(
      `translate3d(${(-16).toFixed(1)}px, ${(-tileSizes.far).toFixed(1)}px, 0)`,
    );
    expect(shifts.near).toBe(
      `translate3d(${(-34).toFixed(1)}px, ${(-tileSizes.near).toFixed(1)}px, 0)`,
    );
  });

  it('wraps far turns back into the tile', () => {
    const x = Number(starShifts(-1000, 0).near.match(/translate3d\((-?[\d.]+)px/)![1]);
    expect(x).toBeGreaterThanOrEqual(-tileSizes.near);
    expect(x).toBeLessThanOrEqual(0);
  });
});
