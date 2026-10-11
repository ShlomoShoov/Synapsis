import { describe, expect, it } from 'vitest';
import { defaultFilter, previousRange, rangeOf, readFilter, writeFilter } from './filters';

describe('metrics filters', () => {
  it('round-trips through an address, leaving defaults out', () => {
    const url = new URL('http://localhost/observe#metrics');
    writeFilter(
      { ...defaultFilter, preset: '7d', sources: ['src:cisa:kev', 'src:nist:nvd'], compare: false },
      url,
    );
    expect(url.search).toBe('?range=7d&sources=src%3Acisa%3Akev%2Csrc%3Anist%3Anvd&compare=off');
    expect(readFilter(url.searchParams)).toMatchObject({
      preset: '7d',
      sources: ['src:cisa:kev', 'src:nist:nvd'],
      compare: false,
      refresh: '30s',
    });
  });

  it('reads a custom period, which wins over a preset', () => {
    const filter = readFilter(
      new URLSearchParams('range=1h&from=2026-10-01T00:00:00Z&to=2026-10-02T00:00:00Z'),
    );
    expect(filter.preset).toBeNull();
    expect(rangeOf(filter, 0)).toEqual({
      from: Date.parse('2026-10-01T00:00:00Z'),
      to: Date.parse('2026-10-02T00:00:00Z'),
    });
  });

  it('ignores what it does not understand', () => {
    expect(readFilter(new URLSearchParams('range=2y&refresh=1s'))).toEqual(defaultFilter);
  });

  it('gives the period just before, of the same length', () => {
    expect(previousRange({ from: 100, to: 160 })).toEqual({ from: 40, to: 100 });
  });
});
