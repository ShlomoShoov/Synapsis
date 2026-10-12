/**
 * The icons of Observe: one family of 16-pixel line drawings, 1.5-pixel strokes, round ends, drawn in the colour of
 * the text around them. Each is a list of SVG path commands, so adding one is adding a line here.
 */
import type { SVGProps } from 'react';

const paths = {
  flow: [
    'M8 8m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0',
    'M2.5 8a5.5 2.6 0 1 0 11 0a5.5 2.6 0 1 0 -11 0',
    'M13.5 3.2l-.01 0',
  ],
  metrics: ['M2.5 13.5h11', 'M4 11V7.5', 'M7 11V3.5', 'M10 11V6', 'M13 11V8.5'],
  home: ['M2.5 7.2L8 2.8l5.5 4.4', 'M4 6.2v7h8v-7'],
  book: [
    'M3 3.5c2-.8 3.5-.6 5 .6c1.5-1.2 3-1.4 5-.6v9c-2-.8-3.5-.6-5 .6c-1.5-1.2-3-1.4-5-.6z',
    'M8 4.1v9',
  ],
  analyze: ['M7 7m-4 0a4 4 0 1 0 8 0a4 4 0 1 0 -8 0', 'M10 10l3.5 3.5'],
  play: ['M5 3.2v9.6L12.6 8z'],
  pause: ['M5.5 3.5v9', 'M10.5 3.5v9'],
  back: ['M12.5 3.5v9L6 8z', 'M3.5 3.5v9'],
  forward: ['M3.5 3.5v9L10 8z', 'M12.5 3.5v9'],
  live: [
    'M8 8m-1.6 0a1.6 1.6 0 1 0 3.2 0a1.6 1.6 0 1 0 -3.2 0',
    'M4.6 4.6a4.8 4.8 0 0 0 0 6.8',
    'M11.4 4.6a4.8 4.8 0 0 1 0 6.8',
  ],
  chevronDown: ['M4 6l4 4l4-4'],
  chevronRight: ['M6 4l4 4l-4 4'],
  chevronLeft: ['M10 4l-4 4l4 4'],
  close: ['M4 4l8 8', 'M12 4l-8 8'],
  search: ['M7 7m-4 0a4 4 0 1 0 8 0a4 4 0 1 0 -8 0', 'M10 10l3.2 3.2'],
  sliders: ['M3 4.5h6', 'M12 4.5h1', 'M10.5 3v3', 'M3 11.5h1', 'M7 11.5h6', 'M5.5 10v3'],
  table: ['M2.5 3.5h11v9h-11z', 'M2.5 6.5h11', 'M2.5 9.5h11', 'M6.5 6.5v6'],
  chart: ['M2.5 12.5l3.5-4l3 2.5l4.5-6'],
  grip: ['M6 4h.01', 'M10 4h.01', 'M6 8h.01', 'M10 8h.01', 'M6 12h.01', 'M10 12h.01'],
  eye: [
    'M1.8 8s2.3-4.3 6.2-4.3S14.2 8 14.2 8s-2.3 4.3-6.2 4.3S1.8 8 1.8 8z',
    'M8 8m-1.8 0a1.8 1.8 0 1 0 3.6 0a1.8 1.8 0 1 0 -3.6 0',
  ],
  eyeOff: [
    'M2.5 2.5l11 11',
    'M6.5 4c.5-.2 1-.3 1.5-.3c3.9 0 6.2 4.3 6.2 4.3s-.6 1.1-1.7 2.2',
    'M10.6 11.6c-.8.4-1.7.7-2.6.7c-3.9 0-6.2-4.3-6.2-4.3s.8-1.4 2.2-2.6',
  ],
  plus: ['M8 3.5v9', 'M3.5 8h9'],
  minus: ['M3.5 8h9'],
  fit: ['M2.5 6V2.5H6', 'M10 2.5h3.5V6', 'M13.5 10v3.5H10', 'M6 13.5H2.5V10'],
  check: ['M3.5 8.2l3 3l6-6.4'],
  warning: ['M8 2.8l5.8 10.2H2.2z', 'M8 6.8v2.8', 'M8 11.3h.01'],
  alert: ['M8 8m-5.5 0a5.5 5.5 0 1 0 11 0a5.5 5.5 0 1 0 -11 0', 'M8 5v3.4', 'M8 10.8h.01'],
  good: ['M8 8m-5.5 0a5.5 5.5 0 1 0 11 0a5.5 5.5 0 1 0 -11 0', 'M5.6 8.2l1.7 1.7l3.2-3.4'],
  external: ['M9 2.5h4.5V7', 'M13.5 2.5L7.5 8.5', 'M11.5 9.5v4h-9v-9h4'],
  clock: ['M8 8m-5.5 0a5.5 5.5 0 1 0 11 0a5.5 5.5 0 1 0 -11 0', 'M8 5v3.2l2.2 1.4'],
  sidebar: ['M2.5 3.5h11v9h-11z', 'M6 3.5v9'],
  refresh: ['M13 8a5 5 0 1 1-1.5-3.6', 'M13 2.8v2.8h-2.8'],
  target: ['M8 8m-5 0a5 5 0 1 0 10 0a5 5 0 1 0 -10 0', 'M8 8m-1 0a1 1 0 1 0 2 0a1 1 0 1 0 -2 0'],
  arrowRight: ['M3 8h10', 'M9.5 4.5L13 8l-3.5 3.5'],
  keyboard: ['M2 4.5h12v7H2z', 'M4.5 7h.01', 'M7 7h.01', 'M9.5 7h.01', 'M12 7h.01', 'M5 9.5h6'],
  database: [
    'M3 4c0-.8 2.2-1.5 5-1.5s5 .7 5 1.5s-2.2 1.5-5 1.5S3 4.8 3 4z',
    'M3 4v8c0 .8 2.2 1.5 5 1.5s5-.7 5-1.5V4',
    'M3 8c0 .8 2.2 1.5 5 1.5s5-.7 5-1.5',
  ],
  layers: ['M8 2.5l5.5 2.8L8 8.1L2.5 5.3z', 'M2.5 8l5.5 2.8L13.5 8', 'M2.5 10.7l5.5 2.8l5.5-2.8'],
} as const;

/** The name of an icon. */
export type IconName = keyof typeof paths;

/** One icon, 16 pixels by default; decorative unless given a `title`. */
export function Icon({
  name,
  size = 16,
  title,
  ...rest
}: { name: IconName; size?: number; title?: string } & Omit<SVGProps<SVGSVGElement>, 'name'>) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.5}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden={title ? undefined : true}
      role={title ? 'img' : undefined}
      {...rest}
    >
      {title ? <title>{title}</title> : null}
      {paths[name].map((d) => (
        <path key={d} d={d} fill={name === 'play' ? 'currentColor' : undefined} />
      ))}
    </svg>
  );
}
