/**
 * The flow engine: the 3D scene of the live flow, independent of React. FlowCanvas.tsx creates one, hands it the
 * sources and the batches to show, and places the HTML labels where the engine says each light is.
 *
 * The scene, from the ground up: an engraved ground (Ground), the sources on their ring (Sources), the core at the
 * centre (Core), the arcs between them (Arcs), the sparks of refused records (Sparks), ripples on the ground
 * (Ripples), and far stars (Stars). One render loop draws it through a bloom pass, and stops whenever the section is
 * out of view.
 *
 * The camera orbits slowly on its own and can be turned by dragging. A plain mouse wheel scrolls the page, as on
 * every other part of it; zoom is a pinch (or Ctrl and the wheel) and the buttons of the section.
 */
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import type { DomainId } from '../../domains';
import { domainGlow, sceneColors } from '../../theme/palette';
import { Arcs } from './Arcs';
import { Core, coreHeight, coreRadius } from './Core';
import { Ground } from './Ground';
import { ringLayout, type RingLayout } from './layout';
import { sharedUniforms, type SharedUniforms } from './materials';
import { Ripples } from './Ripples';
import { Sources } from './Sources';
import { Sparks } from './Sparks';
import { Stars } from './Stars';

/** A batch to show: what one source sent, and what became of it. */
export interface FlowBatch {
  source: string;
  received: number;
  /** Records stored (new and new versions). */
  written: number;
  rejected: number;
}

/** Where a point of the scene appears on the canvas, in CSS pixels. */
export interface ScreenPoint {
  x: number;
  y: number;
  /** Distance from the camera: larger is farther. */
  depth: number;
  /** False when the point is behind the camera or off the canvas. */
  visible: boolean;
}

/** Settings of the engine. */
export interface FlowEngineOptions {
  canvas: HTMLCanvasElement;
  /** True when the viewer asked their system for less motion: no intro, no drifting camera, no breathing. */
  reducedMotion: boolean;
  /** Called after each frame is drawn (to move labels). */
  onFrame?: () => void;
}

const fov = 34;
const restDistance = 35;
const restPolar = (57 * Math.PI) / 180;
const restAzimuth = (-24 * Math.PI) / 180;
const target = new THREE.Vector3(0, 1.25, 0);
const minDistance = 16;
const maxDistance = 54;
const introDistance = 62;
const introSeconds = 2.8;

/** The 3D scene of the live flow. */
export class FlowEngine {
  private readonly renderer: THREE.WebGLRenderer;
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(fov, 1, 0.1, 400);
  private readonly controls: OrbitControls;
  private readonly composer: EffectComposer;
  private readonly bloom: UnrealBloomPass;
  private readonly shared: SharedUniforms = sharedUniforms();
  private readonly ground: Ground;
  private readonly core: Core;
  private readonly sources: Sources;
  private readonly arcs: Arcs;
  private readonly sparks: Sparks;
  private readonly ripples: Ripples;
  private readonly stars: Stars;
  private readonly options: FlowEngineOptions;
  private layout: RingLayout = ringLayout([]);
  private domainOf = new Map<string, DomainId>();
  private readonly clock = new THREE.Timer();
  /** Pulses of the core waiting for their arc to arrive, soonest first: [engine seconds, strength]. */
  private pulses: [number, number][] = [];
  private width = 1;
  private height = 1;
  private running = false;
  private idleSince = 0;
  /** When the first frame was drawn (ms): the intro runs on the wall clock, whatever the frame rate. */
  private startedAt: number | null = null;
  /** The pixel ratio in use, lowered when frames run slow and raised again when they run fast. */
  private pixelRatio = Math.min(window.devicePixelRatio, 2);
  private frameCost = 16;
  private slowSince = 0;
  private fastSince = 0;
  private readonly from = new THREE.Vector3();
  private readonly to = new THREE.Vector3();
  private readonly projected = new THREE.Vector3();

