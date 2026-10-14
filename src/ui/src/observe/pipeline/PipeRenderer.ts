/**
 * Draws the pipes of the pipeline diagram on a 2D canvas, behind the stages' cards: each pipe a faint tube, lit while
 * its stage works, with dots of light running along it as records pass (a red one now and then where records fail),
 * and the backlog waiting in Kafka as a slow crowd in its channel.
 *
 * The tubes are drawn once per layout into a second canvas; each frame only moves the dots. The dots follow the
 * playback: still when it is paused, quicker when a replay runs fast. With reduced motion, the dots stand still.
 */
import { sceneColors } from '../theme/palette';
import {
  type Box,
  backlogDots,
  centerX,
  centerY,
  dotSpeed,
  dotsPerSecond,
  type Point,
  pointAt,
  type Track,
} from './tracks';

/** One pipe and what flows in it. */
export interface Pipe {
  id: string;
  track: Track;
  /** Records a second through it; null or 0 when none. */
  rate: number | null;
  /** Whether its stage is at work at the moment shown. */
  live: boolean;
  /** The share of records that fail along it (shown as red dots that fade before the end). */
  failing: number;
  /** A pipe that carries something other than records (the engine's conclusions): drawn dashed. */
  dashed?: boolean;
}

/** The queue's channel and what waits for each reader. */
export interface Queue {
  channel: Box;
  left: { x: number; lag: number | null };
  right: { x: number; lag: number | null };
}

interface Dot {
  s: number;
  fail: boolean;
  /** Where a failing dot fades out, as a share of the pipe. */
  end: number;
}

interface Flow {
  pipe: Pipe;
  dots: Dot[];
  /** The dots a second it carries now, easing towards the pipe's rate. */
  shown: number;
  owed: number;
}

/** The light of a dot, drawn once. */
function sprite(color: string, size: number): HTMLCanvasElement | null {
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d');
  if (!context) return null;
  const middle = size / 2;
  const glow = context.createRadialGradient(middle, middle, 0, middle, middle, middle);
  glow.addColorStop(0, color);
  glow.addColorStop(0.18, color);
  glow.addColorStop(0.42, `${color}55`);
  glow.addColorStop(1, `${color}00`);
  context.fillStyle = glow;
  context.fillRect(0, 0, size, size);
  return canvas;
}

export class PipeRenderer {
  private readonly canvas: HTMLCanvasElement;
  private readonly context: CanvasRenderingContext2D | null;
  private readonly tubes: HTMLCanvasElement;
  private readonly light = sprite(sceneColors.written, 24);
  private readonly red = sprite(sceneColors.rejected, 24);
  private width = 0;
  private height = 0;
  private ratio = 1;
  private flows = new Map<string, Flow>();
  private queue: Queue | null = null;
  private crowd = { left: 0, right: 0 };
  private glowAt: { x: number; y: number } | null = null;
  private frame = 0;
  private last = 0;
  private time = 0;
  private running = false;
  private motion: () => number = () => 1;
  private readonly still: boolean;

  constructor(canvas: HTMLCanvasElement, reducedMotion: boolean) {
    this.canvas = canvas;
    this.context = canvas.getContext('2d');
    this.tubes = document.createElement('canvas');
    this.still = reducedMotion;
  }

  /** How fast the scene runs now (motionOf in tracks.ts), read every frame. */
  setMotion(motion: () => number) {
    this.motion = motion;
  }

