/**
 * The shaders of the flow scene. Everything that moves (arcs, their heads, sparks, ripples) is animated on the GPU
 * from a start time and a duration written once when it is launched: the CPU never touches a vertex per frame.
 *
 * Light is added, never painted over (additive blending), the way light sums in a dark room; the bloom pass then
 * lets the brightest parts glow. Colours arrive in linear space (THREE.Color converts the palette's sRGB values).
 */
import * as THREE from 'three';

/** Uniforms shared by every animated material, updated once per frame. */
export interface SharedUniforms {
  /** Seconds since the engine started. */
  uTime: { value: number };
  /** Pixels per scene unit at distance 1: turns sizes in scene units into point sizes in pixels. */
  uPointScale: { value: number };
  /** 0 to 1 while the scene fades in. */
  uIntro: { value: number };
}

/** Creates the shared uniforms. */
export function sharedUniforms(): SharedUniforms {
  return { uTime: { value: 0 }, uPointScale: { value: 400 }, uIntro: { value: 0 } };
}

const additive = {
  transparent: true,
  depthWrite: false,
  blending: THREE.AdditiveBlending,
} as const;

/** Engraved lines on the ground: each vertex has its own colour and strength; they fade towards the edge. */
export function groundMaterial(shared: SharedUniforms): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    ...additive,
    uniforms: { ...shared, uFadeStart: { value: 9.5 }, uFadeEnd: { value: 15.5 } },
    vertexShader: /* glsl */ `
      attribute float aAlpha;
      attribute vec3 aColor;
      varying float vAlpha;
      varying vec3 vColor;
      varying float vRadius;
      void main() {
        vAlpha = aAlpha;
        vColor = aColor;
        vRadius = length(position.xz);
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      uniform float uIntro;
      uniform float uFadeStart;
      uniform float uFadeEnd;
      varying float vAlpha;
      varying vec3 vColor;
      varying float vRadius;
      void main() {
        float fade = 1.0 - smoothstep(uFadeStart, uFadeEnd, vRadius);
        // The engraving draws itself outwards from the core as the scene opens.
        float reveal = smoothstep(vRadius - 2.0, vRadius, uIntro * 18.0);
        gl_FragColor = vec4(vColor, vAlpha * fade * reveal);
      }`,
  });
}

/**
 * The trails of the arcs. Each arc is a run of line segments along a curve from a source to the core; every vertex
 * knows its place on the curve (aT, 0 to 1), when its arc left (aStart) and how long the flight takes (aDuration).
 * A bright comet trail follows the head; behind it a faint track stays a moment, then fades.
 */
export function arcMaterial(shared: SharedUniforms): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    ...additive,
    uniforms: { ...shared },
    vertexShader: /* glsl */ `
      attribute float aT;
      attribute float aStart;
      attribute float aDuration;
      attribute float aStrength;
      attribute vec3 aColor;
      varying float vT;
      varying float vStart;
      varying float vDuration;
      varying float vStrength;
      varying vec3 vColor;
      void main() {
        vT = aT;
        vStart = aStart;
        vDuration = aDuration;
        vStrength = aStrength;
        vColor = aColor;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      uniform float uTime;
      varying float vT;
      varying float vStart;
      varying float vDuration;
      varying float vStrength;
      varying vec3 vColor;
      void main() {
        float age = uTime - vStart;
        float head = age / vDuration;
        float behind = head - vT;
        if (age < 0.0 || behind < 0.0) discard;
        float trail = exp(-behind * 6.5);
        float track = 0.16 * (1.0 - smoothstep(0.0, 2.2, age - vDuration));
        float alpha = max(trail, track) * vStrength;
        if (alpha < 0.002) discard;
        gl_FragColor = vec4(vColor * (1.0 + trail * 0.6), alpha);
      }`,
  });
}

