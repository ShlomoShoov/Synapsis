import { describe, expect, it } from 'vitest';
import { describe as describeUrl } from '../api/queryLog';
import { sizeOf, tokensOf, trimmed } from './jsonView';

describe('trimmed', () => {
  it('cuts long lists and says how many more', () => {
    expect(trimmed({ points: [1, 2, 3, 4, 5], cells: [] }, 2)).toEqual({
      points: [1, 2, '… more: 3'],
      cells: [],
    });
  });

  it('cuts nested lists too, and leaves short ones (a point) whole', () => {
    const boundary = [
      [0, 0],
      [1, 1],
      [2, 2],
    ];
    expect(trimmed([{ boundary }], 2)).toEqual([{ boundary: [[0, 0], [1, 1], '\u2026 more: 1'] }]);
  });
});

describe('tokensOf', () => {
  it('tells keys, strings, numbers, literals and notes apart', () => {
    const kinds = tokensOf(JSON.stringify({ a: 'x', b: 2, c: null, d: ['… more: 3'] }))
      .filter((token) => token.kind !== 'punct')
      .map((token) => `${token.kind}:${token.text}`);
    expect(kinds).toEqual([
      'key:"a"',
      'string:"x"',
      'key:"b"',
      'number:2',
      'key:"c"',
      'literal:null',
      'key:"d"',
      'note:"… more: 3"',
    ]);
  });
});

describe('the query log', () => {
  it('reads the route and every parameter, a repeated one once per value', () => {
    expect(describeUrl('http://localhost:8081/analyze/map?type=A&type=B&zoom=2')).toEqual({
      route: 'GET /analyze/map',
      params: [
        ['type', 'A'],
        ['type', 'B'],
        ['zoom', '2'],
      ],
    });
  });
});

describe('sizeOf', () => {
  it('speaks in bytes, kB and MB', () => {
    expect(sizeOf(812)).toBe('812 B');
    expect(sizeOf(12_400)).toBe('12.4 kB');
    expect(sizeOf(1_300_000)).toBe('1.3 MB');
  });
});
