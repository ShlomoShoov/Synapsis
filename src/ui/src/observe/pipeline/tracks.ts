/**
 * The pipes of the pipeline diagram, as geometry: each pipe is a line of points a record travels along, from one
 * stage's box to the next. Measured from the boxes on the page, so the pipes follow the layout at any width.
 * Also how a rate shows: how many dots a second a pipe carries, and how fast they go.
 */

export interface Point {
  x: number;
  y: number;
}

/** A box on the page, relative to the diagram. */
export interface Box {
  left: number;
  top: number;
  width: number;
  height: number;
}

/** A line of points and the distance along it at each point. */
export interface Track {
  points: Point[];
  /** lengths[i]: the distance from the start to points[i]. */
  lengths: number[];
  total: number;
}

export const centerX = (box: Box) => box.left + box.width / 2;
export const centerY = (box: Box) => box.top + box.height / 2;
export const bottomOf = (box: Box): Point => ({ x: centerX(box), y: box.top + box.height });
export const topOf = (box: Box): Point => ({ x: centerX(box), y: box.top });

/** A track through `points`. */
export function trackOf(points: Point[]): Track {
  const lengths = [0];
  for (let i = 1; i < points.length; i += 1) {
    const a = points[i - 1] as Point;
    const b = points[i] as Point;
    lengths.push((lengths[i - 1] as number) + Math.hypot(b.x - a.x, b.y - a.y));
  }
  return { points, lengths, total: lengths[lengths.length - 1] ?? 0 };
}

/** The point at distance `s` along a track (clamped to its ends). */
export function pointAt(track: Track, s: number): Point {
  const { points, lengths, total } = track;
  if (points.length === 0) return { x: 0, y: 0 };
  if (s <= 0) return points[0] as Point;
  if (s >= total) return points[points.length - 1] as Point;
  let low = 0;
  let high = lengths.length - 1;
  while (high - low > 1) {
    const middle = (low + high) >> 1;
    if ((lengths[middle] as number) <= s) low = middle;
    else high = middle;
  }
  const a = points[low] as Point;
  const b = points[high] as Point;
  const span = (lengths[high] as number) - (lengths[low] as number) || 1;
  const t = (s - (lengths[low] as number)) / span;
  return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t };
}

/** A cubic curve from a to b, as points. */
export function curve(a: Point, c1: Point, c2: Point, b: Point, steps = 28): Point[] {
  const points: Point[] = [];
  for (let i = 0; i <= steps; i += 1) {
    const t = i / steps;
    const u = 1 - t;
    points.push({
      x: u * u * u * a.x + 3 * u * u * t * c1.x + 3 * u * t * t * c2.x + t * t * t * b.x,
      y: u * u * u * a.y + 3 * u * u * t * c1.y + 3 * u * t * t * c2.y + t * t * t * b.y,
    });
  }
  return points;
}

/** Down from a to b: leaves a going straight down and enters b going straight down. */
export function drop(a: Point, b: Point): Point[] {
  const bend = Math.max(12, (b.y - a.y) * 0.5);
  return curve(a, { x: a.x, y: a.y + bend }, { x: b.x, y: b.y - bend }, b);
}

/** Across from a to b: leaves a and enters b level. */
export function across(a: Point, b: Point): Point[] {
  const bend = (b.x - a.x) * 0.5;
  return curve(a, { x: a.x + bend, y: a.y }, { x: b.x - bend, y: b.y }, b, 18);
}

/** Along a level channel from a, then down into b: one rounded corner. */
export function branch(a: Point, b: Point, radius = 18): Point[] {
  const side = Math.sign(b.x - a.x) || 1;
  const r = Math.min(radius, Math.abs(b.x - a.x), Math.max(0, b.y - a.y));
  const corner = { x: b.x - side * r, y: a.y };
  const arc: Point[] = [];
  for (let i = 0; i <= 8; i += 1) {
    const angle = (i / 8) * (Math.PI / 2);
    arc.push({ x: corner.x + side * r * Math.sin(angle), y: a.y + r * (1 - Math.cos(angle)) });
  }
  return [a, ...arc, b];
}

/** Dots a second a pipe carries for a rate of records a second: more records, more dots, on a log scale. */
export function dotsPerSecond(rate: number | null): number {
  if (!rate || rate <= 0) return 0;
  return Math.min(14, 0.9 + 2.3 * Math.log10(1 + rate));
}

/** How fast the dots go, in pixels a second: a busier pipe runs a little faster. */
export function dotSpeed(rate: number | null): number {
  return 64 + 26 * Math.log10(1 + Math.max(0, rate ?? 0));
}

/** How many dots stand for a backlog in the queue: one per tenfold, a few to begin with. */
export function backlogDots(lag: number | null): number {
  if (!lag || lag <= 0) return 0;
  return Math.min(46, Math.round(5 * Math.log10(1 + lag)));
}

/** How fast the scene moves for the playback: still when paused, quicker the faster a replay runs. */
export function motionOf(play: {
  mode: 'live' | 'replay';
  playing: boolean;
  speed: number;
}): number {
  if (play.mode === 'live') return 1;
  if (!play.playing) return 0;
  return 1 + 0.35 * Math.log10(Math.max(1, play.speed));
}