/** The glow of a point: a soft round light with a hot centre. */
const glowFragment = /* glsl */ `
  varying vec3 vColor;
  varying float vAlpha;
  void main() {
    float d = length(gl_PointCoord - 0.5) * 2.0;
    if (d > 1.0) discard;
    float halo = exp(-d * d * 5.0);
    float centre = smoothstep(0.32, 0.0, d);
    gl_FragColor = vec4(vColor * (1.0 + centre * 0.8), (halo * 0.8 + centre) * vAlpha);
  }`;

/**
 * The heads of the arcs: points that ride the curve from the source (aP0) over a control point (aP1) to the core
 * (aP2), positioned on the GPU from the time alone.
 */
export function headMaterial(shared: SharedUniforms): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    ...additive,
    uniforms: { ...shared },
    vertexShader: /* glsl */ `
      uniform float uTime;
      uniform float uPointScale;
      attribute vec3 aP0;
      attribute vec3 aP1;
      attribute vec3 aP2;
      attribute float aStart;
      attribute float aDuration;
      attribute float aSize;
      attribute vec3 aColor;
      varying vec3 vColor;
      varying float vAlpha;
      void main() {
        float t = (uTime - aStart) / aDuration;
        vColor = aColor;
        if (t < 0.0 || t > 1.0) {
          vAlpha = 0.0;
          gl_PointSize = 0.0;
          gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
          return;
        }
        float u = 1.0 - t;
        vec3 p = u * u * aP0 + 2.0 * u * t * aP1 + t * t * aP2;
        vec4 mv = modelViewMatrix * vec4(p, 1.0);
        vAlpha = smoothstep(0.0, 0.06, t) * (1.0 - smoothstep(0.93, 1.0, t));
        gl_PointSize = aSize * uPointScale / -mv.z;
        gl_Position = projectionMatrix * mv;
      }`,
    fragmentShader: glowFragment,
  });
}

/**
 * The sparks of refused records: thrown from the core's surface back the way they came, they arc and fall under a
 * little gravity while they fade.
 */
export function sparkMaterial(shared: SharedUniforms, color: THREE.Color): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    ...additive,
    uniforms: { ...shared, uColor: { value: color } },
    vertexShader: /* glsl */ `
      uniform float uTime;
      uniform float uPointScale;
      uniform vec3 uColor;
      attribute vec3 aOrigin;
      attribute vec3 aVelocity;
      attribute float aStart;
      attribute float aLife;
      attribute float aSize;
      varying vec3 vColor;
      varying float vAlpha;
      void main() {
        float age = uTime - aStart;
        vColor = uColor;
        if (age < 0.0 || age > aLife) {
          vAlpha = 0.0;
          gl_PointSize = 0.0;
          gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
          return;
        }
        vec3 p = aOrigin + aVelocity * age + vec3(0.0, -1.6, 0.0) * age * age;
        vec4 mv = modelViewMatrix * vec4(p, 1.0);
        float life = age / aLife;
        vAlpha = pow(1.0 - life, 1.6);
        gl_PointSize = aSize * (1.0 - life * 0.5) * uPointScale / -mv.z;
        gl_Position = projectionMatrix * mv;
      }`,
    fragmentShader: glowFragment,
  });
}

/** The source lights: each a glowing point whose colour, size and brightness come from its attributes. */
export function orbMaterial(shared: SharedUniforms): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    ...additive,
    uniforms: { ...shared },
    vertexShader: /* glsl */ `
      uniform float uPointScale;
      uniform float uIntro;
      attribute float aSize;
      attribute float aBrightness;
      attribute vec3 aColor;
      varying vec3 vColor;
      varying float vAlpha;
      void main() {
        vec4 mv = modelViewMatrix * vec4(position, 1.0);
        vColor = aColor;
        vAlpha = aBrightness * smoothstep(0.35, 0.9, uIntro);
        gl_PointSize = aSize * uPointScale / -mv.z;
        gl_Position = projectionMatrix * mv;
      }`,
    fragmentShader: glowFragment,
  });
}

