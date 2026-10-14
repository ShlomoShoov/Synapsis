/**
 * The intro's astronaut in 3D (three.js), drawn over the CSS sky: modelled from simple solids in the weightless
 * posture (arms raised and forward, hips and knees bent), with physical materials (the suit's fabric with a sheen
 * and a weave, the hard shell, the bearings, a gold visor that mirrors the room) and three lights: the sun warm from
 * the upper right, a cool rim from behind, the Earth's blue from below. No file to load: the model, the weave and the
 * reflections are made here.
 *
 * It drifts and turns towards the pointer; on fall() it lets go, tumbles away towards the Earth faster and faster and
 * glows as it meets the air (the same 2.2 s as the CSS fall it replaces). Loaded only when the intro shows
 * (Intro.tsx); the SVG drawing stands in until the first frame and wherever WebGL is missing.
 */
import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';

export interface AstronautScene {
  /** Lets go: the fall towards the Earth. */
  fall(): void;
  dispose(): void;
}

/** How long the fall lasts, as the CSS fall (Intro.module.css). */
const fallMs = 2200;

type Vec = [number, number, number];

/** A fine weave for the suit's fabric, as a bump map. */
function weave(): THREE.CanvasTexture | null {
  const size = 128;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d');
  if (!context) return null;
  const image = context.createImageData(size, size);
  for (let y = 0; y < size; y += 1) {
    for (let x = 0; x < size; x += 1) {
      const thread = (Math.sin(x * 1.6) + Math.sin(y * 1.6)) * 18;
      const value = 128 + thread + (Math.random() - 0.5) * 40;
      const i = (y * size + x) * 4;
      image.data[i] = value;
      image.data[i + 1] = value;
      image.data[i + 2] = value;
      image.data[i + 3] = 255;
    }
  }
  context.putImageData(image, 0, 0);
  const texture = new THREE.CanvasTexture(canvas);
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.set(6, 6);
  return texture;
}

/** The glow of the air on the way down: a soft flame, brightest at its foot, gone before its edges. */
function flame(): THREE.CanvasTexture | null {
  const canvas = document.createElement('canvas');
  canvas.width = 64;
  canvas.height = 128;
  const context = canvas.getContext('2d');
  if (!context) return null;
  context.translate(32, 64);
  context.scale(1, 2);
  const glow = context.createRadialGradient(0, 18, 0, 0, 4, 30);
  glow.addColorStop(0, 'rgba(255,245,220,1)');
  glow.addColorStop(0.25, 'rgba(255,170,90,0.8)');
  glow.addColorStop(0.6, 'rgba(255,90,40,0.25)');
  glow.addColorStop(1, 'rgba(255,60,20,0)');
  context.fillStyle = glow;
  context.fillRect(-32, -32, 64, 64);
  return new THREE.CanvasTexture(canvas);
}

const smooth = (from: number, to: number, x: number) => {
  const t = Math.min(1, Math.max(0, (x - from) / (to - from)));
  return t * t * (3 - 2 * t);
};

