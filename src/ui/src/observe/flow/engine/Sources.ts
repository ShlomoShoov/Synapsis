/**
 * The sources: one light per source on the ring, in its domain's colour, standing on a thin pylon. The pylon's
 * height is how much the source sent over the last day (on a log scale, so a quiet source still shows); the light
 * flares when the source sends a batch, and grows when the pointer is on it. A source that sent nothing in the window
 * stays low and dim.
 */
import * as THREE from 'three';
import { domainById } from '../../domains';
import { domainGlow } from '../../theme/palette';
import type { PlacedSource } from './layout';
import { groundMaterial, orbMaterial, type SharedUniforms } from './materials';

/** The lowest and highest a light stands above the ground. */
const minHeight = 0.35;
const maxHeight = 3.4;

/** One source's state in the scene. */
interface SourceState {
  placed: PlacedSource;
  color: THREE.Color;
  height: number;
  targetHeight: number;
  /** Extra brightness from a recent batch, decaying. */
  flare: number;
  /** True when the source sent something in the window. */
  active: boolean;
}

/** The sources of the scene. */
export class Sources {
  readonly object = new THREE.Group();
  private states: SourceState[] = [];
  private readonly index = new Map<string, number>();
  private readonly orbs: THREE.Points;
  private readonly pylons: THREE.LineSegments;
  private readonly orbMaterial: THREE.ShaderMaterial;
  private readonly pylonMaterial: THREE.ShaderMaterial;
  private hovered: number | null = null;
  private focused: string | null = null;

  constructor(shared: SharedUniforms) {
    this.orbMaterial = orbMaterial(shared);
    this.pylonMaterial = groundMaterial(shared);
    this.pylonMaterial.uniforms.uFadeStart = { value: 40 };
    this.pylonMaterial.uniforms.uFadeEnd = { value: 50 };
    this.orbs = new THREE.Points(new THREE.BufferGeometry(), this.orbMaterial);
    this.pylons = new THREE.LineSegments(new THREE.BufferGeometry(), this.pylonMaterial);
    this.orbs.frustumCulled = false;
    this.pylons.frustumCulled = false;
    this.object.add(this.pylons, this.orbs);
  }

  /** Places the sources (keeping the heights of those that stay). */
  setSources(placed: readonly PlacedSource[]) {
    const previous = new Map(this.states.map((state) => [state.placed.id, state]));
    this.states = placed.map((source) => {
      const old = previous.get(source.id);
      return {
        placed: source,
        color: new THREE.Color(domainGlow(source.domain)),
        height: old?.height ?? 0,
        targetHeight: old?.targetHeight ?? minHeight,
        flare: 0,
        active: old?.active ?? false,
      };
    });
    this.index.clear();
    this.states.forEach((state, i) => this.index.set(state.placed.id, i));
    this.rebuild();
  }

  /**
   * Sets how much each source sent over the window (records), which sets its height; sources not listed sent
   * nothing.
   */
  setVolumes(volumes: ReadonlyMap<string, number>) {
    const max = Math.max(1, ...volumes.values());
    for (const state of this.states) {
      const volume = volumes.get(state.placed.id) ?? 0;
      state.active = volume > 0;
      state.targetHeight = state.active
        ? minHeight +
          0.25 +
          (maxHeight - minHeight - 0.25) * (Math.log10(1 + volume) / Math.log10(1 + max))
        : minHeight;
    }
  }

  /** Lights a source up for a batch; `strength` 0 to 1. */
  flare(id: string, strength: number) {
    const i = this.index.get(id);
    if (i !== undefined) {
      const state = this.states[i] as SourceState;
      state.flare = Math.min(1.5, state.flare + strength);
      state.active = true;
    }
  }

  /** The source under the pointer (null for none). */
  setHovered(id: string | null) {
    this.hovered = id === null ? null : (this.index.get(id) ?? null);
  }

  /** The source the viewer chose to follow: the others dim (null for none). */
  setFocused(id: string | null) {
    this.focused = id;
  }

  /** Where a source's light is now, in the scene; null for an unknown source. */
  lightPosition(id: string, into = new THREE.Vector3()): THREE.Vector3 | null {
    const i = this.index.get(id);
    if (i === undefined) {
      return null;
    }
    const state = this.states[i] as SourceState;
    return into.set(state.placed.x, state.height, state.placed.z);
  }

  /** Every source with the position of its light, for labels and picking. */
  each(visit: (id: string, position: THREE.Vector3, active: boolean) => void) {
    const position = new THREE.Vector3();
    for (const state of this.states) {
      visit(
        state.placed.id,
        position.set(state.placed.x, state.height, state.placed.z),
        state.active,
      );
    }
  }

