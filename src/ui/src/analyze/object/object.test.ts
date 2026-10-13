import { describe, expect, it } from 'vitest';
import type { WireGraph, WireRecordAnswer } from '../api/contract';
import cve from '../../../fixtures/analyze/records-cve-2021-44228.json';
import ucdp from '../../../fixtures/analyze/records-ucdp-7578.json';
import log4j2Graph from '../../../fixtures/analyze/graph-entity-log4j2.json';
import microsoftGraph from '../../../fixtures/analyze/graph-hub-microsoft.json';
import ucdpGraph from '../../../fixtures/analyze/graph-event-ucdp-7578.json';
import { groupsOf } from './connections';
import { formatTime, headingOfRecord, sourceName, typeWords, words } from './describe';
import { viewOf } from './record';

const conflict = (ucdp as unknown as WireRecordAnswer[])[0]!.record;
const vulnerability = (cve as unknown as WireRecordAnswer[])[0]!.record;

describe('words', () => {
  it('turns codes into words', () => {
    expect(words('deaths_best')).toBe('Deaths best');
    expect(words('ArmedActor')).toBe('Armed actor');
    expect(typeWords('SEC.CONFLICT.STATE_BASED')).toBe('State based');
    expect(sourceName('src:ucdp:ged')).toBe('UCDP GED');
  });

  it('writes a time at its precision, in UTC', () => {
    expect(formatTime('1989-01-01T00:00:00Z', 'day')).toBe('1 Jan 1989');
    expect(formatTime('1965-01-30T04:37:21Z', 'second')).toBe('30 Jan 1965, 04:37:21 UTC');
    expect(formatTime('1989-06-01T00:00:00Z', 'month')).toBe('Jun 1989');
  });
});

describe('a record laid out', () => {
  it('names an event with no title by its type and country', () => {
    expect(headingOfRecord(conflict)).toMatchObject({
      kind: 'event',
      title: 'State based · Sudan',
      family: 'Armed conflict',
    });
  });

  it('keeps every fact of the record', () => {
    const view = viewOf(conflict);
    expect(view.facts.map((fact) => fact.label)).toEqual([
      'When',
      'Where',
      'Point',
      'Source',
      'Known',
      'Reliability',
    ]);
    expect(view.facts[0]).toMatchObject({ value: '1 Jan 1989 → 4 Oct 1989', code: 'day' });
    expect(view.facts[2]).toMatchObject({ value: '5.00° N, 33.00° E', code: '± 300 km' });
    expect(view.point).toEqual([33, 5]);
    expect(view.measures).toHaveLength(7);
    expect(view.named).toEqual([
      { id: 'ent:ucdp-actor:112', role: 'Side a' },
      { id: 'ent:ucdp-actor:466', role: 'Side b' },
    ]);
    expect(view.attributes.find((a) => a.label === 'Dyad name')?.value).toBe(
      'Government of Sudan - SPLM/A',
    );
  });

  it('keeps a text, a logical place, lists and links', () => {
    const view = viewOf(vulnerability);
    expect(view.title).toBe('Apache Log4j2 Remote Code Execution Vulnerability');
    expect(view.facts.find((fact) => fact.label === 'Filed under')).toMatchObject({
      label: 'Filed under',
      value: 'Apache / Log4j2',
      code: 'cyber.vendor_product',
    });
    expect(view.attributes.find((a) => a.label === 'Cwes')?.value).toBe('CWE-20, CWE-400, CWE-502');
    expect(view.attributes.find((a) => a.label === 'Notes')?.link).toBe(true);
    expect(view.evidence?.ref).toBe('CVE-2021-44228');
  });
});

describe('connections', () => {
  it('divides the links by kind, read from the centre', () => {
    const groups = groupsOf(log4j2Graph as unknown as WireGraph);
    expect(groups.map((group) => [group.title, group.neighbours.length])).toEqual([
      ['Takes part in', 2],
      ['Conclusions about it', 1],
      ['Product of', 1],
    ]);
    expect(groups[0]!.neighbours[0]).toMatchObject({
      kind: 'event',
      label: 'Apache Log4j2 Deserialization of Untrusted Data Vulnerability',
      detail: 'Affected product · Known exploited · 2023-05-01',
    });
  });

  it('names the sides of an event', () => {
    const [group] = groupsOf(ucdpGraph as unknown as WireGraph);
    expect(group!.title).toBe('Taking part');
    expect(group!.neighbours.map((n) => n.label)).toEqual(['SPLM/A', 'Government of Sudan']);
  });

  it('groups only what was returned from a hub', () => {
    const graph = microsoftGraph as unknown as WireGraph;
    const groups = groupsOf(graph);
    expect(graph.total).toBe(460);
    expect(groups.reduce((sum, group) => sum + group.neighbours.length, 0)).toBe(200);
  });
});
