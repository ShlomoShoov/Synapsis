import { describe, expect, it } from 'vitest';
import type { WireGraph } from '../api/contract';
import cve from '../../../fixtures/analyze/graph-event-cve-2021-44228.json';
import log4j2 from '../../../fixtures/analyze/graph-entity-log4j2.json';
import { emptyGraph, expand } from './layout';

describe('the graph as it grows', () => {
  it('places the object at the centre and each kind of connection apart', () => {
    const state = expand(emptyGraph(), log4j2 as unknown as WireGraph, 0);
    expect(state.root).toBe('ent:kev-product:apache/log4j2');
    expect(state.nodes.size).toBe(5);
    expect(state.nodes.get(state.root!)!.position).toEqual([0, 0, 0]);
    expect([...state.groups.values()].map((group) => [group.title, group.count])).toEqual([
      ['Takes part in', 2],
      ['Conclusions about it', 1],
      ['Product of', 1],
    ]);
  });

  it('adds a neighbour’s connections without moving what is placed', () => {
    const first = expand(emptyGraph(), log4j2 as unknown as WireGraph, 0);
    const before = first.nodes.get('ent:kev-vendor:apache')!.position;
    const next = expand(first, cve as unknown as WireGraph, 1);
    expect(next.nodes.size).toBe(6); // + CVE-2021-44228, the entity
    expect(next.nodes.get('ent:kev-vendor:apache')!.position).toEqual(before);
    expect(next.nodes.get('ent:cve:CVE-2021-44228')!.parent).toBe('evt:kev:CVE-2021-44228');
    // The link Log4j2 → CVE is read from both ends and drawn once.
    expect(next.edges.size).toBe(7);
  });
});
