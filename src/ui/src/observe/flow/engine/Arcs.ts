/**
 * The arcs: the paths batches take through the air from their source to the core, as on the great threat maps. Each
 * arc is a curve rising above the ring; a comet trail draws it from the source, its head (and, for a large batch, a
 * short stream of heads behind it) rides it to the core, and a faint track lingers a moment after.
 *
 * Arcs and heads live in fixed pools (ring buffers): launching one overwrites the oldest slot, so a burst of traffic
 * never grows memory. Only the slot written is sent to the GPU.
 */
import * as THREE from 'three';
import { arcMaterial, headMaterial, type SharedUniforms } from './materials';

/** How many arcs and heads can be in the air at once. */
const maxArcs = 420;
const maxHeads = 2400;
/** Points along each arc's curve. */
const samples = 48;
const verticesPerArc = (samples - 1) * 2;

/** One arc to launch. */
export interface ArcLaunch {
  from: THREE.Vector3;
  to: THREE.Vector3;
  color: THREE.Color;
  /** 0 to 1: how bright the trail is (larger batches are brighter). */
  strength: number;
  /** Heads riding the arc: 1 for a small batch, a short stream for a large one. */
  heads: number;
  /** When it leaves (engine seconds) and how long the flight takes. */
  start: number;
  duration: number;
}

/** The arcs of the scene. */
export class Arcs {
  readonly object = new THREE.Group();
  private readonly lineGeometry = new THREE.BufferGeometry();
  private readonly headGeometry = new THREE.BufferGeometry();
  private readonly lineMaterial: THREE.ShaderMaterial;
  private readonly headMaterial: THREE.ShaderMaterial;
  private nextArc = 0;
  private nextHead = 0;
  private readonly control = new THREE.Vector3();
  private readonly sample = new THREE.Vector3();

  constructor(shared: SharedUniforms) {
    const lineAttribute = (size: number) =>
      new THREE.BufferAttribute(new Float32Array(maxArcs * verticesPerArc * size), size).setUsage(
        THREE.DynamicDrawUsage,
      );
    this.lineGeometry.setAttribute('position', lineAttribute(3));
    this.lineGeometry.setAttribute('aT', lineAttribute(1));
    this.lineGeometry.setAttribute('aStart', lineAttribute(1));
    this.lineGeometry.setAttribute('aDuration', lineAttribute(1));
    this.lineGeometry.setAttribute('aStrength', lineAttribute(1));
    this.lineGeometry.setAttribute('aColor', lineAttribute(3));
    // Every slot starts "long ago", so nothing shows until it is launched.
    (this.lineGeometry.getAttribute('aStart') as THREE.BufferAttribute).array.fill(-1e6);
    (this.lineGeometry.getAttribute('aDuration') as THREE.BufferAttribute).array.fill(1);

    const headAttribute = (size: number) =>
      new THREE.BufferAttribute(new Float32Array(maxHeads * size), size).setUsage(
        THREE.DynamicDrawUsage,
      );
    this.headGeometry.setAttribute('position', headAttribute(3));
    for (const name of ['aP0', 'aP1', 'aP2', 'aColor']) {
      this.headGeometry.setAttribute(name, headAttribute(3));
    }
    for (const name of ['aStart', 'aDuration', 'aSize']) {
      this.headGeometry.setAttribute(name, headAttribute(1));
    }
    (this.headGeometry.getAttribute('aStart') as THREE.BufferAttribute).array.fill(-1e6);
    (this.headGeometry.getAttribute('aDuration') as THREE.BufferAttribute).array.fill(1);

    this.lineMaterial = arcMaterial(shared);
    this.headMaterial = headMaterial(shared);
    const lines = new THREE.LineSegments(this.lineGeometry, this.lineMaterial);
    const heads = new THREE.Points(this.headGeometry, this.headMaterial);
    lines.frustumCulled = false;
    heads.frustumCulled = false;
    this.object.add(lines, heads);
  }

