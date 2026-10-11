/**
 * The core: the API at the centre of the scene. A sphere of fine points, brighter at its rim, breathing slowly; a
 * warm glow around it; two thin orbits with small satellites, turning like an instrument's rings. When a batch
 * arrives the core pulses, more for larger batches.
 */
import * as THREE from 'three';
import { sceneColors } from '../../theme/palette';
import { coreMaterial, glowMaterial, type SharedUniforms } from './materials';

/** The radius of the core sphere (scene units): arcs end on its surface. */
export const coreRadius = 1.05;

/** The height of the core's centre above the ground. */
export const coreHeight = 1.4;

/** Points spread evenly over a sphere (a Fibonacci lattice). */
function sphere(count: number, radius: number): { positions: Float32Array; seeds: Float32Array } {
  const positions = new Float32Array(count * 3);
  const seeds = new Float32Array(count);
  const golden = Math.PI * (3 - Math.sqrt(5));
  for (let i = 0; i < count; i++) {
    const y = 1 - (2 * (i + 0.5)) / count;
    const r = Math.sqrt(1 - y * y);
    const angle = golden * i;
    positions.set([Math.cos(angle) * r * radius, y * radius, Math.sin(angle) * r * radius], i * 3);
    seeds[i] = (i * 0.618033) % 1;
  }
  return { positions, seeds };
}

/** The core of the scene. */
export class Core {
  readonly object = new THREE.Group();
  private readonly points: THREE.Points;
  private readonly material: THREE.ShaderMaterial;
  private readonly halo: THREE.Mesh;
  private readonly heart: THREE.Mesh;
  private readonly orbits = new THREE.Group();
  private readonly disposables: { dispose(): void }[] = [];
  /** How strongly the core is pulsing now; decays towards zero. */
  private pulse = 0;

  constructor(shared: SharedUniforms) {
    const color = new THREE.Color(sceneColors.core);
    this.object.position.set(0, coreHeight, 0);

    const { positions, seeds } = sphere(1700, coreRadius);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute('aSeed', new THREE.BufferAttribute(seeds, 1));
    this.material = coreMaterial(shared, color);
    this.points = new THREE.Points(geometry, this.material);
    this.object.add(this.points);

    const quad = new THREE.PlaneGeometry(1, 1);
    const haloMaterial = glowMaterial(color, 0.13);
    this.halo = new THREE.Mesh(quad, haloMaterial);
    this.halo.scale.setScalar(8);
    const heartMaterial = glowMaterial(new THREE.Color(sceneColors.written), 0.32);
    this.heart = new THREE.Mesh(quad, heartMaterial);
    this.heart.scale.setScalar(1.7);
    this.object.add(this.halo, this.heart);

    // Two orbits: one level, one tilted, each a thin ring with a few satellites riding it.
    const ringMaterial = new THREE.LineBasicMaterial({
      color,
      transparent: true,
      opacity: 0.16,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    const satelliteMaterial = new THREE.PointsMaterial({
      color,
      size: 3,
      sizeAttenuation: false,
      transparent: true,
      opacity: 0.8,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    for (const [radius, tilt, satellites] of [
      [1.7, 0, 3],
      [2.15, 0.42, 2],
    ] as const) {
      const ring = new THREE.Group();
      const circle = new THREE.BufferGeometry().setFromPoints(
        Array.from({ length: 129 }, (_, i) => {
          const angle = (i / 128) * Math.PI * 2;
          return new THREE.Vector3(Math.cos(angle) * radius, 0, Math.sin(angle) * radius);
        }),
      );
      ring.add(new THREE.Line(circle, ringMaterial));
      const dots = new THREE.BufferGeometry().setFromPoints(
        Array.from({ length: satellites }, (_, i) => {
          const angle = (i / satellites) * Math.PI * 2 + radius;
          return new THREE.Vector3(Math.cos(angle) * radius, 0, Math.sin(angle) * radius);
        }),
      );
      ring.add(new THREE.Points(dots, satelliteMaterial));
      ring.rotation.x = tilt;
      ring.rotation.z = tilt * 0.6;
      this.orbits.add(ring);
      this.disposables.push(circle, dots);
    }
    this.object.add(this.orbits);
    this.disposables.push(
      geometry,
      quad,
      haloMaterial,
      heartMaterial,
      ringMaterial,
      satelliteMaterial,
    );
  }

  /** Makes the core pulse; `strength` 0 to 1 (a large batch is near 1). */
  pulseBy(strength: number) {
    this.pulse = Math.min(1.6, this.pulse + strength);
  }

  /** Advances the core by `dt` seconds and turns its glows towards the camera. */
  update(dt: number, camera: THREE.Camera, reducedMotion: boolean) {
    this.pulse *= Math.exp(-dt * 2.4);
    this.material.uniforms.uPulse!.value = this.pulse;
    const turn = reducedMotion ? 0 : dt;
    this.points.rotation.y += turn * 0.06;
    this.orbits.children[0]!.rotation.y += turn * 0.22;
    this.orbits.children[1]!.rotation.y -= turn * 0.14;
    this.halo.quaternion.copy(camera.quaternion);
    this.heart.quaternion.copy(camera.quaternion);
    this.heart.scale.setScalar(1.7 + this.pulse * 1.2);
    (this.halo.material as THREE.ShaderMaterial).uniforms.uOpacity!.value =
      0.13 + this.pulse * 0.16;
  }

  dispose() {
    this.material.dispose();
    for (const item of this.disposables) {
      item.dispose();
    }
  }
}
