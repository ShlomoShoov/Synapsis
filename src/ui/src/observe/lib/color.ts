/**
 * Small colour helpers: hex <-> OKLCH, the perceptual space the palette was designed and validated in.
 * OKLCH keeps a hue steady while its lightness changes, so a brighter "glow" of a colour is still the same colour.
 */

/** A colour in OKLCH: lightness 0-1, chroma 0-0.4, hue in degrees. */
export interface Oklch {
  l: number;
  c: number;
  h: number;
}

/** The sRGB channels of "#rrggbb", each 0-1. */
export function hexToRgb(hex: string): [number, number, number] {
  const value = hex.replace('#', '');
  return [0, 2, 4].map((i) => parseInt(value.slice(i, i + 2), 16) / 255) as [
    number,
    number,
    number,
  ];
}

/** "#rrggbb" from sRGB channels 0-1 (clamped). */
export function rgbToHex([r, g, b]: [number, number, number]): string {
  return (
    '#' +
    [r, g, b]
      .map((channel) =>
        Math.round(Math.min(1, Math.max(0, channel)) * 255)
          .toString(16)
          .padStart(2, '0'),
      )
      .join('')
  );
}

const toLinear = (x: number) => (x <= 0.04045 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4);
const toGamma = (x: number) => (x <= 0.0031308 ? 12.92 * x : 1.055 * x ** (1 / 2.4) - 0.055);

/** "#rrggbb" as OKLCH. */
export function hexToOklch(hex: string): Oklch {
  const [r, g, b] = hexToRgb(hex).map(toLinear) as [number, number, number];
  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
  const L = 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s;
  const A = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
  const B = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
  const hue = (Math.atan2(B, A) * 180) / Math.PI;
  return { l: L, c: Math.hypot(A, B), h: hue < 0 ? hue + 360 : hue };
}

/** OKLCH as "#rrggbb", clipped into sRGB. */
export function oklchToHex({ l, c, h }: Oklch): string {
  const a = c * Math.cos((h * Math.PI) / 180);
  const b = c * Math.sin((h * Math.PI) / 180);
  const l1 = (l + 0.3963377774 * a + 0.2158037573 * b) ** 3;
  const m1 = (l - 0.1055613458 * a - 0.0638541728 * b) ** 3;
  const s1 = (l - 0.0894841775 * a - 1.291485548 * b) ** 3;
  return rgbToHex([
    toGamma(4.0767416621 * l1 - 3.3077115913 * m1 + 0.2309699292 * s1),
    toGamma(-1.2684380046 * l1 + 2.6097574011 * m1 - 0.3413193965 * s1),
    toGamma(-0.0041960863 * l1 - 0.7034186147 * m1 + 1.707614701 * s1),
  ]);
}

/** The same hue at another lightness (and optionally another chroma): a glow, a shadow. */
export function withLightness(hex: string, lightness: number, chroma?: number): string {
  const color = hexToOklch(hex);
  return oklchToHex({ l: lightness, c: chroma ?? color.c, h: color.h });
}

/** A colour between two others, mixed in OKLab (t = 0 gives `from`, 1 gives `to`). */
export function mix(from: string, to: string, t: number): string {
  const a = hexToOklch(from);
  const b = hexToOklch(to);
  const ax = a.c * Math.cos((a.h * Math.PI) / 180);
  const ay = a.c * Math.sin((a.h * Math.PI) / 180);
  const bx = b.c * Math.cos((b.h * Math.PI) / 180);
  const by = b.c * Math.sin((b.h * Math.PI) / 180);
  const x = ax + (bx - ax) * t;
  const y = ay + (by - ay) * t;
  const hue = (Math.atan2(y, x) * 180) / Math.PI;
  return oklchToHex({
    l: a.l + (b.l - a.l) * t,
    c: Math.hypot(x, y),
    h: hue < 0 ? hue + 360 : hue,
  });
}