/** The core: a sphere of fine points, brighter at its rim, slowly turning and breathing. */
export function coreMaterial(shared: SharedUniforms, color: THREE.Color): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    ...additive,
    uniforms: { ...shared, uColor: { value: color }, uPulse: { value: 0 } },
    vertexShader: /* glsl */ `
      uniform float uTime;
      uniform float uPointScale;
      uniform float uIntro;
      uniform float uPulse;
      attribute float aSeed;
      varying float vAlpha;
      void main() {
        float breath = 1.0 + 0.025 * sin(uTime * 1.3 + aSeed * 6.2831) + uPulse * 0.06;
        vec3 p = position * breath * mix(0.2, 1.0, smoothstep(0.0, 0.7, uIntro));
        vec4 mv = modelViewMatrix * vec4(p, 1.0);
        vec3 n = normalize(normalMatrix * normalize(position));
        float rim = 1.0 - abs(dot(n, normalize(-mv.xyz)));
        float twinkle = 0.75 + 0.25 * sin(uTime * 2.0 + aSeed * 40.0);
        vAlpha = (0.07 + rim * rim * 0.55) * twinkle * (0.85 + uPulse * 0.7) * uIntro;
        gl_PointSize = 0.07 * uPointScale / -mv.z;
        gl_Position = projectionMatrix * mv;
      }`,
    fragmentShader: /* glsl */ `
      uniform vec3 uColor;
      varying float vAlpha;
      void main() {
        float d = length(gl_PointCoord - 0.5) * 2.0;
        if (d > 1.0) discard;
        gl_FragColor = vec4(uColor, (1.0 - d * d) * vAlpha);
      }`,
  });
}

/** A soft round glow on a quad that always faces the camera (the core's light). */
export function glowMaterial(color: THREE.Color, opacity: number): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    ...additive,
    uniforms: { uColor: { value: color }, uOpacity: { value: opacity } },
    vertexShader: /* glsl */ `
      varying vec2 vUv;
      void main() {
        vUv = uv;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      uniform vec3 uColor;
      uniform float uOpacity;
      varying vec2 vUv;
      void main() {
        float d = length(vUv - 0.5) * 2.0;
        float glow = exp(-d * d * 4.5) - exp(-4.5);
        gl_FragColor = vec4(uColor, max(glow, 0.0) * uOpacity);
      }`,
  });
}

/** A ripple: a thin ring on the ground spreading out from the core, as a batch is taken in. */
export function rippleMaterial(color: THREE.Color): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    ...additive,
    uniforms: { uColor: { value: color }, uProgress: { value: 1 }, uStrength: { value: 0 } },
    vertexShader: /* glsl */ `
      varying vec2 vUv;
      void main() {
        vUv = uv;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      uniform vec3 uColor;
      uniform float uProgress;
      uniform float uStrength;
      varying vec2 vUv;
      void main() {
        float r = length(vUv - 0.5) * 2.0;
        float radius = mix(0.12, 1.0, uProgress);
        float ring = exp(-pow((r - radius) * 38.0, 2.0));
        float alpha = ring * (1.0 - uProgress) * uStrength;
        if (alpha < 0.003) discard;
        gl_FragColor = vec4(uColor, alpha);
      }`,
  });
}

/** Faint, far points around the scene, twinkling slowly: depth for the eye, nothing more. */
export function starMaterial(shared: SharedUniforms): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    ...additive,
    uniforms: { ...shared },
    vertexShader: /* glsl */ `
      uniform float uTime;
      uniform float uIntro;
      attribute float aSeed;
      varying float vAlpha;
      void main() {
        vec4 mv = modelViewMatrix * vec4(position, 1.0);
        vAlpha = (0.25 + 0.35 * sin(uTime * 0.6 + aSeed * 50.0)) * uIntro;
        gl_PointSize = 1.6;
        gl_Position = projectionMatrix * mv;
      }`,
    fragmentShader: /* glsl */ `
      varying float vAlpha;
      void main() {
        float d = length(gl_PointCoord - 0.5) * 2.0;
        if (d > 1.0) discard;
        gl_FragColor = vec4(vec3(0.95, 0.9, 0.82), (1.0 - d) * vAlpha * 0.5);
      }`,
  });
}
