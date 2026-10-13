/**
 * The map's colours: a night Earth in deep space, and the events as coloured lights. The ground is quiet blues so that
 * the lights are the only bright colour on the screen. Change them here, never in the style or the components.
 */

/** The basemap at night. */
export const ground = {
  space: '#03050a',
  land: '#0e1a26',
  sea: '#050c16',
  coast: '#2c5878',
  coastGlow: '#2f7fb8',
  river: '#14324a',
  border: '#3a5670',
  division: '#22384c',
  graticule: '#16283a',
  label: '#9db4c9',
  labelHalo: '#03070d',
  seaLabel: '#3f6b8c',
  reliefShadow: '#000308',
  reliefHighlight: '#3b5f82',
  /** The air at the rim of the Earth. */
  horizon: '#3a86ff',
};

/**
 * The categories of events, told apart by colour: the first two parts of a type ("SEC.ATTACK" in
 * SEC.ATTACK.BOMBING), or its first part alone. Bright, distinct hues that glow on the night Earth.
 */
export const categories: { key: string; name: string; color: string }[] = [
  { key: 'SEC.ATTACK', name: 'Attacks', color: '#ff7a45' },
  { key: 'SEC.CONFLICT', name: 'Armed conflict', color: '#ff3d71' },
  { key: 'SEC', name: 'Security', color: '#ff9e7a' },
  { key: 'GEO', name: 'Earthquakes and the Earth', color: '#3ddcff' },
  { key: 'CYB', name: 'Cyber', color: '#a98bff' },
  { key: 'TXT', name: 'Texts', color: '#ffd166' },
];

/** The category of a type not listed above. */
export const otherCategory = { key: 'OTHER', name: 'Other', color: '#dce8ff' };

/** The light of a hexagon of counted events (no type is known there). */
export const densityColor = '#ffe9c4';

/** The category of a record type: the most specific listed key it starts with. */
export function categoryOf(type: string): { key: string; name: string; color: string } {
  for (const category of categories) {
    if (type === category.key || type.startsWith(`${category.key}.`)) return category;
  }
  return otherCategory;
}

/** "#ff7a45" → [1, 0.478, 0.27] */
export function rgbOf(hex: string): [number, number, number] {
  const value = Number.parseInt(hex.slice(1), 16);
  return [((value >> 16) & 255) / 255, ((value >> 8) & 255) / 255, (value & 255) / 255];
}
