/**
 * The ground: an engraving under the scene, like the plate of an astrolabe. Concentric rings and spokes, a ticked
 * bezel at the edge, and one coloured arc for each domain's sector of the ring, so the eye knows where each family
 * of sources lives before reading a label.
 */
import * as THREE from 'three';
import { domainById } from '../../domains';
import { sceneColors } from '../../theme/palette';
import { sourceRadius, type RingLayout } from './layout';
import { groundMaterial, type SharedUniforms } from './materials';

/** A growing list of line segments, each end with its colour and strength. */
class Segments {
  readonly positions: number[] = [];
  readonly colors: number[] = [];
  readonly alphas: number[] = [];

  add(a: THREE.Vector3, b: THREE.Vector3, color: THREE.Color, alphaA: number, alphaB = alphaA) {
    this.positions.push(a.x, a.y, a.z, b.x, b.y, b.z);
    this.colors.push(color.r, color.g, color.b, color.r, color.g, color.b);
    this.alphas.push(alphaA, alphaB);
  }

  /** An arc of a circle on the ground, from angle `start` to `end` (radians). */
  arc(radius: number, start: number, end: number, color: THREE.Color, alpha: number, y = 0) {
    const steps = Math.max(8, Math.ceil(((end - start) * radius * 48) / (Math.PI * 2)));
    let previous = point(radius, start, y);
    for (let i = 1; i <= steps; i++) {
      const next = point(radius, start + ((end - start) * i) / steps, y);
      this.add(previous, next, color, alpha);
      previous = next;
    }
  }

  geometry(): THREE.BufferGeometry {
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(this.positions, 3));
    geometry.setAttribute('aColor', new THREE.Float32BufferAttribute(this.colors, 3));
    geometry.setAttribute('aAlpha', new THREE.Float32BufferAttribute(this.alphas, 1));
    return geometry;
  }
}

/** A point on the ground at a radius and an angle (the same convention as layout.ts). */
function point(radius: number, angle: number, y = 0): THREE.Vector3 {
  return new THREE.Vector3(Math.cos(angle) * radius, y, -Math.sin(angle) * radius);
}

/** The ground of the scene. */
export class Ground {
  readonly object: THREE.LineSegments;
  private readonly material: THREE.ShaderMaterial;

  constructor(shared: SharedUniforms, layout: RingLayout) {
    this.material = groundMaterial(shared);
    this.object = new THREE.LineSegments(this.build(layout), this.material);
    this.object.frustumCulled = false;
  }

  /** Redraws the domain arcs for a new layout (sources appeared or left). */
  setLayout(layout: RingLayout) {
    this.object.geometry.dispose();
    this.object.geometry = this.build(layout);
  }

  dispose() {
    this.object.geometry.dispose();
    this.material.dispose();
  }

  private build(layout: RingLayout): THREE.BufferGeometry {
    const lines = new Segments();
    const ink = new THREE.Color(sceneColors.grid);
    const full = Math.PI * 2;

    // Fine rings every unit, a little stronger every third, and the source ring itself.
    for (let radius = 1.5; radius <= 15; radius += 1.5) {
      const strong = Math.abs(radius % 4.5) < 0.01;
      lines.arc(radius, 0, full, ink, strong ? 0.12 : 0.055);
    }
    lines.arc(sourceRadius, 0, full, ink, 0.17);

    // Spokes from near the core to the bezel; the four quarters a little stronger.
    for (let degrees = 0; degrees < 360; degrees += 15) {
      const angle = (degrees * Math.PI) / 180;
      const quarter = degrees % 90 === 0;
      lines.add(point(2.2, angle), point(12.2, angle), ink, quarter ? 0.1 : 0.05, 0.0);
    }

    // The bezel: a tick every two degrees, a longer one every ten.
    lines.arc(12.4, 0, full, ink, 0.2);
    for (let degrees = 0; degrees < 360; degrees += 2) {
      const angle = (degrees * Math.PI) / 180;
      const long = degrees % 10 === 0;
      lines.add(point(12.4, angle), point(long ? 13.05 : 12.7, angle), ink, long ? 0.3 : 0.15);
    }

    // Each domain's sector: an arc in its colour, closed by two small ticks.
    for (const sector of layout.sectors) {
      const color = new THREE.Color(domainById(sector.domain).color);
      const inset = 0.012;
      lines.arc(
        10.6,
        sector.start + inset,
        sector.end - inset,
        color,
        sector.count > 0 ? 0.62 : 0.2,
      );
      for (const edge of [sector.start + inset, sector.end - inset]) {
        lines.add(point(10.35, edge), point(10.85, edge), color, sector.count > 0 ? 0.5 : 0.18);
      }
    }
    return lines.geometry();
  }
}
