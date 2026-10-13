/**
 * The connections graph drawn in 3D (three.js): the nodes as glowing lights in their family's colour, the links as
 * lines with a pulse running out from the object, a ring around the object looked at, faint stars behind, and a
 * bloom over it all. The mouse turns (drag), moves (right-drag) and zooms (wheel); the view glides to a node when it
 * is opened. New nodes grow out of the node that brought them.
 *
 * Imperative and owned by GraphView: it is given the graph and what to show, and tells which node is under the
 * pointer and which was clicked. HTML labels are bound to it and placed every frame.
 */
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import type { GraphNode, GraphState, Vec3 } from './layout';

/** What to draw: the graph, the nodes and links shown, the object looked at and the trail to it. */
export interface Drawn {
  state: GraphState;
  nodes: Set<string>;
  edges: Set<string>;
  focus: string | null;
  trail: string[];
}

export interface SceneEvents {
  onHover: (id: string | null, x: number, y: number) => void;
  onPick: (id: string) => void;
}

/** How long a new node takes to grow out (ms). */
const growMs = 900;
/** How far from a node, in pixels, the pointer still names it. */
const reachPx = 16;

const nodeVertex = /* glsl */ `
  attribute vec3 tint;
  attribute float size;
  attribute float glow;
  uniform float uScale;
  varying vec3 vTint;
  varying float vGlow;
  void main() {
    vTint = tint;
    vGlow = glow;
    vec4 view = modelViewMatrix * vec4(position, 1.0);
    gl_PointSize = size * uScale / -view.z;
    gl_Position = projectionMatrix * view;
  }`;

const nodeFragment = /* glsl */ `
  varying vec3 vTint;
  varying float vGlow;
  void main() {
    float d = length(gl_PointCoord * 2.0 - 1.0);
    if (d > 1.0) discard;
    float core = smoothstep(0.36, 0.2, d);
    float halo = pow(1.0 - d, 2.6) * 0.6;
    vec3 colour = vTint * (halo + core * 0.95) + vec3(1.0) * core * (0.25 + 0.35 * vGlow);
    gl_FragColor = vec4(colour, 1.0);
  }`;

const ringFragment = /* glsl */ `
  uniform float uTime;
  varying vec3 vTint;
  varying float vGlow;
  void main() {
    float d = length(gl_PointCoord * 2.0 - 1.0);
    if (d > 1.0) discard;
    float ring = smoothstep(0.05, 0.0, abs(d - 0.62));
    float wave = fract(uTime * 0.42);
    float ripple = vGlow * smoothstep(0.045, 0.0, abs(d - (0.62 + wave * 0.38))) * (1.0 - wave);
    float strength = mix(0.28, 0.75 + 0.25 * sin(uTime * 2.2), vGlow);
    gl_FragColor = vec4(vTint * (ring * strength + ripple * 0.8), 1.0);
  }`;

const edgeVertex = /* glsl */ `
  attribute vec3 tint;
  attribute float along;
  attribute float emph;
  varying vec3 vTint;
  varying float vAlong;
  varying float vEmph;
  void main() {
    vTint = tint;
    vAlong = along;
    vEmph = emph;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }`;

const edgeFragment = /* glsl */ `
  uniform float uTime;
  uniform float uDensity;
  varying vec3 vTint;
  varying float vAlong;
  varying float vEmph;
  void main() {
    float pulse = fract(uTime * 0.3);
    float band = exp(-pow((vAlong - pulse) * 8.0, 2.0));
    float base = mix(0.9 * uDensity, 0.22, vAlong) * (0.7 + vEmph * 1.2);
    gl_FragColor = vec4(vTint * (base + band * (0.35 + vEmph * 0.5)), 1.0);
  }`;

export class GraphScene {
  private readonly renderer: THREE.WebGLRenderer;
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(48, 1, 1, 8000);
  private readonly controls: OrbitControls;
  private readonly composer: EffectComposer;
  private readonly bloom: UnrealBloomPass;
  private readonly nodeMaterial: THREE.ShaderMaterial;
  private readonly ringMaterial: THREE.ShaderMaterial;
  private readonly edgeMaterial: THREE.ShaderMaterial;
  private readonly nodes: THREE.Points;
  private readonly rings: THREE.Points;
  private readonly edges: THREE.LineSegments;
  private readonly resize: ResizeObserver;
  private readonly labels = new Map<string, { element: HTMLElement; place: () => Vec3 | null }>();
  private drawn: Drawn | null = null;
  /** The nodes in buffer order, and each one's size. */
  private order: GraphNode[] = [];
  private sizes: number[] = [];
  private hovered = -1;
  private grew = false;
  private inset = 0;
  private frame = 0;
  private readonly started = performance.now();
  private glide: {
    target: [THREE.Vector3, THREE.Vector3];
    eye: [THREE.Vector3, THREE.Vector3];
    start: number;
  } | null = null;
  private pointer: { x: number; y: number; downX: number; downY: number } | null = null;

