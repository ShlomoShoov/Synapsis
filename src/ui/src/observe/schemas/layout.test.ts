import { describe, expect, it } from 'vitest';
import type { SchemaDocument } from '../data/contracts';
import { buildSchemaGraph } from './graph';
import { layoutSchemaGraph, metrics, overlaps, threadOf } from './layout';

async function demoGraph() {
  const documents = (await import('../data/simulation/demo-packages.json'))
    .default as SchemaDocument[];
  return buildSchemaGraph(documents);
}

describe('the schema map layout', () => {
  it('puts the hub in the middle and every other card beside it, none overlapping', async () => {
    const graph = await demoGraph();
    const layout = layoutSchemaGraph(graph);
    const hub = layout.cardOf.get('core');
    expect(hub?.side).toBe('hub');
    expect(hub?.x).toBe(-metrics.hubWidth / 2);
    expect(layout.cards).toHaveLength(graph.packages.length);
    const overlapping = layout.cards.flatMap((a, i) =>
      layout.cards
        .slice(i + 1)
        .filter((b) => overlaps(a, b))
        .map((b) => `${a.name}/${b.name}`),
    );
    expect(overlapping).toEqual([]);
  });

  it('stands the packages that use the hub next to it, and the others further out', async () => {
    const graph = await demoGraph();
    const layout = layoutSchemaGraph(graph);
    const users = new Set(
      graph.dependencies.filter((item) => item.to === 'core').map((item) => item.from),
    );
    expect(layout.cards.filter((card) => users.has(card.name) && card.column !== 'inner')).toEqual(
      [],
    );
    // Both sides are used, neither is more than twice as tall as the other, and the map is wider than tall.
    const height = (side: string) =>
      Math.max(
        ...layout.groups
          .filter((group) => group.side === side)
          .map((group) => group.y + group.height),
      ) - Math.min(...layout.groups.filter((group) => group.side === side).map((group) => group.y));
    expect(height('left') / height('right')).toBeGreaterThan(0.5);
    expect(height('left') / height('right')).toBeLessThan(2);
    expect(layout.bounds.width).toBeGreaterThan(layout.bounds.height);
  });

  it('gives every definition a row to hang threads on, and opens a card on demand', async () => {
    const graph = await demoGraph();
    const closed = layoutSchemaGraph(graph);
    const missing = graph.packages.flatMap((pkg) =>
      pkg.definitions.filter(
        (definition) => !closed.cardOf.get(pkg.name)?.anchors.has(definition.id),
      ),
    );
    expect(missing).toEqual([]);
    const fdic = closed.cardOf.get('fdic');
    expect(fdic?.expandable).toBe(true);
    const open = layoutSchemaGraph(graph, new Set(['fdic'])).cardOf.get('fdic');
    expect(open?.height).toBeGreaterThan(fdic?.height ?? Infinity);
    expect(open?.footer?.type).toBe('less');
    // Core's measures are used by another package, so they keep rows of their own while it is closed.
    expect(closed.cardOf.get('core')?.sections.map((section) => section.kind)).toContain('measure');
  });

  it('draws a thread from the using row to the used one, facing each other', async () => {
    const graph = await demoGraph();
    const layout = layoutSchemaGraph(graph);
    const reference = graph.references.find(
      (item) =>
        item.from === 'event:CYB.VULN.SCORED' || (item.crossPackage && item.kind === 'type'),
    );
    expect(reference).toBeDefined();
    const thread = reference ? threadOf(layout, graph, reference) : null;
    expect(thread?.d).toMatch(/^M-?[\d.]+ -?[\d.]+C/);
    const hub = layout.cardOf.get('core');
    expect([hub?.x, (hub?.x ?? 0) + (hub?.width ?? 0)]).toContain(thread?.to.x);
  });

  it('is the same every time', async () => {
    const graph = await demoGraph();
    expect(
      JSON.stringify(layoutSchemaGraph(graph).cards.map((card) => [card.name, card.x, card.y])),
    ).toBe(
      JSON.stringify(layoutSchemaGraph(graph).cards.map((card) => [card.name, card.x, card.y])),
    );
  });
});