  /** The canvas's size and the pipes; the dots already on a pipe that keeps its id stay where they are. */
  setPipes(
    width: number,
    height: number,
    pipes: Pipe[],
    queue: Queue | null,
    glowAt: Point | null,
  ) {
    const ratio = Math.min(2, window.devicePixelRatio || 1);
    if (width !== this.width || height !== this.height || ratio !== this.ratio) {
      this.width = width;
      this.height = height;
      this.ratio = ratio;
      this.canvas.width = Math.round(width * ratio);
      this.canvas.height = Math.round(height * ratio);
      this.tubes.width = this.canvas.width;
      this.tubes.height = this.canvas.height;
    }
    const flows = new Map<string, Flow>();
    for (const pipe of pipes) {
      const before = this.flows.get(pipe.id);
      flows.set(pipe.id, {
        pipe,
        dots:
          before && !this.still
            ? before.dots.filter((dot) => dot.s <= pipe.track.total)
            : this.seed(pipe),
        shown: before?.shown ?? (pipe.live ? dotsPerSecond(pipe.rate) : 0),
        owed: before?.owed ?? 0,
      });
    }
    this.flows = flows;
    this.queue = queue;
    this.glowAt = glowAt;
    if (queue && (this.still || (this.crowd.left === 0 && this.crowd.right === 0))) {
      this.crowd = { left: backlogDots(queue.left.lag), right: backlogDots(queue.right.lag) };
    }
    this.drawTubes();
    this.draw();
  }

  /** Starts or stops the frames (the section on or off screen). */
  setRunning(running: boolean) {
    if (running === this.running) return;
    this.running = running;
    if (running && !this.still) {
      this.last = performance.now();
      this.frame = requestAnimationFrame(this.step);
    } else {
      cancelAnimationFrame(this.frame);
      this.draw();
    }
  }

  dispose() {
    this.running = false;
    cancelAnimationFrame(this.frame);
  }

  /** A pipe that opens already carrying its dots, evenly spread (and the only dots with reduced motion). */
  private seed(pipe: Pipe): Dot[] {
    const perSecond = pipe.live ? dotsPerSecond(pipe.rate) : 0;
    if (perSecond === 0 || pipe.track.total === 0) return [];
    const gap = dotSpeed(pipe.rate) / perSecond;
    const dots: Dot[] = [];
    for (let s = gap * 0.5; s < pipe.track.total; s += gap) {
      dots.push({ s, fail: false, end: 1 });
    }
    return dots;
  }

  private readonly step = (now: number) => {
    if (!this.running) return;
    this.frame = requestAnimationFrame(this.step);
    const seconds = Math.min(0.05, (now - this.last) / 1000);
    this.last = now;
    const motion = this.motion();
    this.time += seconds * motion;
    for (const flow of this.flows.values()) {
      const { pipe } = flow;
      const target = pipe.live ? dotsPerSecond(pipe.rate) : 0;
      flow.shown += (target - flow.shown) * Math.min(1, seconds * 1.6);
      if (motion === 0) continue;
      const speed = dotSpeed(pipe.rate) * motion;
      for (const dot of flow.dots) dot.s += speed * seconds;
      flow.dots = flow.dots.filter((dot) => dot.s < pipe.track.total * dot.end);
      flow.owed += flow.shown * motion * seconds;
      while (flow.owed >= 1) {
        flow.owed -= 1;
        const fail = pipe.failing > 0 && Math.random() < pipe.failing;
        flow.dots.push({ s: Math.random() * 4, fail, end: fail ? 0.35 + Math.random() * 0.45 : 1 });
      }
    }
    if (this.queue) {
      const ease = Math.min(1, seconds * 0.8);
      this.crowd.left += (backlogDots(this.queue.left.lag) - this.crowd.left) * ease;
      this.crowd.right += (backlogDots(this.queue.right.lag) - this.crowd.right) * ease;
    }
    this.draw();
  };