  constructor(options: FlowEngineOptions) {
    this.options = options;
    this.renderer = new THREE.WebGLRenderer({
      canvas: options.canvas,
      antialias: false,
      powerPreference: 'high-performance',
    });
    this.renderer.setPixelRatio(this.pixelRatio);
    this.renderer.setClearColor(new THREE.Color(sceneColors.background), 1);
    this.renderer.toneMapping = THREE.NeutralToneMapping;
    this.renderer.toneMappingExposure = 1;

    this.ground = new Ground(this.shared, this.layout);
    this.core = new Core(this.shared);
    this.sources = new Sources(this.shared);
    this.arcs = new Arcs(this.shared);
    this.sparks = new Sparks(this.shared);
    this.ripples = new Ripples();
    this.stars = new Stars(this.shared);
    this.scene.add(
      this.stars.object,
      this.ground.object,
      this.ripples.object,
      this.sources.object,
      this.core.object,
      this.arcs.object,
      this.sparks.object,
    );

    const intro = options.reducedMotion ? restDistance : introDistance;
    this.placeCamera(
      intro,
      options.reducedMotion ? restPolar : 0.55,
      restAzimuth - (options.reducedMotion ? 0 : 0.5),
    );
    this.controls = new OrbitControls(this.camera, options.canvas);
    this.controls.target.copy(target);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.07;
    this.controls.rotateSpeed = 0.5;
    this.controls.enableZoom = false;
    this.controls.enablePan = false;
    this.controls.minPolarAngle = (24 * Math.PI) / 180;
    this.controls.maxPolarAngle = (83 * Math.PI) / 180;
    this.controls.autoRotate = !options.reducedMotion;
    this.controls.autoRotateSpeed = 0.32;
    this.controls.enabled = options.reducedMotion;
    this.controls.addEventListener('start', () => {
      this.controls.autoRotate = false;
    });
    this.controls.addEventListener('end', () => {
      this.idleSince = this.shared.uTime.value;
    });
    options.canvas.addEventListener('wheel', this.onWheel, { passive: false });

    const renderTarget = new THREE.WebGLRenderTarget(1, 1, {
      type: THREE.HalfFloatType,
      samples: 4,
    });
    this.composer = new EffectComposer(this.renderer, renderTarget);
    this.composer.addPass(new RenderPass(this.scene, this.camera));
    this.bloom = new UnrealBloomPass(new THREE.Vector2(1, 1), 0.62, 0.55, 0.22);
    this.composer.addPass(this.bloom);
    this.composer.addPass(new OutputPass());

    if (options.reducedMotion) {
      this.shared.uIntro.value = 1;
    }
  }

  /** Engine time, in seconds: the clock arcs and effects are scheduled on. */
  get time(): number {
    return this.shared.uTime.value;
  }

  /** Places the sources on the ring. */
  setSources(sources: readonly { id: string; domain: DomainId }[]) {
    this.layout = ringLayout(sources);
    this.domainOf = new Map(sources.map((source) => [source.id, source.domain]));
    this.ground.setLayout(this.layout);
    this.sources.setSources(this.layout.sources);
  }

  /** The ring's layout: where each source and each domain's sector is. */
  get ring(): RingLayout {
    return this.layout;
  }

  /** How much each source sent over the window: the heights of the pylons. */
  setVolumes(volumes: ReadonlyMap<string, number>) {
    this.sources.setVolumes(volumes);
  }

  /**
   * Shows a batch: its source flares, an arc carries it to the core, and on arrival the core pulses, a ripple
   * spreads, and refused records fly back as sparks. `delay` seconds from now.
   */
  launch(batch: FlowBatch, delay = 0): boolean {
    const domain = this.domainOf.get(batch.source);
    if (!domain || !this.sources.lightPosition(batch.source, this.from)) {
      return false;
    }
    const start = this.time + delay;
    // Arrive on the side of the core that faces the source, a little scattered so arcs do not stack.
    this.to.set(this.from.x, this.from.y - coreHeight, this.from.z).normalize();
    this.to.x += (Math.random() - 0.5) * 0.5;
    this.to.y += (Math.random() - 0.5) * 0.5;
    this.to.z += (Math.random() - 0.5) * 0.5;
    this.to
      .normalize()
      .multiplyScalar(coreRadius * 1.02)
      .add(new THREE.Vector3(0, coreHeight, 0));

    const strength = Math.min(1, Math.max(0.16, Math.log10(1 + batch.received) / Math.log10(5001)));
    const duration = 1.55 + this.from.distanceTo(this.to) * 0.035 + Math.random() * 0.3;
    this.arcs.launch({
      from: this.from,
      to: this.to,
      color: new THREE.Color(domainGlow(domain)),
      strength: 0.35 + strength * 0.65,
      heads: 1 + Math.round(strength * 3),
      start,
      duration,
    });
    this.sources.flare(batch.source, 0.4 + strength * 0.6);
    const arrival = start + duration;
    const pulse: [number, number] = [arrival, batch.written > 0 ? 0.25 + strength * 0.6 : 0.1];
    const at = this.pulses.findIndex((item) => item[0] > arrival);
    this.pulses.splice(at === -1 ? this.pulses.length : at, 0, pulse);
    const refusedShare = batch.received > 0 ? batch.rejected / batch.received : 0;
    this.ripples.emit(
      arrival,
      batch.written > 0 ? strength : strength * 0.4,
      refusedShare > 0.5 ? new THREE.Color(sceneColors.rejected) : undefined,
    );
    if (batch.rejected > 0) {
      const sparks = Math.min(70, Math.round(5 + Math.log2(1 + batch.rejected) * 4.5));
      this.sparks.burst(this.to, this.from, sparks, arrival);
    }
    return true;
  }

