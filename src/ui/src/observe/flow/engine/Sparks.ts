/**
 * The sparks: refused records, thrown back from the core's surface the way they came, falling and fading in the
 * reserved red. More refusals, more sparks (on a log scale, capped, so a storm reads as a storm without hiding the
 * scene).
 */
import * as THREE from 'three';
import { sceneColors } from '../../theme/palette';
import { sparkMaterial, type SharedUniforms } from './materials';

const maxSparks = 3000;

/** The sparks of the scene. */
export class Sparks {
  readonly object: THREE.Points;
  private readonly geometry = new THREE.BufferGeometry();
  private readonly material: THREE.ShaderMaterial;
  private next = 0;
  private readonly away = new THREE.Vector3();

  constructor(shared: SharedUniforms) {
    const attribute = (size: number) =>
      new THREE.BufferAttribute(new Float32Array(maxSparks * size), size).setUsage(
        THREE.DynamicDrawUsage,
      );
    this.geometry.setAttribute('position', attribute(3));
    this.geometry.setAttribute('aOrigin', attribute(3));
    this.geometry.setAttribute('aVelocity', attribute(3));
    this.geometry.setAttribute('aStart', attribute(1));
    this.geometry.setAttribute('aLife', attribute(1));
    this.geometry.setAttribute('aSize', attribute(1));
    (this.geometry.getAttribute('aStart') as THREE.BufferAttribute).array.fill(-1e6);
    (this.geometry.getAttribute('aLife') as THREE.BufferAttribute).array.fill(1);
    this.material = sparkMaterial(shared, new THREE.Color(sceneColors.rejected));
    this.object = new THREE.Points(this.geometry, this.material);
    this.object.frustumCulled = false;
  }

  /**
   * Throws `count` sparks from `at` (a point on the core's surface), back towards `from` (where the batch came from),
   * starting at `start` (engine seconds).
   */
  burst(at: THREE.Vector3, from: THREE.Vector3, count: number, start: number) {
    this.away.subVectors(from, at).setY(0).normalize();
    for (let i = 0; i < count; i++) {
      const slot = this.next;
      this.next = (this.next + 1) % maxSparks;
      const spread = 0.9;
      const speed = 1.6 + Math.random() * 2.4;
      const velocity = [
        (this.away.x + (Math.random() - 0.5) * spread) * speed,
        0.8 + Math.random() * 2.2,
        (this.away.z + (Math.random() - 0.5) * spread) * speed,
      ];
      this.write('aOrigin', slot, [at.x, at.y, at.z]);
      this.write('aVelocity', slot, velocity);
      this.write('aStart', slot, [start + Math.random() * 0.12]);
      this.write('aLife', slot, [0.9 + Math.random() * 1.1]);
      this.write('aSize', slot, [0.12 + Math.random() * 0.14]);
    }
  }

  dispose() {
    this.geometry.dispose();
    this.material.dispose();
  }

  private write(name: string, slot: number, values: number[]) {
    const attribute = this.geometry.getAttribute(name) as THREE.BufferAttribute;
    (attribute.array as Float32Array).set(values, slot * attribute.itemSize);
    attribute.addUpdateRange(slot * attribute.itemSize, attribute.itemSize);
    attribute.needsUpdate = true;
  }
}
