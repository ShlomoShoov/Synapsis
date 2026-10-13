import { describe, expect, it } from 'vitest';
import { bboxParam, filterParams, graphQuery, isoOf, mapQuery, recordPath } from './query';

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
      countries: ['US'],
    });
    expect(params.getAll('type')).toEqual(['SEC.ATTACK.BOMBING', 'GEO.EARTHQUAKE']);
    expect(params.toString()).toBe('type=SEC.ATTACK.BOMBING&type=GEO.EARTHQUAKE&country=US');
  });

  it('sends the times and leaves out what is not set', () => {
    const params = filterParams({ from: day('1970-01-01'), to: day('1970-04-01'), q: '  ' });
    expect(params.toString()).toBe('from=1970-01-01T00%3A00%3A00Z&to=1970-04-01T00%3A00%3A00Z');
  });

  it('encodes ids that hold slashes as query values', () => {
    const params = filterParams({ entities: ['ent:kev-product:apache/log4j2'] });
    expect(params.toString()).toBe('entity=ent%3Akev-product%3Aapache%2Flog4j2');
  });
});

describe('mapQuery', () => {
  it('adds the box and the zoom, and the layer only when not auto', () => {
    const query = mapQuery(
      { from: day('1970-01-01') },
      { zoom: 4, bbox: { west: -125, south: 24, east: -66, north: 50 } },
    );
    expect(query).toBe('from=1970-01-01T00%3A00%3A00Z&bbox=-125%2C24%2C-66%2C50&zoom=4');
    expect(mapQuery({}, { zoom: 2, layer: 'cells' })).toBe('zoom=2&layer=cells');
  });
});

describe('graphQuery', () => {
  it('puts the id first, then the filters and the page', () => {
    expect(
      graphQuery('evt:gtd:197001060001', { asOf: day('2026-10-13') }, { group: 'event:X' }),
    ).toBe('id=evt%3Agtd%3A197001060001&as_of=2026-10-13T00%3A00%3A00Z&group=event%3AX');
  });
});

describe('recordPath', () => {
  it('keeps the slashes of the id in the path and encodes the rest', () => {
    expect(recordPath('ent:kev-product:apache/log4j2')).toBe(
      '/records/ent%3Akev-product%3Aapache/log4j2',
    );
    expect(recordPath('evt:kev:CVE-2021-44228', day('2026-10-13'), 'src:cisa:kev')).toBe(
      '/records/evt%3Akev%3ACVE-2021-44228?as_of=2026-10-13T00%3A00%3A00Z&source=src%3Acisa%3Akev',
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
