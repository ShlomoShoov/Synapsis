/**
 * The small marks of the definition kinds, drawn before each row of a card and in the inspector: one shape per kind,
 * so a row is read at a glance even zoomed out (a source is a ringed dot, an event type a diamond, an entity type a
 * square). 12 pixels, 1.25-pixel strokes, in the colour of the text around them.
 */
import type { DefinitionKind } from './graph';

const shapes: Record<DefinitionKind, readonly string[]> = {
  // A feed: a dot in a ring.
  source: [
    'M6 6m-4.25 0a4.25 4.25 0 1 0 8.5 0a4.25 4.25 0 1 0 -8.5 0',
    'M6 6m-1.4 0a1.4 1.4 0 1 0 2.8 0a1.4 1.4 0 1 0 -2.8 0',
  ],
  // Something that happened: a diamond.
  event: ['M6 1.6L10.4 6L6 10.4L1.6 6z'],
  // A thing with an identity: a square.
  entity: ['M2.2 2.2h7.6v7.6h-7.6z'],
  // Two things joined.
  relation: [
    'M3.4 6m-1.6 0a1.6 1.6 0 1 0 3.2 0a1.6 1.6 0 1 0 -3.2 0',
    'M8.6 6m-1.6 0a1.6 1.6 0 1 0 3.2 0a1.6 1.6 0 1 0 -3.2 0',
    'M5 6h2',
  ],
  // A part to play: an open circle.
  role: ['M6 6m-3.6 0a3.6 3.6 0 1 0 7.2 0a3.6 3.6 0 1 0 -7.2 0'],
  // A quantity: a ruler.
  measure: ['M1.8 8.6h8.4', 'M3 8.6v-2.4', 'M6 8.6v-4.6', 'M9 8.6v-2.4'],
  // A tree of values.
  space: ['M6 2v3', 'M6 5L3 9', 'M6 5l3 4', 'M3 9h.01', 'M9 9h.01', 'M6 2h.01'],
  // A shelf of keys.
  namespace: ['M2 3.4h8', 'M2 6h8', 'M2 8.6h5'],
  // A finding: a four-point spark.
  conclusion: [
    'M6 1.6c.4 2.6 1.8 4 4.4 4.4c-2.6.4-4 1.8-4.4 4.4c-.4-2.6-1.8-4-4.4-4.4c2.6-.4 4-1.8 4.4-4.4z',
  ],
  // A closed list.
  vocabulary: [
    'M3.8 3.2h6.2',
    'M3.8 6h6.2',
    'M3.8 8.8h6.2',
    'M1.9 3.2h.01',
    'M1.9 6h.01',
    'M1.9 8.8h.01',
  ],
};

/** The mark of a kind; decorative (the row names the kind in words for screen readers). */
export function KindGlyph({ kind, size = 12 }: { kind: DefinitionKind; size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 12 12"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.25}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      style={{ flex: 'none' }}
    >
      {shapes[kind].map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  );
}
