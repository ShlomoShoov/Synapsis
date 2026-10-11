/**
 * The ripples: thin rings spreading over the ground from the core each time it takes a batch in, larger and brighter
 * for larger batches. A small pool of rings is reused.
 */
import * as THREE from 'three';
import { sceneColors } from '../../theme/palette';
import { rippleMaterial } from './materials';

const poolSize = 14;
/** How far a ripple spreads (scene units) and how long it takes (seconds). */
const reach = 7.5;
const life = 2.6;

interface Ripple {
  mesh: THREE.Mesh;
  material: THREE.ShaderMaterial;
  start: number;
}

/** The ripples of the scene. */
export class Ripples {
  readonly object = new THREE.Group();
  private readonly pool: Ripple[] = [];
  private readonly geometry = new THREE.PlaneGeometry(1, 1);
  private next = 0;

  constructor() {
    const color = new THREE.Color(sceneColors.written);
    for (let i = 0; i < poolSize; i++) {
      const material = rippleMaterial(color);
      const mesh = new THREE.Mesh(this.geometry, material);
      mesh.rotation.x = -Math.PI / 2;
      mesh.position.y = 0.01;
      mesh.visible = false;
      this.object.add(mesh);
      this.pool.push({ mesh, material, start: -1e6 });
    }
  }

  /** Starts a ripple at `start` (engine seconds); `strength` 0 to 1, `color` for a refused batch's red. */
  emit(start: number, strength: number, color?: THREE.Color) {
    const ripple = this.pool[this.next] as Ripple;
    this.next = (this.next + 1) % poolSize;
    ripple.start = start;
    ripple.material.uniforms.uStrength!.value = 0.35 + strength * 0.65;
    ripple.material.uniforms.uColor!.value = color ?? new THREE.Color(sceneColors.written);
    const size = reach * (0.55 + strength * 0.45) * 2;
    ripple.mesh.scale.set(size, size, 1);
    ripple.mesh.visible = true;
  }

  /** Advances every ripple to `time` (engine seconds). */
  update(time: number) {
    for (const ripple of this.pool) {
      const progress = (time - ripple.start) / life;
      if (progress >= 1 || progress < 0) {
        ripple.mesh.visible = false;
        continue;
      }
      ripple.material.uniforms.uProgress!.value = 1 - (1 - progress) ** 2.2;
    }
  }

  dispose() {
    this.geometry.dispose();
    for (const ripple of this.pool) {
      ripple.material.dispose();
    }
  }
}