  /** The source under the pointer; it grows and its label opens. */
  setHovered(id: string | null) {
    this.sources.setHovered(id);
  }

  /** The source being followed; the others dim. */
  setFocused(id: string | null) {
    this.sources.setFocused(id);
  }

  /** Where a source's light appears on the canvas. */
  projectSource(id: string): ScreenPoint | null {
    const position = this.sources.lightPosition(id, this.projected);
    return position ? this.toScreen(position) : null;
  }

  /** Where a point of the ground (at a radius and an angle) appears on the canvas. */
  projectGround(radius: number, angle: number, y = 0): ScreenPoint {
    return this.toScreen(
      this.projected.set(Math.cos(angle) * radius, y, -Math.sin(angle) * radius),
    );
  }

  /** Where the core appears on the canvas (just under its sphere). */
  projectCore(): ScreenPoint {
    return this.toScreen(this.projected.set(0, coreHeight - coreRadius - 1.15, 0));
  }

  /** The source whose light is nearest to a canvas point, within `radius` pixels. */
  pick(x: number, y: number, radius = 22): string | null {
    let best: string | null = null;
    let bestDistance = radius;
    this.sources.each((id, position) => {
      const screen = this.toScreen(this.projected.copy(position));
      const distance = Math.hypot(screen.x - x, screen.y - y);
      if (screen.visible && distance < bestDistance) {
        best = id;
        bestDistance = distance;
      }
    });
    return best;
  }

  /** Brings the camera closer (factor < 1) or farther (factor > 1). */
  zoomBy(factor: number) {
    const offset = this.camera.position.clone().sub(this.controls.target);
    offset.setLength(Math.min(maxDistance, Math.max(minDistance, offset.length() * factor)));
    this.camera.position.copy(this.controls.target).add(offset);
    this.controls.update();
  }

  /** Back to the resting view, turning slowly again. */
  resetView() {
    this.placeCamera(restDistance, restPolar, restAzimuth);
    this.controls.autoRotate = !this.options.reducedMotion;
    this.controls.update();
  }

  /** Sets the size of the canvas, in CSS pixels. */
  resize(width: number, height: number) {
    this.width = Math.max(1, width);
    this.height = Math.max(1, height);
    this.renderer.setPixelRatio(this.pixelRatio);
    this.renderer.setSize(this.width, this.height, false);
    this.composer.setPixelRatio(this.pixelRatio);
    this.composer.setSize(this.width, this.height);
    this.bloom.resolution.set(this.width / 2, this.height / 2);
    // Pull back on narrow canvases so the whole ring stays in view.
    const baseFov = this.width / this.height < 1.25 ? fov * 1.15 : fov;
    const halfTan = Math.tan(((baseFov / 2) * Math.PI) / 180);
    // Centre the ring in the space the instruments leave free (left of the arrivals column, above the time bar):
    // the camera sees a larger frame and the canvas shows its lower-right part, so nothing is cropped or scaled.
    const shiftX = this.width > 1100 ? 150 : 0;
    const shiftY = 40;
    const fullWidth = this.width + 2 * shiftX;
    const fullHeight = this.height + 2 * shiftY;
    this.camera.aspect = fullWidth / fullHeight;
    this.camera.fov = (2 * Math.atan((halfTan * fullHeight) / this.height) * 180) / Math.PI;
    this.camera.setViewOffset(
      fullWidth,
      fullHeight,
      2 * shiftX,
      2 * shiftY,
      this.width,
      this.height,
    );
    this.camera.updateProjectionMatrix();
    this.shared.uPointScale.value = (this.height * this.pixelRatio) / (2 * halfTan);
  }

  /** Runs the render loop (true) or stops it (false), when the section enters or leaves the view. */
  setRunning(running: boolean) {
    if (running === this.running) {
      return;
    }
    this.running = running;
    this.renderer.setAnimationLoop(running ? this.frame : null);
  }