  /** The tubes, the glow where the stem leaves the core, and the queue's channel: once per layout. */
  private drawTubes() {
    const context = this.tubes.getContext('2d');
    if (!context) return;
    context.setTransform(this.ratio, 0, 0, this.ratio, 0, 0);
    context.clearRect(0, 0, this.width, this.height);
    if (this.glowAt) {
      const { x, y } = this.glowAt;
      const glow = context.createRadialGradient(x, y, 0, x, y, 220);
      glow.addColorStop(0, 'rgb(255 245 226 / 0.11)');
      glow.addColorStop(1, 'rgb(255 245 226 / 0)');
      context.fillStyle = glow;
      context.fillRect(x - 220, y - 220, 440, 440);
    }
    context.lineCap = 'round';
    context.lineJoin = 'round';
    for (const { pipe } of this.flows.values()) {
      const { points } = pipe.track;
      if (points.length < 2) continue;
      const trace = () => {
        context.beginPath();
        context.moveTo((points[0] as Point).x, (points[0] as Point).y);
        for (const point of points.slice(1)) context.lineTo(point.x, point.y);
      };
      context.setLineDash([]);
      // The tube's wall: wide and faint, a little wider for a busier pipe.
      const busy = pipe.live && pipe.rate ? Math.min(1, Math.log10(1 + pipe.rate) / 3) : 0;
      trace();
      context.strokeStyle = pipe.live ? 'rgb(244 240 231 / 0.07)' : 'rgb(244 240 231 / 0.035)';
      context.lineWidth = 8 + 5 * busy;
      context.stroke();
      // Its core: a hairline, lit while the stage works.
      if (pipe.live && busy > 0) {
        context.save();
        context.shadowColor = 'rgb(255 245 226 / 0.5)';
        context.shadowBlur = 10;
        trace();
        context.strokeStyle = `rgb(255 245 226 / ${0.16 + 0.22 * busy})`;
        context.lineWidth = 1.2;
        context.stroke();
        context.restore();
      } else {
        if (pipe.dashed) context.setLineDash([3, 5]);
        trace();
        context.strokeStyle = pipe.live ? 'rgb(244 240 231 / 0.24)' : 'rgb(244 240 231 / 0.12)';
        context.lineWidth = 1;
        context.stroke();
      }
    }
    context.setLineDash([]);
  }

  private draw() {
    const context = this.context;
    if (!context || this.width === 0) return;
    context.setTransform(1, 0, 0, 1, 0, 0);
    context.clearRect(0, 0, this.canvas.width, this.canvas.height);
    context.drawImage(this.tubes, 0, 0);
    context.setTransform(this.ratio, 0, 0, this.ratio, 0, 0);
    this.drawQueue(context);
    for (const { pipe, dots } of this.flows.values()) {
      const { total } = pipe.track;
      for (const dot of dots) {
        const at = pointAt(pipe.track, dot.s);
        const share = total > 0 ? dot.s / total : 0;
        // Each dot fades in as it leaves and out as it arrives; a failing one fades where it falls out.
        const fadeIn = Math.min(1, share * 8);
        const fadeOut = Math.min(1, (dot.end - share) * (dot.fail ? 5 : 10));
        context.globalAlpha = Math.max(0, Math.min(fadeIn, fadeOut)) * (pipe.dashed ? 0.7 : 1);
        const image = dot.fail ? this.red : this.light;
        if (image) context.drawImage(image, at.x - 7, at.y - 7, 14, 14);
      }
    }
    context.globalAlpha = 1;
  }

  /** The backlog: dots drifting slowly in the queue's channel, each half towards the reader it waits for. */
  private drawQueue(context: CanvasRenderingContext2D) {
    const queue = this.queue;
    const image = this.light;
    if (!queue || !image) return;
    const middle = centerX(queue.channel);
    const level = centerY(queue.channel);
    const half = queue.channel.height / 2 - 2;
    const sides = [
      { count: this.crowd.left, to: queue.left.x },
      { count: this.crowd.right, to: queue.right.x },
    ];
    sides.forEach(({ count, to }, side) => {
      const whole = Math.floor(count);
      for (let i = 0; i < Math.ceil(count); i += 1) {
        const seed = (i + 1) * (side === 0 ? 12.9898 : 78.233);
        const hash = (n: number) => {
          const value = Math.sin(seed * n) * 43758.5453;
          return value - Math.floor(value);
        };
        // A place along the channel from the middle towards the reader, drifting back and forth.
        const along = (0.08 + 0.92 * hash(1)) * (to - middle);
        const sway = Math.sin(this.time * (0.25 + 0.3 * hash(2)) + hash(3) * 6.28) * 7;
        const x = middle + along + sway;
        const y = level + (hash(4) - 0.5) * 2 * half * 0.8;
        context.globalAlpha = (i < whole ? 1 : count - whole) * (0.22 + 0.3 * hash(5));
        context.drawImage(image, x - 5, y - 5, 10, 10);
      }
    });
    context.globalAlpha = 1;
  }
}
