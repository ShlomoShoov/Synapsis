/**
 * The stars behind the globe: two tiles of stars drawn once on a canvas (fine distant ones, and fewer brighter ones),
 * repeated over the background. As the globe turns, the layers slide at two speeds, so the sky has depth. Seeded, so the
 * sky is the same at every visit.
 */

/** Draws a square tile of stars and returns it as an image address. */
export function starTile(size: number, count: number, seed: number, bright: boolean): string {
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d');
  if (!context) return '';
  const random = seeded(seed);
  for (let index = 0; index < count; index++) {
    const x = random() * size;
    const y = random() * size;
    const magnitude = random() ** (bright ? 1.5 : 3);
    const radius = bright ? 0.7 + magnitude * 1.4 : 0.35 + magnitude * 0.75;
    const alpha = bright ? 0.55 + magnitude * 0.45 : 0.25 + magnitude * 0.6;
    // Most stars are blue-white; a few are warm.
    const warm = random() < 0.18;
    const [r, g, b] = warm ? [255, 226, 196] : [214, 228, 255];
    if (bright) {
      const glow = context.createRadialGradient(x, y, 0, x, y, radius * 5);
      glow.addColorStop(0, `rgba(${r},${g},${b},${alpha * 0.35})`);
      glow.addColorStop(1, `rgba(${r},${g},${b},0)`);
      context.fillStyle = glow;
      context.fillRect(x - radius * 5, y - radius * 5, radius * 10, radius * 10);
    }
    context.fillStyle = `rgba(${r},${g},${b},${alpha})`;
    context.beginPath();
    context.arc(x, y, radius, 0, Math.PI * 2);
    context.fill();
  }
  return canvas.toDataURL('image/png');
}

/** The size of the two star tiles, in pixels (far, near). */
export const tileSizes = { far: 900, near: 1300 };

/**
 * How far each layer of stars is moved for a view, within one tile (the layers are a tile larger than the screen on
 * every side, so a move never shows an edge). The far layer moves less than the near one.
 */
export function starShifts(lng: number, lat: number): { far: string; near: string } {
  const shift = (pixelsPerDegree: number, size: number) => {
    const x = (((-lng * pixelsPerDegree) % size) + size) % size;
    const y = (((lat * pixelsPerDegree) % size) + size) % size;
    return `translate3d(${(x - size).toFixed(1)}px, ${(y - size).toFixed(1)}px, 0)`;
  };
  return { far: shift(1.6, tileSizes.far), near: shift(3.4, tileSizes.near) };
}

/** A small seeded random generator (mulberry32). */
function seeded(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