  dispose() {
    this.setRunning(false);
    this.options.canvas.removeEventListener('wheel', this.onWheel);
    this.controls.dispose();
    for (const part of [
      this.ground,
      this.core,
      this.sources,
      this.arcs,
      this.sparks,
      this.ripples,
      this.stars,
    ]) {
      part.dispose();
    }
    this.composer.dispose();
    this.renderer.dispose();
  }

  /** One frame: advance the clock, the camera and every part; draw; let the labels follow. */
  private frame = (timestamp: number) => {
    this.clock.update(timestamp);
    const realDt = this.clock.getDelta();
    const dt = Math.min(0.1, realDt);
    const time = this.shared.uTime.value + dt;
    this.shared.uTime.value = time;
    this.adaptResolution(timestamp, realDt * 1000);

    if (this.shared.uIntro.value < 1) {
      this.startedAt ??= timestamp;
      const intro = Math.min(1, (timestamp - this.startedAt) / 1000 / introSeconds);
      this.shared.uIntro.value = intro;
      const eased = 1 - (1 - intro) ** 3;
      this.placeCamera(
        introDistance + (restDistance - introDistance) * eased,
        0.55 + (restPolar - 0.55) * eased,
        restAzimuth - 0.5 * (1 - eased),
      );
      if (intro >= 1) {
        this.controls.enabled = true;
      }
    } else {
      if (
        !this.controls.autoRotate &&
        !this.options.reducedMotion &&
        time - this.idleSince > 12 &&
        this.idleSince > 0
      ) {
        this.controls.autoRotate = true;
      }
      this.controls.update(dt);
    }

    while (this.pulses.length > 0 && (this.pulses[0]?.[0] ?? Infinity) <= time) {
      this.core.pulseBy(this.pulses.shift()?.[1] ?? 0);
    }

    this.core.update(dt, this.camera, this.options.reducedMotion);
    this.sources.update(dt);
    this.ripples.update(time);
    this.composer.render(dt);
    this.options.onFrame?.();
  };

  /**
   * Keeps the frame rate up on modest graphics: after two seconds of slow frames (over 24 ms), draw fewer pixels;
   * after five seconds of fast ones (under 11 ms), draw more again, up to the screen's own density.
   */
  private adaptResolution(timestamp: number, frameMs: number) {
    if (frameMs <= 0 || frameMs > 250) {
      return;
    }
    this.frameCost += (frameMs - this.frameCost) * 0.05;
    const best = Math.min(window.devicePixelRatio, 2);
    if (this.frameCost > 24 && this.pixelRatio > 1) {
      this.slowSince ||= timestamp;
      this.fastSince = 0;
      if (timestamp - this.slowSince > 2_000) {
        this.pixelRatio = Math.max(1, this.pixelRatio - 0.5);
        this.slowSince = 0;
        this.resize(this.width, this.height);
      }
    } else if (this.frameCost < 11 && this.pixelRatio < best) {
      this.fastSince ||= timestamp;
      this.slowSince = 0;
      if (timestamp - this.fastSince > 5_000) {
        this.pixelRatio = Math.min(best, this.pixelRatio + 0.5);
        this.fastSince = 0;
        this.resize(this.width, this.height);
      }
    } else {
      this.slowSince = 0;
      this.fastSince = 0;
    }
  }

  /** Turns a plain wheel into a page scroll and a pinch (Ctrl and wheel) into a zoom. */
  private onWheel = (event: WheelEvent) => {
    if (!event.ctrlKey) {
      return;
    }
    event.preventDefault();
    this.zoomBy(Math.exp(event.deltaY * 0.01));
  };

  private placeCamera(distance: number, polar: number, azimuth: number) {
    this.camera.position.set(
      target.x + distance * Math.sin(polar) * Math.sin(azimuth),
      target.y + distance * Math.cos(polar),
      target.z + distance * Math.sin(polar) * Math.cos(azimuth),
    );
    this.camera.lookAt(target);
  }

  private toScreen(point: THREE.Vector3): ScreenPoint {
    const depth = point.distanceTo(this.camera.position);
    point.project(this.camera);
    const visible =
      point.z < 1 && point.x > -1.1 && point.x < 1.1 && point.y > -1.1 && point.y < 1.1;
    return {
      x: ((point.x + 1) / 2) * this.width,
      y: ((1 - point.y) / 2) * this.height,
      depth,
      visible,
    };
  }
}
