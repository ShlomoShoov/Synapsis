import { describe, expect, it } from 'vitest';
import {
  bboxParam,
  cellsQuery,
  filterParams,
  graphQuery,
  isoOf,
  logicalQuery,
  pointsQuery,
  recordPath,
  resolutionOf,
  searchQuery,
} from './query';

const day = (text: string) => Date.parse(`${text}T00:00:00Z`);

describe('isoOf', () => {
  it('writes UTC with whole seconds and a Z', () => {
    expect(isoOf(day('2026-10-13'))).toBe('2026-10-13T00:00:00Z');
    expect(isoOf(day('2026-10-13') + 1_500)).toBe('2026-10-13T00:00:01Z');
  });
});

describe('filterParams', () => {
  it('repeats a list filter, never joins it', () => {
    const params = filterParams({
      types: ['SEC.ATTACK.BOMBING', 'GEO.EARTHQUAKE'],
      sources: ['src:usgs:comcat'],
    });
    expect(params.getAll('type')).toEqual(['SEC.ATTACK.BOMBING', 'GEO.EARTHQUAKE']);
    expect(params.toString()).toBe(
      'source=src%3Ausgs%3Acomcat&type=SEC.ATTACK.BOMBING&type=GEO.EARTHQUAKE',
    );
  });

  it('sends the times and leaves out what is not set', () => {
    const params = filterParams({ from: day('1970-01-01'), to: day('1970-04-01') });
    expect(params.toString()).toBe('from=1970-01-01T00%3A00%3A00Z&to=1970-04-01T00%3A00%3A00Z');
  });
});

describe('resolutionOf', () => {
  it('maps the zoom to the H3 resolution', () => {
    expect([0, 3.9, 4, 5.9, 6, 7.9, 8, 14].map(resolutionOf)).toEqual([2, 2, 3, 3, 4, 4, 5, 5]);
  });
});

describe('cellsQuery and pointsQuery', () => {
  it('add the resolution or the box and the limit', () => {
    const box = { west: 170, south: 45, east: -170, north: 55 };
    expect(cellsQuery({}, 3, box)).toBe('resolution=3&bbox=170%2C45%2C-170%2C55');
    expect(cellsQuery({ from: day('1965-01-01') }, 2)).toBe(
      'from=1965-01-01T00%3A00%3A00Z&resolution=2',
    );
    expect(pointsQuery({}, box, 5000)).toBe('bbox=170%2C45%2C-170%2C55&limit=5000');
  });
});

describe('searchQuery, logicalQuery and graphQuery', () => {
  it('send only what is set, ids as query values', () => {
    expect(searchQuery({}, { q: ' log4j ', semantic: false })).toBe('q=log4j');
    expect(searchQuery({}, { entity: 'ent:kev-product:apache/log4j2', limit: 20 })).toBe(
      'entity=ent%3Akev-product%3Aapache%2Flog4j2&limit=20',
    );
    expect(logicalQuery({}, 'cyber.vendor_product')).toBe('node=cyber.vendor_product');
    expect(logicalQuery({})).toBe('');
    expect(graphQuery('evt:kev:CVE-2021-44228')).toBe('id=evt%3Akev%3ACVE-2021-44228');
  });
});

describe('recordPath', () => {
  it('keeps the slashes of the id in the path and encodes the rest', () => {
    expect(recordPath('ent:kev-product:apache/log4j2')).toBe(
      '/records/ent%3Akev-product%3Aapache/log4j2',
    );
  });
});

describe('bboxParam', () => {
  it('rounds to four decimals', () => {
    expect(bboxParam({ west: -125.123456, south: 24, east: -66, north: 50.00001 })).toBe(
      '-125.1235,24,-66,50',
    );
  });
});
