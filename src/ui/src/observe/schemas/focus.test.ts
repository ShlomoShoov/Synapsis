import { describe, expect, it } from 'vitest';
import type { SchemaDocument } from '../data/contracts';
import { highlightOf, sameTarget } from './focus';
import { buildSchemaGraph } from './graph';

describe('what the map lights up', async () => {
  const documents = (await import('../data/simulation/demo-packages.json'))
    .default as SchemaDocument[];
  const graph = buildSchemaGraph(documents);

  it('lights a shared entity type with every package that uses it', () => {
    const light = highlightOf(graph, { type: 'definition', id: 'entity:Vulnerability' });
    expect(light.rows.get('entity:Vulnerability')).toBe('focus');
    expect([...(light.cards ?? [])].sort()).toEqual(['core', 'epss', 'kev', 'nvd', 'social']);
    expect(
      [...light.references].every((reference) => reference.to === 'entity:Vulnerability'),
    ).toBe(true);
  });

  it('lights a package with its threads across packages only', () => {
    const light = highlightOf(graph, { type: 'package', name: 'gtd' });
    expect([...(light.cards ?? [])].sort()).toEqual(['core', 'gtd']);
    expect(light.references.size).toBeGreaterThan(0);
    expect([...light.references].every((reference) => reference.crossPackage)).toBe(true);
    expect(light.rows.get('entity:ArmedActor')).toBe('linked');
  });

  it('dims nothing without a target, and lights chosen domains', () => {
    expect(highlightOf(graph, null).cards).toBeNull();
    const light = highlightOf(graph, null, new Set(['health']));
    expect([...(light.cards ?? [])].sort()).toEqual(['delphi', 'health', 'jhu-covid']);
  });

  it('compares targets', () => {
    expect(sameTarget({ type: 'package', name: 'kev' }, { type: 'package', name: 'kev' })).toBe(
      true,
    );
    expect(sameTarget({ type: 'package', name: 'kev' }, { type: 'definition', id: 'kev' })).toBe(
      false,
    );
    expect(sameTarget(null, null)).toBe(true);
  });
});
