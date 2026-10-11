import { describe, expect, it } from 'vitest';
import { ringLayout, sourceRadius } from './layout';

describe('ringLayout', () => {
  const layout = ringLayout([
    { id: 'src:cisa:kev', domain: 'cyber' },
    { id: 'src:nist:nvd', domain: 'cyber' },
    { id: 'src:usgs:comcat', domain: 'environment' },
    { id: 'unknown', domain: 'other' },
  ]);

  it('keeps a sector for each of the six source domains, and adds others only when they have sources', () => {
    expect(layout.sectors.map((sector) => sector.domain)).toEqual([
      'cyber',
      'conflict',
      'society',
      'environment',
      'health',
      'finance',
      'other',
    ]);
  });

  it('puts every source on the ring, inside its domain’s sector', () => {
    for (const source of layout.sources) {
      expect(Math.hypot(source.x, source.z)).toBeCloseTo(sourceRadius);
      const sector = layout.sectors.find((item) => item.domain === source.domain);
      expect(source.angle).toBeGreaterThan(sector?.start ?? Infinity);
      expect(source.angle).toBeLessThan(sector?.end ?? -Infinity);
    }
  });

  it('leaves the ring whole: the sectors and their gaps add up to a full turn', () => {
    const first = layout.sectors[0];
    const last = layout.sectors[layout.sectors.length - 1];
    expect((last?.end ?? 0) - (first?.start ?? 0)).toBeLessThan(Math.PI * 2);
    expect((last?.end ?? 0) - (first?.start ?? 0)).toBeGreaterThan(Math.PI * 1.8);
  });
});