/** Builds the scene on `canvas`; `onReady` is called once the first frame is drawn. Throws without WebGL. */
export function createAstronautScene(
  canvas: HTMLCanvasElement,
  onReady: () => void,
): AstronautScene {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
  renderer.setPixelRatio(Math.min(1.75, window.devicePixelRatio || 1));
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 0.95;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFShadowMap;

  const scene = new THREE.Scene();
  const pmrem = new THREE.PMREMGenerator(renderer);
  const room = new RoomEnvironment();
  const environment = pmrem.fromScene(room, 0.04);
  room.dispose();
  scene.environment = environment.texture;
  scene.environmentIntensity = 0.28;

  const camera = new THREE.PerspectiveCamera(30, 1, 0.1, 400);
  camera.position.set(0, 0, 10.5);

  // The sun, warm, from the upper right (as the CSS sun); a cool rim from behind; the Earth's blue from below.
  const sun = new THREE.DirectionalLight(0xfff0d8, 2.9);
  sun.position.set(5, 4, 2.5);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  sun.shadow.camera.left = -1.6;
  sun.shadow.camera.right = 1.6;
  sun.shadow.camera.top = 1.6;
  sun.shadow.camera.bottom = -1.6;
  sun.shadow.bias = -0.0004;
  sun.shadow.radius = 4;
  const rim = new THREE.DirectionalLight(0x9cc2ff, 3.4);
  rim.position.set(-3, 2, -4);
  const earthshine = new THREE.HemisphereLight(0x0b1426, 0x3a7fd6, 1.2);
  scene.add(sun, rim, earthshine);

  // ---------- Materials ----------
  const bump = weave();
  const fabric = new THREE.MeshPhysicalMaterial({
    color: 0xeceef1,
    roughness: 0.72,
    sheen: 1,
    sheenRoughness: 0.55,
    sheenColor: new THREE.Color(0xffffff),
    bumpMap: bump,
    bumpScale: 0.45,
  });
  const packFabric = fabric.clone();
  packFabric.color.set(0xdde0e6);
  const shell = new THREE.MeshPhysicalMaterial({
    color: 0xf3f4f6,
    roughness: 0.3,
    clearcoat: 0.8,
    clearcoatRoughness: 0.18,
  });
  const metal = new THREE.MeshStandardMaterial({ color: 0xa9b1bd, metalness: 0.9, roughness: 0.3 });
  const dark = new THREE.MeshStandardMaterial({ color: 0x252a33, roughness: 0.55 });
  const visor = new THREE.MeshPhysicalMaterial({
    color: 0x9a6f26,
    metalness: 1,
    roughness: 0.05,
    clearcoat: 1,
    clearcoatRoughness: 0.02,
    envMapIntensity: 2.2,
  });
  const lamp = new THREE.MeshStandardMaterial({
    color: 0xfff6dc,
    emissive: 0xfff1c8,
    emissiveIntensity: 2,
  });
  const green = new THREE.MeshStandardMaterial({
    color: 0x5fd18a,
    emissive: 0x3fbf6a,
    emissiveIntensity: 1.6,
  });
  const amber = new THREE.MeshStandardMaterial({
    color: 0xffb35c,
    emissive: 0xe08a2a,
    emissiveIntensity: 1.4,
  });
  const heated = [fabric, packFabric, shell];

  const part = (
    geometry: THREE.BufferGeometry,
    material: THREE.Material,
    parent: THREE.Object3D,
    at: Vec = [0, 0, 0],
    turn: Vec = [0, 0, 0],
    scale: Vec = [1, 1, 1],
  ) => {
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(...at);
    mesh.rotation.set(...turn);
    mesh.scale.set(...scale);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    parent.add(mesh);
    return mesh;
  };
  const group = (parent: THREE.Object3D, at: Vec = [0, 0, 0]) => {
    const made = new THREE.Group();
    made.position.set(...at);
    parent.add(made);
    return made;
  };
  const capsule = (radius: number, length: number) =>
    new THREE.CapsuleGeometry(radius, length, 8, 24);
  const ring = (radius: number, tube: number) => new THREE.TorusGeometry(radius, tube, 12, 48);
  const flat = Math.PI / 2;

  // ---------- The body ----------
  const root = group(scene, [0, 0.5, 0]);
  const body = group(root);

  part(capsule(0.27, 0.3), fabric, body, [0, 0.04, 0], [0, 0, 0], [1.22, 1, 0.84]);
  part(
    new THREE.SphereGeometry(0.3, 40, 24),
    fabric,
    body,
    [0, 0.22, 0],
    [0, 0, 0],
    [1.28, 0.72, 0.88],
  );
  part(
    new THREE.SphereGeometry(0.27, 40, 24),
    fabric,
    body,
    [0, -0.34, 0],
    [0, 0, 0],
    [1.12, 0.66, 0.86],
  );
  part(ring(0.3, 0.024), metal, body, [0, -0.2, 0], [flat, 0, 0], [1.1, 0.86, 1]);
  part(ring(0.18, 0.034), metal, body, [0, 0.43, 0], [flat, 0, 0]);

  // The life-support pack and the chest unit.
  part(new RoundedBoxGeometry(0.68, 0.8, 0.3, 4, 0.08), packFabric, body, [0, 0.06, -0.33]);
  part(new RoundedBoxGeometry(0.6, 0.12, 0.26, 3, 0.04), shell, body, [0, 0.5, -0.32]);
  part(
    new RoundedBoxGeometry(0.33, 0.19, 0.1, 3, 0.03),
    shell,
    body,
    [0, 0.07, 0.27],
    [-0.12, 0, 0],
  );
  part(new THREE.BoxGeometry(0.13, 0.055, 0.01), dark, body, [-0.06, 0.11, 0.326], [-0.12, 0, 0]);
  part(new THREE.SphereGeometry(0.012, 12, 8), green, body, [-0.09, 0.11, 0.333]);
  part(new THREE.SphereGeometry(0.012, 12, 8), amber, body, [0.09, 0.03, 0.33]);
  for (const x of [-0.09, -0.04]) {
    part(
      new THREE.CylinderGeometry(0.018, 0.018, 0.03, 16),
      metal,
      body,
      [x, 0.03, 0.33],
      [flat, 0, 0],
    );
  }
  part(
    new THREE.CylinderGeometry(0.03, 0.03, 0.035, 20),
    metal,
    body,
    [0.07, 0.11, 0.33],
    [flat, 0, 0],
  );

  // The helmet: the shell, the lamps, the gold visor.
  const head = group(body, [0, 0.67, 0.01]);
  part(new THREE.SphereGeometry(0.265, 48, 32), shell, head);
  part(
    new THREE.SphereGeometry(0.271, 48, 32, flat - 0.98, 1.96, 0.5, 1.32),
    visor,
    head,
    [0, -0.005, 0.004],
  );
  for (const side of [-1, 1]) {
    part(
      new RoundedBoxGeometry(0.07, 0.1, 0.09, 2, 0.02),
      shell,
      head,
      [side * 0.235, 0.1, 0.06],
      [0, 0, side * -0.25],
    );
  }
  part(
    new THREE.CylinderGeometry(0.025, 0.025, 0.012, 20),
    lamp,
    head,
    [0.245, 0.11, 0.11],
    [flat, 0, 0],
  );

  // ---------- The limbs: a pivot at the shoulder or the hip, another at the elbow or the knee ----------
  const limb = (
    at: Vec,
    upper: { radius: number; length: number },
    lower: { radius: number; length: number },
  ) => {
    const top = group(body, at);
    part(capsule(upper.radius, upper.length), fabric, top, [0, -upper.length / 2 - 0.02, 0]);
    const joint = group(top, [0, -upper.length - 0.04, 0]);
    part(ring(upper.radius * 0.92, 0.02), metal, joint, [0, 0.02, 0], [flat, 0, 0]);
    part(capsule(lower.radius, lower.length), fabric, joint, [0, -lower.length / 2 - 0.02, 0]);
    const end = group(joint, [0, -lower.length - 0.06, 0]);
    return { top, joint, end };
  };

  const arms = [-1, 1].map((side) => {
    const arm = limb(
      [side * 0.42, 0.25, 0],
      { radius: 0.1, length: 0.25 },
      { radius: 0.088, length: 0.22 },
    );
    part(ring(0.088, 0.02), metal, arm.end, [0, 0.03, 0], [flat, 0, 0]);
    part(
      new THREE.SphereGeometry(0.09, 24, 16),
      fabric,
      arm.end,
      [0, -0.06, 0],
      [0, 0, 0],
      [0.95, 1.2, 0.75],
    );
    part(capsule(0.03, 0.05), fabric, arm.end, [side * -0.07, -0.04, 0.03], [0.4, 0, side * 0.7]);
    return { side, ...arm };
  });
  const legs = [-1, 1].map((side) => {
    const leg = limb(
      [side * 0.16, -0.46, 0],
      { radius: 0.13, length: 0.33 },
      { radius: 0.112, length: 0.3 },
    );
    part(new RoundedBoxGeometry(0.2, 0.16, 0.32, 3, 0.06), shell, leg.end, [0, -0.02, 0.05]);
    part(new RoundedBoxGeometry(0.21, 0.04, 0.33, 2, 0.015), dark, leg.end, [0, -0.1, 0.05]);
    return { side, ...leg };
  });

  // The glow of the air, above the body as it falls.
  const flameMap = flame();
  const heat = new THREE.Sprite(
    new THREE.SpriteMaterial({
      map: flameMap,
      color: 0xffc080,
      blending: THREE.AdditiveBlending,
      transparent: true,
      depthWrite: false,
      opacity: 0,
    }),
  );
  scene.add(heat);

  // ---------- Size, pointer, frames ----------
  const resize = () => {
    const width = canvas.clientWidth || window.innerWidth;
    const height = canvas.clientHeight || window.innerHeight;
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
  };
  resize();
  window.addEventListener('resize', resize);

  const pointer = { x: 0, y: 0, seenX: 0, seenY: 0 };
  const onPointer = (event: PointerEvent) => {
    pointer.x = (event.clientX / window.innerWidth) * 2 - 1;
    pointer.y = (event.clientY / window.innerHeight) * 2 - 1;
  };
  window.addEventListener('pointermove', onPointer);

  let fallAt: number | null = null;
  let frame = 0;
  let ready = false;
  const started = performance.now();

  const pose = (t: number, spread: number) => {
    // The weightless posture, breathing a little; `spread` opens the limbs as it falls.
    for (const { side, top, joint } of arms) {
      top.rotation.set(
        -0.55 - 0.08 * Math.sin(t * 0.7 + side) - spread * 1.6,
        0,
        side * (0.5 + 0.06 * Math.sin(t * 0.55 + side * 2) + spread * 0.9),
      );
      joint.rotation.x = -0.95 + 0.1 * Math.sin(t * 0.8 + side * 1.3) + spread * 0.6;
    }
    for (const { side, top, joint } of legs) {
      top.rotation.set(
        -0.55 + 0.06 * Math.sin(t * 0.5 + side * 2.2) + spread * 0.4,
        0,
        side * (0.1 + spread * 0.35),
      );
      joint.rotation.x = 1.25 + 0.08 * Math.sin(t * 0.65 + side) - spread * 0.7;
    }
  };

  const render = () => {
    frame = requestAnimationFrame(render);
    const now = performance.now();
    const t = (now - started) / 1000;
    pointer.seenX += (pointer.x - pointer.seenX) * 0.04;
    pointer.seenY += (pointer.y - pointer.seenY) * 0.04;

    if (fallAt === null) {
      pose(t, 0);
      root.position.set(0.05 * Math.sin(t * 0.37), 0.5 + 0.07 * Math.sin(t * 0.52), 0);
      body.rotation.set(
        0.12 + 0.1 * Math.sin(t * 0.21) + pointer.seenY * 0.2,
        -0.6 + 0.3 * Math.sin(t * 0.16) + pointer.seenX * 0.35,
        0.1 * Math.sin(t * 0.13 + 1),
      );
      head.rotation.set(pointer.seenY * 0.15, pointer.seenX * 0.3, 0);
    } else {
      // Lets go (a small push back), then gravity: faster and faster, away and down towards the Earth.
      const p = Math.min(1, (now - fallAt) / fallMs);
      const lift = smooth(0, 0.14, p) * 0.18;
      const q = Math.max(0, (p - 0.14) / 0.86);
      const drop = q * q;
      pose(t, smooth(0, 0.5, q));
      root.position.set(drop * 3, 0.5 + lift - drop * 40, -drop * 150);
      body.rotation.x += 0.004 + q * 0.11;
      body.rotation.z += 0.002 + q * 0.06;
      body.rotation.y += q * 0.03;
      const glow = smooth(0.45, 1, q);
      for (const material of heated)
        material.emissive.setRGB(1, 0.42, 0.14).multiplyScalar(glow * 0.9);
      heat.material.opacity = glow;
      // The flame's foot (its brightest part) at the body, the rest trailing above it.
      heat.scale.set(1.2 + 7 * glow, 3 + 22 * glow, 1);
      heat.position.set(root.position.x, root.position.y + heat.scale.y * 0.3, root.position.z - 1);
      if (p >= 1) cancelAnimationFrame(frame);
    }

    renderer.render(scene, camera);
    if (!ready) {
      ready = true;
      onReady();
    }
  };
  frame = requestAnimationFrame(render);

  return {
    fall() {
      if (fallAt === null) fallAt = performance.now();
    },
    dispose() {
      cancelAnimationFrame(frame);
      window.removeEventListener('resize', resize);
      window.removeEventListener('pointermove', onPointer);
      scene.traverse((object) => {
        if (object instanceof THREE.Mesh || object instanceof THREE.Sprite) {
          object.geometry.dispose();
          const materials = Array.isArray(object.material) ? object.material : [object.material];
          for (const material of materials) material.dispose();
        }
      });
      bump?.dispose();
      flameMap?.dispose();
      environment.dispose();
      pmrem.dispose();
      renderer.dispose();
    },
  };
}