  private readonly container: HTMLElement;
  private readonly events: SceneEvents;
  private readonly still: boolean;

  constructor(container: HTMLElement, events: SceneEvents, still: boolean) {
    this.container = container;
    this.events = events;
    this.still = still;
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setClearColor(0x000000, 0);
    container.appendChild(this.renderer.domElement);

    this.camera.position.set(30, 55, 210);
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.rotateSpeed = 0.6;
    this.controls.zoomSpeed = 0.9;
    this.controls.minDistance = 25;
    this.controls.maxDistance = 1600;
    this.controls.autoRotate = !still;
    this.controls.autoRotateSpeed = 0.35;
    this.controls.addEventListener('start', () => {
      this.controls.autoRotate = false;
      this.glide = null;
    });

    this.nodeMaterial = new THREE.ShaderMaterial({
      vertexShader: nodeVertex,
      fragmentShader: nodeFragment,
      uniforms: { uScale: { value: 400 } },
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    this.ringMaterial = new THREE.ShaderMaterial({
      vertexShader: nodeVertex,
      fragmentShader: ringFragment,
      uniforms: { uScale: { value: 400 }, uTime: { value: 0 } },
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    this.edgeMaterial = new THREE.ShaderMaterial({
      vertexShader: edgeVertex,
      fragmentShader: edgeFragment,
      uniforms: { uTime: { value: 0 }, uDensity: { value: 1 } },
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    this.nodes = new THREE.Points(new THREE.BufferGeometry(), this.nodeMaterial);
    this.rings = new THREE.Points(new THREE.BufferGeometry(), this.ringMaterial);
    this.edges = new THREE.LineSegments(new THREE.BufferGeometry(), this.edgeMaterial);
    for (const object of [this.edges, this.nodes, this.rings]) object.frustumCulled = false;
    this.scene.add(stars(), this.edges, this.nodes, this.rings);

    this.composer = new EffectComposer(this.renderer);
    this.composer.addPass(new RenderPass(this.scene, this.camera));
    this.bloom = new UnrealBloomPass(new THREE.Vector2(1, 1), 0.85, 0.6, 0.08);
    this.composer.addPass(this.bloom);
    this.composer.addPass(new OutputPass());

    const canvas = this.renderer.domElement;
    canvas.addEventListener('pointermove', this.onMove);
    canvas.addEventListener('pointerdown', this.onDown);
    canvas.addEventListener('pointerup', this.onUp);
    canvas.addEventListener('pointerleave', this.onLeave);
    this.resize = new ResizeObserver(() => this.fit());
    this.resize.observe(container);
    this.fit();
    this.frame = requestAnimationFrame(this.tick);
    if (import.meta.env.DEV) {
      // The graph in the browser console while developing (and for the screenshot scripts).
      (window as unknown as { analyzeGraph?: GraphScene }).analyzeGraph = this;
    }
  }

  /** Where a node is on the screen, in pixels from the view's top left; null when behind the camera. */
  screenOf(id: string): { x: number; y: number } | null {
    const place = this.positionOf(id);
    if (!place) return null;
    const vector = new THREE.Vector3(...place).project(this.camera);
    if (vector.z > 1) return null;
    return {
      x: ((vector.x + 1) / 2) * this.container.clientWidth,
      y: ((1 - vector.y) / 2) * this.container.clientHeight,
    };
  }

  /** Draws this graph (keeping the view where it is). */
  set(drawn: Drawn) {
    this.drawn = drawn;
    this.order = [...drawn.nodes].flatMap((id) => drawn.state.nodes.get(id) ?? []);
    this.sizes = this.order.map((node) => sizeOf(node, drawn));
    this.hovered = -1;
    this.build(performance.now());
  }

  /** Keeps the graph's middle clear of a panel this many pixels wide on the right. */
  setInset(pixels: number) {
    this.inset = pixels;
    this.fit();
  }

  /** Binds an HTML label to a place, moved there every frame; null unbinds it. */
  bindLabel(key: string, element: HTMLElement | null, place: () => Vec3 | null) {
    if (element) this.labels.set(key, { element, place });
    else this.labels.delete(key);
  }

  /** Where a node is now (it may still be growing). */
  positionOf(id: string): Vec3 | null {
    const node = this.drawn?.state.nodes.get(id);
    return node ? placeAt(node, performance.now()) : null;
  }

  /** Glides the view to a node, closer when far away. */
  focusOn(id: string) {
    const node = this.drawn?.state.nodes.get(id);
    if (!node) return;
    const target = new THREE.Vector3(...node.position);
    const offset = this.camera.position.clone().sub(this.controls.target);
    // Far enough to see the node's own connections around it.
    let spread = 0;
    for (const other of this.drawn?.state.nodes.values() ?? []) {
      if (other.parent !== id) continue;
      spread = Math.max(spread, target.distanceTo(new THREE.Vector3(...other.position)));
    }
    const distance = THREE.MathUtils.clamp(Math.max(offset.length() * 0.8, spread * 2.4), 110, 600);
    offset.setLength(distance);
    this.glide = {
      target: [this.controls.target.clone(), target],
      eye: [this.camera.position.clone(), target.clone().add(offset)],
      start: performance.now(),
    };
  }

  zoomBy(factor: number) {
    const offset = this.camera.position.clone().sub(this.controls.target).multiplyScalar(factor);
    this.camera.position.copy(this.controls.target).add(offset);
  }

  /** The whole graph in view, from the front. */
  showAll() {
    const nodes = this.order;
    if (nodes.length === 0) return;
    const box = new THREE.Box3();
    for (const node of nodes) box.expandByPoint(new THREE.Vector3(...node.position));
    const centre = box.getCenter(new THREE.Vector3());
    const radius = Math.max(60, box.getSize(new THREE.Vector3()).length() / 2);
    this.glide = {
      target: [this.controls.target.clone(), centre],
      eye: [
        this.camera.position.clone(),
        centre.clone().add(new THREE.Vector3(0.15, 0.25, 1).setLength(radius * 2.4)),
      ],
      start: performance.now(),
    };
  }

  dispose() {
    cancelAnimationFrame(this.frame);
    this.resize.disconnect();
    const canvas = this.renderer.domElement;
    canvas.removeEventListener('pointermove', this.onMove);
    canvas.removeEventListener('pointerdown', this.onDown);
    canvas.removeEventListener('pointerup', this.onUp);
    canvas.removeEventListener('pointerleave', this.onLeave);
    this.controls.dispose();
    this.scene.traverse((object) => {
      if (object instanceof THREE.Points || object instanceof THREE.LineSegments) {
        object.geometry.dispose();
        (object.material as THREE.Material).dispose();
      }
    });
    this.composer.dispose();
    this.renderer.dispose();
    canvas.remove();
  }

  private readonly tick = (time: number) => {
    this.frame = requestAnimationFrame(this.tick);
    const seconds = this.still ? 0 : (time - this.started) / 1000;
    this.ringMaterial.uniforms.uTime!.value = seconds;
    this.edgeMaterial.uniforms.uTime!.value = seconds;
    // While nodes grow, and once more when they are done, so they end exactly in place however slow the frames.
    const growing = this.order.some((node) => time - node.born < growMs);
    if (growing || this.grew) this.build(time);
    this.grew = growing;
    if (this.glide) {
      const t = Math.min(1, (time - this.glide.start) / 1100);
      const eased = t < 0.5 ? 4 * t * t * t : 1 - (-2 * t + 2) ** 3 / 2;
      this.controls.target.lerpVectors(this.glide.target[0], this.glide.target[1], eased);
      this.camera.position.lerpVectors(this.glide.eye[0], this.glide.eye[1], eased);
      if (t >= 1) this.glide = null;
    }
    this.controls.update();
    this.composer.render();
    this.placeLabels();
  };

  /** Fills the buffers: nodes, rings (the object and the expanded nodes) and links, at their places at `time`. */
  private build(time: number) {
    const drawn = this.drawn;
    if (!drawn) return;
    const places = new Map<string, Vec3>();
    for (const node of this.order) places.set(node.id, placeAt(node, time));

    const nodeAttributes = attributesOf(this.order.length);
    this.order.forEach((node, index) => {
      put(
        nodeAttributes,
        index,
        places.get(node.id)!,
        node.color,
        this.sizes[index]! * (index === this.hovered ? 1.5 : 1),
        node.id === drawn.focus ? 1 : 0,
      );
    });
    setAttributes(this.nodes.geometry, nodeAttributes);

    const ringed = this.order.filter((node) => node.id === drawn.focus || node.total !== undefined);
    const ringAttributes = attributesOf(ringed.length);
    ringed.forEach((node, index) => {
      const focus = node.id === drawn.focus;
      put(ringAttributes, index, places.get(node.id)!, node.color, focus ? 26 : 17, focus ? 1 : 0);
    });
    setAttributes(this.rings.geometry, ringAttributes);

    const trail = new Set(drawn.trail.slice(1).map((id, index) => pairOf(drawn.trail[index]!, id)));
    const edges = [...drawn.edges].flatMap((id) => drawn.state.edges.get(id) ?? []);
    const position = new Float32Array(edges.length * 6);
    const tint = new Float32Array(edges.length * 6);
    const along = new Float32Array(edges.length * 2);
    const emph = new Float32Array(edges.length * 2);
    edges.forEach((edge, index) => {
      const from = places.get(edge.owner);
      const to = places.get(edge.other);
      if (!from || !to) return;
      const colour = new THREE.Color(drawn.state.nodes.get(edge.other)?.color ?? '#8fb6ff');
      position.set([...from, ...to], index * 6);
      tint.set([colour.r, colour.g, colour.b, colour.r, colour.g, colour.b], index * 6);
      along.set([0, 1], index * 2);
      const strong = trail.has(pairOf(edge.owner, edge.other)) ? 1 : 0;
      emph.set([strong, strong], index * 2);
    });
    // Where hundreds of links meet, each is fainter, so their sum stays a glow and not a blank.
    this.edgeMaterial.uniforms.uDensity!.value = Math.min(
      1,
      4 / Math.sqrt(Math.max(1, edges.length)),
    );
    const geometry = this.edges.geometry;
    geometry.setAttribute('position', new THREE.BufferAttribute(position, 3));
    geometry.setAttribute('tint', new THREE.BufferAttribute(tint, 3));
    geometry.setAttribute('along', new THREE.BufferAttribute(along, 1));
    geometry.setAttribute('emph', new THREE.BufferAttribute(emph, 1));
  }

  private fit() {
    const width = Math.max(1, this.container.clientWidth);
    const height = Math.max(1, this.container.clientHeight);
    this.renderer.setSize(width, height);
    this.composer.setSize(width, height);
    this.bloom.resolution.set(width, height);
    this.camera.aspect = width / height;
    // The middle of the view moves left by half the panel, so the graph sits in the free part of the screen.
    this.camera.setViewOffset(width, height, this.inset / 2, 0, width, height);
    this.camera.updateProjectionMatrix();
    const scale =
      (height * this.renderer.getPixelRatio()) / (2 * Math.tan((this.camera.fov * Math.PI) / 360));
    this.nodeMaterial.uniforms.uScale!.value = scale;
    this.ringMaterial.uniforms.uScale!.value = scale;
  }

  private placeLabels() {
    const width = this.container.clientWidth;
    const height = this.container.clientHeight;
    const vector = new THREE.Vector3();
    for (const { element, place } of this.labels.values()) {
      const at = place();
      if (!at) {
        element.style.opacity = '0';
        continue;
      }
      vector.set(...at).project(this.camera);
      const visible = vector.z < 1 && Math.abs(vector.x) < 1.2 && Math.abs(vector.y) < 1.2;
      const x = ((vector.x + 1) / 2) * width;
      const y = ((1 - vector.y) / 2) * height;
      element.style.opacity = visible ? '1' : '0';
      element.style.transform = `translate3d(${x.toFixed(1)}px, ${y.toFixed(1)}px, 0)`;
    }
  }

  /** The node nearest the pointer within reach, by its place on the screen. */
  private nodeAt(x: number, y: number): number {
    const width = this.container.clientWidth;
    const height = this.container.clientHeight;
    const vector = new THREE.Vector3();
    const now = performance.now();
    let best = -1;
    let bestDistance = Infinity;
    this.order.forEach((node, index) => {
      vector.set(...placeAt(node, now)).project(this.camera);
      if (vector.z > 1) return;
      const dx = ((vector.x + 1) / 2) * width - x;
      const dy = ((1 - vector.y) / 2) * height - y;
      const distance = Math.hypot(dx, dy);
      if (distance < reachPx + this.sizes[index]! * 0.4 && distance < bestDistance) {
        bestDistance = distance;
        best = index;
      }
    });
    return best;
  }

  private readonly onMove = (event: PointerEvent) => {
    const { x, y } = local(event, this.renderer.domElement);
    if (this.pointer) Object.assign(this.pointer, { x, y });
    else this.pointer = { x, y, downX: Number.NaN, downY: Number.NaN };
    const index = this.nodeAt(x, y);
    if (index !== this.hovered) {
      this.hovered = index;
      this.build(performance.now());
    }
    this.renderer.domElement.style.cursor = index >= 0 ? 'pointer' : 'grab';
    this.events.onHover(index >= 0 ? this.order[index]!.id : null, x, y);
  };

  private readonly onDown = (event: PointerEvent) => {
    const { x, y } = local(event, this.renderer.domElement);
    this.pointer = { x, y, downX: x, downY: y };
  };

  private readonly onUp = (event: PointerEvent) => {
    const { x, y } = local(event, this.renderer.domElement);
    const down = this.pointer;
    if (!down || Math.hypot(x - down.downX, y - down.downY) > 5) return;
    const index = this.nodeAt(x, y);
    if (index >= 0) this.events.onPick(this.order[index]!.id);
  };

  private readonly onLeave = () => {
    this.pointer = null;
    if (this.hovered >= 0) {
      this.hovered = -1;
      this.build(performance.now());
    }
    this.events.onHover(null, 0, 0);
  };
}

/** A node's size: the object and the first one largest, expanded nodes larger than the rest. */
function sizeOf(node: GraphNode, drawn: Drawn): number {
  if (node.id === drawn.focus) return 13;
  if (node.id === drawn.state.root) return 11;
  return node.total !== undefined ? 9 : 6;
}

/** Where a node is at a moment: on its way out from its origin while it grows. */
export function placeAt(node: GraphNode, time: number): Vec3 {
  const t = Math.min(1, Math.max(0, (time - node.born) / growMs));
  if (t >= 1) return node.position;
  const eased = 1 - (1 - t) ** 3;
  return [0, 1, 2].map(
    (axis) => node.origin[axis]! + (node.position[axis]! - node.origin[axis]!) * eased,
  ) as Vec3;
}

function pairOf(a: string, b: string): string {
  return a < b ? `${a}\n${b}` : `${b}\n${a}`;
}

function attributesOf(count: number) {
  return {
    position: new Float32Array(count * 3),
    tint: new Float32Array(count * 3),
    size: new Float32Array(count),
    glow: new Float32Array(count),
  };
}

function put(
  attributes: ReturnType<typeof attributesOf>,
  index: number,
  place: Vec3,
  colour: string,
  size: number,
  glow: number,
) {
  const tint = new THREE.Color(colour);
  attributes.position.set(place, index * 3);
  attributes.tint.set([tint.r, tint.g, tint.b], index * 3);
  attributes.size[index] = size;
  attributes.glow[index] = glow;
}

function setAttributes(
  geometry: THREE.BufferGeometry,
  attributes: ReturnType<typeof attributesOf>,
) {
  geometry.setAttribute('position', new THREE.BufferAttribute(attributes.position, 3));
  geometry.setAttribute('tint', new THREE.BufferAttribute(attributes.tint, 3));
  geometry.setAttribute('size', new THREE.BufferAttribute(attributes.size, 1));
  geometry.setAttribute('glow', new THREE.BufferAttribute(attributes.glow, 1));
}

/** Faint stars far behind the graph, so turning it feels like turning in space. */
function stars(): THREE.Points {
  const count = 1600;
  const position = new Float32Array(count * 3);
  for (let index = 0; index < count; index++) {
    const y = Math.random() * 2 - 1;
    const theta = Math.random() * Math.PI * 2;
    const radius = 2200 + Math.random() * 2400;
    const ring = Math.sqrt(1 - y * y);
    position.set(
      [Math.cos(theta) * ring * radius, y * radius, Math.sin(theta) * ring * radius],
      index * 3,
    );
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(position, 3));
  const material = new THREE.PointsMaterial({
    color: 0x9fb4d8,
    size: 1.3,
    sizeAttenuation: false,
    transparent: true,
    opacity: 0.55,
    depthWrite: false,
  });
  return new THREE.Points(geometry, material);
}

function local(event: PointerEvent, element: HTMLElement): { x: number; y: number } {
  const box = element.getBoundingClientRect();
  return { x: event.clientX - box.left, y: event.clientY - box.top };
}
