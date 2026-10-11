import { describe, expect, it } from 'vitest';
import type { SchemaDocument } from '../data/contracts';
import { buildSchemaGraph } from './graph';
import { labelOf, searchGraph } from './search';

describe('searching the schema map', async () => {
  const documents = (await import('../data/simulation/demo-packages.json'))
    .default as SchemaDocument[];
  const graph = buildSchemaGraph(documents);

  it('puts an exact code first, then codes that start with the query', () => {
    const results = searchGraph(graph, 'vulnerability');
    expect(labelOf(results[0] as (typeof results)[number])).toBe('Vulnerability');
  });

  it('finds sources by their short id, and packages by name', () => {
    expect(searchGraph(graph, 'cisa:kev').map(labelOf)).toContain('src:cisa:kev');
    expect(searchGraph(graph, 'kev')[0]).toMatchObject({ type: 'package', name: 'kev' });
  });

  it('matches the start of a word inside a code', () => {
    const labels = searchGraph(graph, 'attack', 20).map(labelOf);
    expect(labels).toContain('SEC.ATTACK.BOMBING');
  });

  it('finds nothing for an empty query, and keeps to the limit', () => {
    expect(searchGraph(graph, '  ')).toEqual([]);
    expect(searchGraph(graph, 'e', 5)).toHaveLength(5);
  });
});
