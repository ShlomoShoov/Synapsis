/**
 * The threads of the map: one curve per reference, from the row that uses a definition to the row of the definition,
 * in the colour of the user's domain. At rest only the threads between packages are drawn (uses of a role aside: every
 * event type has one); a lit target draws its own threads bright, the others fade, and the role an entity type plays
 * is written by its thread.
 *
 * Strokes keep their width on screen at any zoom (non-scaling-stroke), so the threads stay fine lines far out and
 * never grow heavy close in.
 */
import { memo } from 'react';
import { domainById } from '../domains';
import type { Highlight } from './focus';
import type { Reference, SchemaGraph } from './graph';
import { threadOf, type Box, type MapLayout, type Thread } from './layout';
import styles from './SchemaMap.module.css';

/** Whether a reference is drawn while nothing is lit. */
function drawnAtRest(reference: Reference): boolean {
  return reference.crossPackage && reference.kind !== 'role';
}

/**
 * The roles written by lit threads: near the used end, on the side the thread comes from, once per row and role
 * (nine event types of one package taking the same role need one label).
 */
function roleLabels(threads: readonly Thread[]) {
  const labels = new Map<
    string,
    { key: string; x: number; y: number; anchor: 'start' | 'end'; text: string }
  >();
  for (const thread of threads) {
    const role = thread.reference.kind === 'type' ? thread.reference.role : undefined;
    if (!role) {
      continue;
    }
    const leftward = thread.from.x > thread.to.x;
    const key = `${thread.to.x}:${thread.to.y}:${leftward}:${role}`;
    if (!labels.has(key)) {
      labels.set(key, {
        key,
        x: thread.to.x + (leftward ? 10 : -10),
        y: thread.to.y - 6,
        anchor: leftward ? 'start' : 'end',
        text: role,
      });
    }
  }
  // Two roles on one row and side stack upwards.
  const stacked = new Map<string, number>();
  return [...labels.values()].map((label) => {
    const spot = `${label.x}:${label.y}`;
    const index = stacked.get(spot) ?? 0;
    stacked.set(spot, index + 1);
    return { ...label, y: label.y - index * 13 };
  });
}

/** The threads layer, in map coordinates. */
export const Threads = memo(function Threads({
  graph,
  layout,
  highlight,
}: {
  graph: SchemaGraph;
  layout: MapLayout;
  highlight: Highlight;
}) {
  const lit = highlight.references;
  const box: Box = layout.bounds;
  const threads: { thread: Thread; state: 'lit' | 'rest' | 'dim'; color: string }[] = [];
  for (const reference of graph.references) {
    const isLit = lit.has(reference);
    if (!isLit && !drawnAtRest(reference)) {
      continue;
    }
    const thread = threadOf(layout, graph, reference);
    if (!thread) {
      continue;
    }
    const from = graph.definitions.get(reference.from);
    const pkg = from ? graph.packageByName.get(from.packageName) : undefined;
    // Lit threads stand out from the rest; with only cards lit (chosen domains), the threads of the others fade.
    const state = isLit
      ? 'lit'
      : lit.size > 0 || (highlight.cards !== null && !(pkg && highlight.cards.has(pkg.name)))
        ? 'dim'
        : 'rest';
    threads.push({ thread, state, color: domainById(pkg?.domain ?? 'other').color });
  }
  // Bright threads last, so they cross over the faded ones.
  threads.sort((a, b) => Number(a.state === 'lit') - Number(b.state === 'lit'));

  return (
    <svg
      className={styles.threads}
      style={{ left: box.x, top: box.y, width: box.width, height: box.height }}
      viewBox={`${box.x} ${box.y} ${box.width} ${box.height}`}
      aria-hidden="true"
    >
      {threads.map(({ thread, state, color }) => (
        <path
          key={`${thread.reference.from}>${thread.reference.to}>${thread.reference.kind}>${thread.reference.role ?? ''}`}
          className={styles.thread}
          data-state={state}
          d={thread.d}
          stroke={color}
          vectorEffect="non-scaling-stroke"
        />
      ))}
      {threads
        .filter(({ state }) => state !== 'dim')
        .map(({ thread, state, color }) => (
          <circle
            key={`end:${thread.reference.from}>${thread.reference.to}>${thread.reference.kind}>${thread.reference.role ?? ''}`}
            className={styles.port}
            data-state={state}
            cx={thread.to.x}
            cy={thread.to.y}
            r={state === 'lit' ? 3 : 2.2}
            fill={color}
          />
        ))}
      {roleLabels(threads.filter(({ state }) => state === 'lit').map(({ thread }) => thread)).map(
        (label) => (
          <text
            key={label.key}
            className={styles.threadLabel}
            x={label.x}
            y={label.y}
            textAnchor={label.anchor}
          >
            {label.text}
          </text>
        ),
      )}
    </svg>
  );
});