  /** Launches one arc and its heads. */
  launch(arc: ArcLaunch) {
    const { from, to } = arc;
    // The control point: above the middle, higher for longer flights, so arcs rise and fall like a throw.
    const distance = from.distanceTo(to);
    this.control.addVectors(from, to).multiplyScalar(0.5);
    this.control.y += distance * 0.42 + 1.1;

    const slot = this.nextArc;
    this.nextArc = (this.nextArc + 1) % maxArcs;
    const first = slot * verticesPerArc;
    const position = this.lineGeometry.getAttribute('position') as THREE.BufferAttribute;
    const along = this.lineGeometry.getAttribute('aT') as THREE.BufferAttribute;
    const start = this.lineGeometry.getAttribute('aStart') as THREE.BufferAttribute;
    const duration = this.lineGeometry.getAttribute('aDuration') as THREE.BufferAttribute;
    const strength = this.lineGeometry.getAttribute('aStrength') as THREE.BufferAttribute;
    const color = this.lineGeometry.getAttribute('aColor') as THREE.BufferAttribute;
    let vertex = first;
    for (let i = 0; i < samples - 1; i++) {
      for (const t of [i / (samples - 1), (i + 1) / (samples - 1)]) {
        this.pointAt(from, this.control, to, t, this.sample);
        position.setXYZ(vertex, this.sample.x, this.sample.y, this.sample.z);
        along.setX(vertex, t);
        start.setX(vertex, arc.start);
        duration.setX(vertex, arc.duration);
        strength.setX(vertex, arc.strength);
        color.setXYZ(vertex, arc.color.r, arc.color.g, arc.color.b);
        vertex++;
      }
    }
    for (const attribute of [position, along, start, duration, strength, color]) {
      attribute.addUpdateRange(first * attribute.itemSize, verticesPerArc * attribute.itemSize);
      attribute.needsUpdate = true;
    }

    const gap = Math.min(0.16, (arc.duration * 0.5) / Math.max(1, arc.heads));
    for (let h = 0; h < arc.heads; h++) {
      this.launchHead(arc, this.control, arc.start + h * gap, h === 0 ? 0.42 : 0.26);
    }
  }

  dispose() {
    this.lineGeometry.dispose();
    this.headGeometry.dispose();
    this.lineMaterial.dispose();
    this.headMaterial.dispose();
  }

  private launchHead(arc: ArcLaunch, control: THREE.Vector3, start: number, size: number) {
    const slot = this.nextHead;
    this.nextHead = (this.nextHead + 1) % maxHeads;
    const write = (name: string, values: number[]) => {
      const attribute = this.headGeometry.getAttribute(name) as THREE.BufferAttribute;
      (attribute.array as Float32Array).set(values, slot * attribute.itemSize);
      attribute.addUpdateRange(slot * attribute.itemSize, attribute.itemSize);
      attribute.needsUpdate = true;
    };
    write('aP0', [arc.from.x, arc.from.y, arc.from.z]);
    write('aP1', [control.x, control.y, control.z]);
    write('aP2', [arc.to.x, arc.to.y, arc.to.z]);
    write('aColor', [arc.color.r, arc.color.g, arc.color.b]);
    write('aStart', [start]);
    write('aDuration', [arc.duration]);
    write('aSize', [size]);
  }

  /** The point at `t` (0 to 1) of the quadratic curve from p0 over p1 to p2. */
  private pointAt(
    p0: THREE.Vector3,
    p1: THREE.Vector3,
    p2: THREE.Vector3,
    t: number,
    into: THREE.Vector3,
  ) {
    const u = 1 - t;
    return into.set(
      u * u * p0.x + 2 * u * t * p1.x + t * t * p2.x,
      u * u * p0.y + 2 * u * t * p1.y + t * t * p2.y,
      u * u * p0.z + 2 * u * t * p1.z + t * t * p2.z,
    );
  }
}
