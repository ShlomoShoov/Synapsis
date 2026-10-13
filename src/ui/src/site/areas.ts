/**
 * The areas of Synapsis, in the order the header and the home page's index show them.
 * A new area adds one entry here and its route in router.ts.
 */

/** One area of the product. */
export interface Area {
  /** The name on its button. */
  name: string;
  /** Its address. */
  path: string;
  /** One sentence on what it does. */
  line: string;
  /** Whether it is built; until then its address shows a "Coming soon" page. */
  ready: boolean;
}

export const areas: readonly Area[] = [
  {
    name: 'Getting started',
    path: '/getting-started',
    line: 'How Synapsis works, and how a new source joins it.',
    ready: false,
  },
  {
    name: 'Observe',
    path: '/observe',
    line: 'What the system receives, live and in the past: its flow and its numbers.',
    ready: true,
  },
  {
    name: 'Analyze',
    path: '/analyze',
    line: 'One shared space of every record: find by meaning, then move by connection, place and time.',
    ready: false,
  },
];
