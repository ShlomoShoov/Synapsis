/**
 * The camera of the schema map: where the map is and how large. A point of the map at (x, y) shows on screen at
 * (x * k + tx, y * k + ty). Pure functions, so pan, zoom and fit are the same everywhere and tested.
 */
import type { Box, Point } from './layout';

/** The camera: a scale and a translation. */
export interface Camera {
  k: number;
  tx: number;
  ty: number;
}

/** The size of the frame the map shows in, and the part of it that is free (the inspector covers the right side). */
export interface Frame {
  width: number;
  height: number;
  /** Pixels on the right covered by a panel; the map centres on what is left. */
  coveredRight?: number;
}

/** How far the map zooms out and in. */
export const minScale = 0.18;
export const maxScale = 2.2;
/** Below this scale the cards are too small to read: they show their shape, and labels float above them. */
export const readableScale = 0.58;

/** A scale kept inside the limits. */
export function clampScale(k: number): number {
  return Math.min(maxScale, Math.max(minScale, k));
}

/** The point of the map under a point of the screen. */
export function toMap(camera: Camera, screen: Point): Point {
  return { x: (screen.x - camera.tx) / camera.k, y: (screen.y - camera.ty) / camera.k };
}

/** The point of the screen over a point of the map. */
export function toScreen(camera: Camera, point: Point): Point {
  return { x: point.x * camera.k + camera.tx, y: point.y * camera.k + camera.ty };
}

/** Zooms by `factor` around a point of the screen, which stays where it is. */
export function zoomAt(camera: Camera, factor: number, screen: Point): Camera {
  const k = clampScale(camera.k * factor);
  const anchor = toMap(camera, screen);
  return { k, tx: screen.x - anchor.x * k, ty: screen.y - anchor.y * k };
}

/** Moves the camera by a distance on screen. */
export function panBy(camera: Camera, dx: number, dy: number): Camera {
  return { ...camera, tx: camera.tx + dx, ty: camera.ty + dy };
}

/** The camera that shows the whole box in the free part of the frame, with a margin, no larger than `limit`. */
export function fitBox(box: Box, frame: Frame, margin = 32, limit = 1): Camera {
  const width = Math.max(1, frame.width - (frame.coveredRight ?? 0) - margin * 2);
  const height = Math.max(1, frame.height - margin * 2);
  const k = clampScale(Math.min(limit, width / box.width, height / box.height));
  return centreOn({ x: box.x + box.width / 2, y: box.y + box.height / 2 }, frame, k);
}

/** The camera at scale `k` with a point of the map in the middle of the free part of the frame. */
export function centreOn(point: Point, frame: Frame, k: number): Camera {
  const free = frame.width - (frame.coveredRight ?? 0);
  return { k, tx: free / 2 - point.x * k, ty: frame.height / 2 - point.y * k };
}

/**
 * The smallest move that brings a box of the map fully into the free part of the frame (with a margin); the same
 * camera when it is already in view.
 */
export function reveal(camera: Camera, box: Box, frame: Frame, margin = 24): Camera {
  const free = frame.width - (frame.coveredRight ?? 0);
  const left = box.x * camera.k + camera.tx;
  const top = box.y * camera.k + camera.ty;
  const right = left + box.width * camera.k;
  const bottom = top + box.height * camera.k;
  let dx = 0;
  let dy = 0;
  if (right > free - margin) {
    dx = free - margin - right;
  }
  if (left + dx < margin) {
    dx = margin - left;
  }
  if (bottom > frame.height - margin) {
    dy = frame.height - margin - bottom;
  }
  if (top + dy < margin) {
    dy = margin - top;
  }
  return dx || dy ? panBy(camera, dx, dy) : camera;
}

/** The camera between two, at `t` from 0 to 1 (the scale moves geometrically, so a zoom feels even). */
export function between(from: Camera, to: Camera, t: number): Camera {
  if (t <= 0) {
    return from;
  }
  if (t >= 1) {
    return to;
  }
  const k = from.k * Math.pow(to.k / from.k, t);
  // Interpolate the map point at the frame's origin, so the view travels in a straight line at either scale.
  const fx = -from.tx / from.k;
  const fy = -from.ty / from.k;
  const gx = -to.tx / to.k;
  const gy = -to.ty / to.k;
  const x = fx + (gx - fx) * t;
  const y = fy + (gy - fy) * t;
  return { k, tx: -x * k, ty: -y * k };
}
