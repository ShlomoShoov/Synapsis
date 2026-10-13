/**
 * The events as twinkling lights: a MapLibre custom layer that draws every light in one call, on the GPU.
 *
 * Each light is a point sprite: a white-hot core in a soft halo of its category's colour, added to what is under it
 * (additive blending), so lights glow on the night Earth and brighten where they gather. The twinkle, the fade from one
 * answer to the next and LIVE's ageing are all computed in the shader from a few numbers (time, the playhead), so a
 * frame costs the same for ten lights or ten thousand, and nothing is re-sent to the GPU while they animate.
 *
 * MapLibre's own projection code (its shader prelude) places the lights on the globe, so they sit exactly where the
 * basemap is, hide behind the horizon, and follow the change to a flat map when zoomed in.
 */
import type { CustomLayerInterface, CustomRenderMethodInput, Map as MapLibre } from 'maplibre-gl';
import { dayOfMs, type LightSet } from './lights';

/** How LIVE shows time: lights appear at the playhead and fade over the trail. */
export interface LiveTime {
  /** The playhead (ms). */
  head: number;
  /** How long a light stays (ms): it fades from bright to dark over this time. */
  trail: number;
}

/** How long a new set of lights takes to fade in, and the old one to fade out. */
const fadeMs = 420;

/** The twinkle is drawn at about 30 frames a second when nothing else moves. */
const twinkleFrameMs = 33;

const vertexBody = `
in vec2 a_pos;
in vec3 a_color;
in float a_day;
in float a_seed;
in float a_size;
uniform float u_time;
uniform float u_alpha;
uniform float u_px;
uniform float u_zoom;
uniform float u_live;
uniform float u_head;
uniform float u_trail;
uniform int u_hover;
out vec3 v_color;
out float v_alpha;
out float v_flash;
void main() {
  gl_Position = projectTile(a_pos);
  // Each light breathes at its own pace, and now and then sparkles.
  float breathe = 0.74 + 0.26 * sin(u_time * (0.7 + 1.6 * a_seed) + a_seed * 40.0);
  float sparkle = pow(max(0.0, sin(u_time * (0.23 + 0.31 * a_seed) + a_seed * 17.0)), 24.0);
  float alpha = u_alpha * (breathe + 0.6 * sparkle);
  float size = a_size * mix(18.0, 36.0, clamp((u_zoom - 1.0) / 9.0, 0.0, 1.0)) * (1.0 + 0.35 * sparkle);
  float flash = 0.0;
  if (u_live > 0.5) {
    // Age in days since the event; a light is born bright and large at the playhead, then fades over the trail.
    float age = u_head - a_day;
    if (!(age >= 0.0) || age > u_trail) {
      alpha = 0.0;
    } else {
      flash = exp(-age / max(u_trail * 0.035, 0.02));
      alpha *= mix(1.0, 0.1, smoothstep(0.0, 1.0, age / u_trail)) + flash;
      size *= 1.0 + 1.6 * flash;
    }
  }
  if (gl_VertexID == u_hover) {
    size *= 1.8;
    alpha = max(alpha, 1.2);
  }
  gl_PointSize = size * u_px;
  v_color = a_color;
  v_alpha = alpha;
  v_flash = flash;
  if (alpha < 0.002) gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
}`;

const fragmentSource = `#version 300 es
precision mediump float;
in vec3 v_color;
in float v_alpha;
in float v_flash;
out vec4 fragColor;
void main() {
  vec2 c = gl_PointCoord * 2.0 - 1.0;
  float r2 = dot(c, c);
  if (r2 > 1.0) discard;
  float halo = exp(-r2 * 4.5) * (1.0 - r2);
  float core = exp(-r2 * 60.0);
  // A faint four-pointed flare, like a star seen through glass.
  float flare = (exp(-abs(c.x) * 26.0) * exp(-abs(c.y) * 3.2) + exp(-abs(c.y) * 26.0) * exp(-abs(c.x) * 3.2)) * 0.22;
  vec3 color = v_color * (halo * 0.95 + flare) + mix(v_color, vec3(1.0), 0.8) * core * 1.25;
  float intensity = v_alpha * (1.0 + 0.8 * v_flash);
  fragColor = vec4(color * intensity, clamp((halo * 0.6 + core) * v_alpha, 0.0, 1.0));
}`;

interface Program {
  program: WebGLProgram;
  uniforms: Record<string, WebGLUniformLocation | null>;
}

/** The attributes, at fixed locations 0..4, and their sizes. */
const attributeNames = ['a_pos', 'a_color', 'a_day', 'a_seed', 'a_size'];
const attributeSizes = [2, 3, 1, 1, 1];

