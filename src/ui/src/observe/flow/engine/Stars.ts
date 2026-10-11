/**
 * Far, faint points around the scene: they turn with the camera and give the eye a sense of depth. Nothing more.
 */
import * as THREE from 'three';
import { starMaterial, type SharedUniforms } from './materials';

/** The starfield of the scene. */
export class Stars {
  readonly object: THREE.Points;
  private readonly material: THREE.ShaderMaterial;

  constructor(shared: SharedUniforms, count = 900) {
    const positions = new Float32Array(count * 3);
    const seeds = new Float32Array(count);
    for (let i = 0; i < count; i++) {
      // On a far sphere, mostly above the horizon.
      const y = Math.random() * 1.1 - 0.15;
      const angle = Math.random() * Math.PI * 2;
      const r = Math.sqrt(Math.max(0, 1 - y * y));
      const distance = 70 + Math.random() * 40;
      positions.set(
        [Math.cos(angle) * r * distance, y * distance, Math.sin(angle) * r * distance],
        i * 3,
      );
      seeds[i] = Math.random();
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute('aSeed', new THREE.BufferAttribute(seeds, 1));
    this.material = starMaterial(shared);
    this.object = new THREE.Points(geometry, this.material);
  }

  dispose() {
    this.object.geometry.dispose();
    this.material.dispose();
  }
}
