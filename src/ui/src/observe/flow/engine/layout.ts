/**
 * Where everything stands in the 3D scene, worked out on its own (no three.js), so it can be tested.
 *
 * The core is at the centre. Sources stand on a ring around it, grouped by domain: each domain holds one sector of
 * the ring, sized by how many sources it has, with a gap between sectors, and its sources are spread evenly inside
 * it. The six source domains always keep a sector, even an empty one, so the ring keeps its shape as sources appear.
 */
import { domains, sourceDomains, type DomainId } from '../../domains';

/** The radius of the ring the sources stand on (scene units). */
export const sourceRadius = 9;

/** A source to place. */
export interface RingSource {
  id: string;
  domain: DomainId;
}

/** A placed source. */
export interface PlacedSource extends RingSource {
  /** Its angle around the core, in radians (0 on +x, growing towards -z). */
  angle: number;
  x: number;
  z: number;
}

/** A domain's sector of the ring. */
export interface Sector {
  domain: DomainId;
  start: number;
  end: number;
  /** How many sources it holds. */
  count: number;
}

/** The layout of the ring. */
export interface RingLayout {
  sources: PlacedSource[];
  sectors: Sector[];
}

/** The gap between two sectors, in radians. */
const sectorGap = (7 * Math.PI) / 180;

/** Places sources on the ring by domain (in the domains' order, which is also the validated colour order). */
export function ringLayout(
  sources: readonly RingSource[],
  startAngle = Math.PI * 0.62,
): RingLayout {
  const byDomain = new Map<DomainId, RingSource[]>();
  for (const source of sources) {
    const list = byDomain.get(source.domain) ?? [];
    list.push(source);
    byDomain.set(source.domain, list);
  }
  const present = domains.filter(
    (domain) => sourceDomains.includes(domain) || (byDomain.get(domain.id)?.length ?? 0) > 0,
  );
  const weights = present.map((domain) => Math.max(1, byDomain.get(domain.id)?.length ?? 0));
  const totalWeight = weights.reduce((sum, weight) => sum + weight, 0);
  const usable = Math.PI * 2 - sectorGap * present.length;

  const sectors: Sector[] = [];
  const placed: PlacedSource[] = [];
  let angle = startAngle;
  present.forEach((domain, index) => {
    const span = (usable * (weights[index] ?? 1)) / totalWeight;
    const members = [...(byDomain.get(domain.id) ?? [])].sort((a, b) => a.id.localeCompare(b.id));
    sectors.push({ domain: domain.id, start: angle, end: angle + span, count: members.length });
    members.forEach((source, i) => {
      const at = angle + (span * (i + 0.5)) / members.length;
      placed.push({
        ...source,
        angle: at,
        x: Math.cos(at) * sourceRadius,
        z: -Math.sin(at) * sourceRadius,
      });
    });
    angle += span + sectorGap;
  });
  return { sources: placed, sectors };
}