interface Drawn {
  set: LightSet;
  vao: WebGLVertexArrayObject | null;
  buffers: WebGLBuffer[];
  alpha: number;
  /** 1 while it is the shown set; 0 once a newer set replaces it. */
  target: number;
}

const uniformNames = [
  'u_projection_matrix',
  'u_projection_fallback_matrix',
  'u_projection_tile_mercator_coords',
  'u_projection_clipping_plane',
  'u_projection_transition',
  'u_projection_clip_antimeridian',
  'u_time',
  'u_alpha',
  'u_px',
  'u_zoom',
  'u_live',
  'u_head',
  'u_trail',
  'u_hover',
];

export class LightsLayer implements CustomLayerInterface {
  readonly id = 'event-lights';
  readonly type = 'custom' as const;
  readonly renderingMode = '2d' as const;

  private map: MapLibre | null = null;
  private gl: WebGL2RenderingContext | null = null;
  private programs = new Map<string, Program>();
  private drawn: Drawn[] = [];
  private pending: LightSet | null = null;
  private live: LiveTime | null = null;
  private hover = -1;
  private still = false;
  private readonly started = performance.now();
  private lastFrame = performance.now();
  private timer = 0;

  /** Shows a new set of lights: it fades in while the shown one fades out. */
  setLights(set: LightSet) {
    this.pending = set;
    this.hover = -1;
    this.map?.triggerRepaint();
  }

  /** LIVE: lights appear at the playhead and fade over the trail; null shows every light. */
  setLive(live: LiveTime | null) {
    this.live = live;
    this.map?.triggerRepaint();
  }

  /** Lights one light more (its index in the shown set), or none (-1). */
  setHover(index: number) {
    if (index === this.hover) return;
    this.hover = index;
    this.map?.triggerRepaint();
  }

  /** No twinkle and no fades (the viewer asked for less motion). */
  setStill(still: boolean) {
    this.still = still;
    this.map?.triggerRepaint();
  }

  /** The set of lights now shown. */
  shown(): LightSet | null {
    return this.drawn.at(-1)?.set ?? this.pending;
  }

  onAdd(map: MapLibre, gl: WebGLRenderingContext | WebGL2RenderingContext) {
    this.map = map;
    this.gl = gl as WebGL2RenderingContext;
  }

  onRemove() {
    window.clearTimeout(this.timer);
    const gl = this.gl;
    if (gl) {
      for (const drawn of this.drawn) this.release(gl, drawn);
      for (const { program } of this.programs.values()) gl.deleteProgram(program);
    }
    this.drawn = [];
    this.programs.clear();
    this.map = null;
    this.gl = null;
  }

