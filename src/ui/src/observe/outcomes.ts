/**
 * What can happen to a record the API receives. Every record of a batch ends in exactly one outcome, so
 * received = new + changed + unchanged + duplicates + rejected (contracts/synapsis-api.yaml, IngestLogEntry).
 *
 * The colours tell the story without competing with the domains: what is written is light, what is left alone fades
 * into the room, and what is refused is red ink, as an editor marks a manuscript. The four light steps are one ramp
 * (checked: monotone, every step visible); red is the reserved "critical" colour.
 */

/** The outcome fields of a log entry. */
export type OutcomeKey = 'new' | 'changed' | 'unchanged' | 'duplicates' | 'rejected';

/** One outcome. */
export interface Outcome {
  key: OutcomeKey;
  /** The name in legends and tables. */
  label: string;
  /** What it means, in one line. */
  description: string;
  /** The colour of its marks. */
  color: string;
  /** True when the records were stored (a new record or a new version). */
  written: boolean;
}

/** Every outcome, from the most to the least written, refusals last. */
export const outcomes: readonly Outcome[] = [
  {
    key: 'new',
    label: 'New',
    description: 'Stored for the first time.',
    color: '#ece6d8',
    written: true,
  },
  {
    key: 'changed',
    label: 'New version',
    description: 'Stored as a new version of a record already held.',
    color: '#ada493',
    written: true,
  },
  {
    key: 'unchanged',
    label: 'Unchanged',
    description: 'Already stored exactly as it is: nothing written.',
    color: '#7a7366',
    written: false,
  },
  {
    key: 'duplicates',
    label: 'Duplicate',
    description: 'An exact copy inside the same batch, dropped.',
    color: '#57524a',
    written: false,
  },
  {
    key: 'rejected',
    label: 'Rejected',
    description: 'Refused by a rule, with an error code.',
    color: '#ef5b47',
    written: false,
  },
];

/** An outcome by its key. */
export function outcome(key: OutcomeKey): Outcome {
  return outcomes.find((item) => item.key === key) as Outcome;
}

/** The status colours: a state, never a series. Always shown with an icon and a word. */
export const statusColors = {
  good: '#5fb56a',
  warning: '#f0b43c',
  serious: '#e8834f',
  critical: '#ef5b47',
} as const;

/** A status. */
export type Status = keyof typeof statusColors;
