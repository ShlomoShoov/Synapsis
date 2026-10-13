/**
 * The map's colours. The ground is the home page's paper and ink; the events are the only saturated colours, so they
 * are what the eye finds first. Change them here, never in the style or the components.
 */

/** The basemap: paper land, a cool grey-green sea, ink lines and labels. */
export const ground = {
  /** The page around the globe (the home page's paper). */
  space: '#f4f0e7',
  land: '#f7f3ea',
  sea: '#d9e0dc',
  /** The coast, the edge of the sea. */
  coast: '#a7b2ad',
  seaLabel: '#6f8580',
  river: '#b9c8c3',
  border: '#8f8676',
  division: '#b7ae9d',
  graticule: '#b9b2a2',
  label: '#4a443a',
  labelHalo: '#f7f3ea',
  relief: '#6b6457',
  /** The thin air at the rim of the globe. */
  horizon: '#e4ebe8',
};

/**
 * The domains of event types: the first part of a type ("SEC" in SEC.ATTACK.BOMBING). Each has one colour, used for its
 * points and wherever the domain is named.
 */
export const domains: Record<string, { name: string; color: string }> = {
  SEC: { name: 'Security', color: '#c4452d' },
  GEO: { name: 'Earth', color: '#1f7a8c' },
  CYB: { name: 'Cyber', color: '#6b4fbb' },
  TXT: { name: 'Texts', color: '#8c6d1f' },
};

/** The colour of a domain not listed above. */
export const otherDomain = { name: 'Other', color: '#5b5548' };

/** The domain of a record type: "SEC.ATTACK.BOMBING" → SEC. */
export function domainOf(type: string): string {
  const head = type.split('.')[0] ?? '';
  return head in domains ? head : 'OTHER';
}

/** The colour of a record type, by its domain. */
export function colorOfType(type: string): string {
  return domains[domainOf(type)]?.color ?? otherDomain.color;
}

/**
 * The hexagons' ramp, from one event to the most events in view: warm sand to oxblood. Read with the legend; the
 * column's height says the same thing.
 */
export const cellRamp = ['#f0d29a', '#e7ad60', '#d97f3f', '#c2532b', '#93301d', '#5c1b11'];