  render(glContext: WebGLRenderingContext | WebGL2RenderingContext, args: CustomRenderMethodInput) {
    const gl = glContext as WebGL2RenderingContext;
    const map = this.map;
    if (!map) return;
    const now = performance.now();
    const step = this.still ? 1 : Math.min(1, (now - this.lastFrame) / fadeMs);
    this.lastFrame = now;

    if (this.pending) {
      for (const drawn of this.drawn) drawn.target = 0;
      this.drawn.push(this.upload(gl, this.pending));
      this.pending = null;
    }
    for (const drawn of this.drawn) {
      drawn.alpha =
        drawn.target > drawn.alpha
          ? Math.min(drawn.target, drawn.alpha + step)
          : Math.max(drawn.target, drawn.alpha - step);
    }
    const gone = this.drawn.filter((drawn) => drawn.target === 0 && drawn.alpha === 0);
    for (const drawn of gone) this.release(gl, drawn);
    this.drawn = this.drawn.filter((drawn) => !gone.includes(drawn));
    if (this.drawn.length === 0) return;

    const program = this.programFor(gl, args.shaderData);
    if (!program) return;
    const { uniforms: u } = program;
    const projection = args.defaultProjectionData;
    gl.useProgram(program.program);
    gl.uniformMatrix4fv(u.u_projection_matrix!, false, new Float32Array(projection.mainMatrix));
    gl.uniformMatrix4fv(
      u.u_projection_fallback_matrix!,
      false,
      new Float32Array(projection.fallbackMatrix),
    );
    gl.uniform4f(u.u_projection_tile_mercator_coords!, ...projection.tileMercatorCoords);
    gl.uniform4f(u.u_projection_clipping_plane!, ...projection.clippingPlane);
    gl.uniform1f(u.u_projection_transition!, projection.projectionTransition);
    gl.uniform1i(u.u_projection_clip_antimeridian!, projection.clipAntimeridian ? 1 : 0);
    gl.uniform1f(u.u_time!, this.still ? 0 : (now - this.started) / 1000);
    gl.uniform1f(u.u_px!, map.getPixelRatio());
    gl.uniform1f(u.u_zoom!, map.getZoom());
    gl.uniform1f(u.u_live!, this.live ? 1 : 0);
    gl.uniform1f(u.u_head!, this.live ? dayOfMs(this.live.head) : 0);
    gl.uniform1f(u.u_trail!, this.live ? Math.max(this.live.trail / 86_400_000, 1 / 24) : 1);

    gl.enable(gl.BLEND);
    gl.blendFuncSeparate(gl.ONE, gl.ONE, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
    gl.disable(gl.DEPTH_TEST);
    this.drawn.forEach((drawn, index) => {
      const latest = index === this.drawn.length - 1;
      gl.uniform1f(u.u_alpha!, drawn.alpha);
      gl.uniform1i(u.u_hover!, latest ? this.hover : -1);
      gl.bindVertexArray(drawn.vao);
      gl.drawArrays(gl.POINTS, 0, drawn.set.count);
    });
    gl.bindVertexArray(null);

    // Keep drawing while lights fade or twinkle; when still, only on demand.
    const fading = this.drawn.some((drawn) => drawn.alpha !== drawn.target);
    if (fading) {
      map.triggerRepaint();
    } else if (!this.still) {
      window.clearTimeout(this.timer);
      this.timer = window.setTimeout(() => this.map?.triggerRepaint(), twinkleFrameMs);
    }
  }

  private upload(gl: WebGL2RenderingContext, set: LightSet): Drawn {
    const vao = gl.createVertexArray();
    gl.bindVertexArray(vao);
    const buffers: WebGLBuffer[] = [];
    const arrays = [set.positions, set.colors, set.days, set.seeds, set.sizes];
    arrays.forEach((data, location) => {
      const buffer = gl.createBuffer();
      if (!buffer) return;
      buffers.push(buffer);
      gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
      gl.bufferData(gl.ARRAY_BUFFER, data, gl.STATIC_DRAW);
      gl.enableVertexAttribArray(location);
      gl.vertexAttribPointer(location, attributeSizes[location]!, gl.FLOAT, false, 0, 0);
    });
    gl.bindVertexArray(null);
    gl.bindBuffer(gl.ARRAY_BUFFER, null);
    return { set, vao, buffers, alpha: this.still ? 1 : 0, target: 1 };
  }

  private release(gl: WebGL2RenderingContext, drawn: Drawn) {
    gl.deleteVertexArray(drawn.vao);
    for (const buffer of drawn.buffers) gl.deleteBuffer(buffer);
  }

  /** The program for the map's current projection (globe or flat), compiled once per variant. */
  private programFor(
    gl: WebGL2RenderingContext,
    shaderData: CustomRenderMethodInput['shaderData'],
  ): Program | null {
    const cached = this.programs.get(shaderData.variantName);
    if (cached) return cached;
    const vertex = compile(
      gl,
      gl.VERTEX_SHADER,
      `#version 300 es\n${shaderData.vertexShaderPrelude}\n${shaderData.define}\n${vertexBody}`,
    );
    const fragment = compile(gl, gl.FRAGMENT_SHADER, fragmentSource);
    const program = gl.createProgram();
    if (!vertex || !fragment || !program) return null;
    gl.attachShader(program, vertex);
    gl.attachShader(program, fragment);
    // Fixed locations, so one vertex array serves every variant (see upload).
    attributeNames.forEach((name, location) => gl.bindAttribLocation(program, location, name));
    gl.linkProgram(program);
    gl.deleteShader(vertex);
    gl.deleteShader(fragment);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
      console.error('Analyze lights:', gl.getProgramInfoLog(program));
      return null;
    }
    const entry: Program = {
      program,
      uniforms: Object.fromEntries(
        uniformNames.map((name) => [name, gl.getUniformLocation(program, name)]),
      ),
    };
    this.programs.set(shaderData.variantName, entry);
    return entry;
  }
}

function compile(gl: WebGL2RenderingContext, type: number, source: string): WebGLShader | null {
  const shader = gl.createShader(type);
  if (!shader) return null;
  gl.shaderSource(shader, source);
  gl.compileShader(shader);
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    console.error('Analyze lights:', gl.getShaderInfoLog(shader));
    gl.deleteShader(shader);
    return null;
  }
  return shader;
}