  /** Advances heights and flares by `dt` seconds. */
  update(dt: number) {
    const orbPositions = this.orbs.geometry.getAttribute('position') as
      THREE.BufferAttribute | undefined;
    const sizes = this.orbs.geometry.getAttribute('aSize') as THREE.BufferAttribute | undefined;
    const brightness = this.orbs.geometry.getAttribute('aBrightness') as
      THREE.BufferAttribute | undefined;
    const pylonPositions = this.pylons.geometry.getAttribute('position') as
      THREE.BufferAttribute | undefined;
    if (!orbPositions || !sizes || !brightness || !pylonPositions) {
      return;
    }
    const ease = 1 - Math.exp(-dt * 3.2);
    this.states.forEach((state, i) => {
      state.height += (state.targetHeight - state.height) * ease;
      state.flare *= Math.exp(-dt * 2.2);
      const hovered = this.hovered === i;
      const dimmed = this.focused !== null && this.focused !== state.placed.id;
      orbPositions.setY(i, state.height);
      pylonPositions.setY(i * 2 + 1, state.height);
      sizes.setX(i, (state.active ? 0.62 : 0.4) * (hovered ? 1.55 : 1) * (1 + state.flare * 0.5));
      brightness.setX(
        i,
        ((state.active ? 0.8 : 0.32) + state.flare * 0.9 + (hovered ? 0.5 : 0)) *
          (dimmed ? 0.25 : 1),
      );
    });
    orbPositions.needsUpdate = true;
    sizes.needsUpdate = true;
    brightness.needsUpdate = true;
    pylonPositions.needsUpdate = true;
  }

  dispose() {
    this.orbs.geometry.dispose();
    this.pylons.geometry.dispose();
    this.orbMaterial.dispose();
    this.pylonMaterial.dispose();
  }

  /** Builds the geometry for the current sources: one point per light, one line per pylon, a foot ring each. */
  private rebuild() {
    const count = this.states.length;
    const positions = new Float32Array(count * 3);
    const colors = new Float32Array(count * 3);
    const pylonPositions: number[] = [];
    const pylonColors: number[] = [];
    const pylonAlphas: number[] = [];
    this.states.forEach((state, i) => {
      const { x, z } = state.placed;
      positions.set([x, state.height, z], i * 3);
      colors.set([state.color.r, state.color.g, state.color.b], i * 3);
      // The pylon first (its two vertices are 2i and 2i+1, so update() can move its top).
      pylonPositions.push(x, 0, z, x, state.height, z);
      pylonColors.push(...state.color.toArray(), ...state.color.toArray());
      pylonAlphas.push(0.04, 0.55);
    });
    // Then a small foot ring under each light, in the colour of its domain.
    for (const state of this.states) {
      const foot = new THREE.Color(domainById(state.placed.domain).color);
      for (let k = 0; k < 24; k++) {
        const a = (k / 24) * Math.PI * 2;
        const b = ((k + 1) / 24) * Math.PI * 2;
        pylonPositions.push(
          state.placed.x + Math.cos(a) * 0.3,
          0,
          state.placed.z + Math.sin(a) * 0.3,
          state.placed.x + Math.cos(b) * 0.3,
          0,
          state.placed.z + Math.sin(b) * 0.3,
        );
        pylonColors.push(...foot.toArray(), ...foot.toArray());
        pylonAlphas.push(0.35, 0.35);
      }
    }
    const orbGeometry = new THREE.BufferGeometry();
    orbGeometry.setAttribute(
      'position',
      new THREE.BufferAttribute(positions, 3).setUsage(THREE.DynamicDrawUsage),
    );
    orbGeometry.setAttribute('aColor', new THREE.BufferAttribute(colors, 3));
    orbGeometry.setAttribute(
      'aSize',
      new THREE.BufferAttribute(new Float32Array(count), 1).setUsage(THREE.DynamicDrawUsage),
    );
    orbGeometry.setAttribute(
      'aBrightness',
      new THREE.BufferAttribute(new Float32Array(count), 1).setUsage(THREE.DynamicDrawUsage),
    );
    this.orbs.geometry.dispose();
    this.orbs.geometry = orbGeometry;

    const pylonGeometry = new THREE.BufferGeometry();
    pylonGeometry.setAttribute(
      'position',
      new THREE.Float32BufferAttribute(pylonPositions, 3).setUsage(THREE.DynamicDrawUsage),
    );
    pylonGeometry.setAttribute('aColor', new THREE.Float32BufferAttribute(pylonColors, 3));
    pylonGeometry.setAttribute('aAlpha', new THREE.Float32BufferAttribute(pylonAlphas, 1));
    this.pylons.geometry.dispose();
    this.pylons.geometry = pylonGeometry;
  }
}
