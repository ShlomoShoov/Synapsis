import { describe, expect, it } from 'vitest';
import { cellsLayerOf, itemOf, labelOf, pointsLayerOf } from './adapt';
import type { WireCells, WireDoc } from './contract';

const quake: WireDoc = {
  id: 'evt:usgs:ushis2739',
  record_key: 'evt:usgs:ushis2739|src:usgs:comcat',
  state: 1,
  kind: 'event',
  type: 'GEO.EARTHQUAKE',
  source: 'src:usgs:comcat',
  known_at: '1965-01-30T05:37:21Z',
  time_start: '1965-01-30T04:37:21Z',
  time_precision: 'second',
  location: [-179.67, 51.68],
  h3_r2: '82225ffffffffff',
  logical_prefixes: [],
  measures: { magnitude: [5.6], depth_km: [88] },
  entities: [],
};

describe('itemOf', () => {
  it('reads the time, the point and every measure value of a document', () => {
    const item = itemOf(quake);
    expect(item.sources).toEqual(['src:usgs:comcat']);
    expect(item.time).toEqual({ start: '1965-01-30T04:37:21Z', precision: 'second' });
    expect(item.place).toEqual({
      point: [-179.67, 51.68],
      country: undefined,
      subdivision: undefined,
    });
    expect(item.measures).toEqual([
      { name: 'magnitude', value: 5.6 },
      { name: 'depth_km', value: 88 },
    ]);
    expect(item.label).toBe('GEO.EARTHQUAKE');
    expect(item.labelFrom).toBe('generated');
  });

  it('keeps only the deepest logical paths and lists the participants by id', () => {
    const item = itemOf({
      ...quake,
      kind: 'event',
      logical_prefixes: [
        'cyber.vendor_product',
        'cyber.vendor_product/Apache',
        'cyber.vendor_product/Apache/Log4j2',
      ],
      entities: ['ent:kev-product:apache/log4j2'],
    });
    expect(item.logical).toEqual(['cyber.vendor_product/Apache/Log4j2']);
    expect(item.participants).toEqual([
      { id: 'ent:kev-product:apache/log4j2', label: 'ent:kev-product:apache/log4j2' },
    ]);
  });

  it('gives an entity no time and no place', () => {
    const { time_start: _time, location: _location, ...rest } = quake;
    const item = itemOf({ ...rest, kind: 'entity', type: 'Vendor', name: 'Apache' });
    expect(item.time).toBeUndefined();
    expect(item.place).toBeUndefined();
    expect(item.label).toBe('Apache');
  });
});

describe('labelOf', () => {
  it('prefers the name, then the title, then the start of the body, then the type and where', () => {
    expect(labelOf({ ...quake, title: 'A title' })).toEqual(['A title', 'title']);
    expect(labelOf({ ...quake, body: `${'word '.repeat(40)}end` })[0]).toMatch(/…$/);
    expect(labelOf({ ...quake, country: 'US' })).toEqual(['GEO.EARTHQUAKE · US', 'generated']);
  });
});

describe('cellsLayerOf', () => {
  it('draws each bucket as a hexagon with its records, and sums the records', () => {
    const wire: WireCells = {
      records: 29,
      sum_other_doc_count: 0,
      buckets: [{ key: '822db7fffffffff', doc_count: 29, records: { value: 29 } }],
    };
    const layer = cellsLayerOf(wire, 2);
    const [cell] = layer.cells;
    expect(layer.layer).toBe('cells');
    expect(layer.events).toBe(29);
    expect(layer.total).toBe(1);
    expect(cell?.id).toBe('822db7fffffffff');
    expect(cell?.count).toBe(29);
    expect(cell?.boundary).toHaveLength(6);
    // The centre the server's H3 library gave for this cell (rounded to 4 decimals): [34.9954, 32.3309].
    expect(cell?.center[0]).toBeCloseTo(34.9954, 3);
    expect(cell?.center[1]).toBeCloseTo(32.3309, 3);
  });
});

describe('pointsLayerOf', () => {
  it('keeps the total of all matching records next to the items', () => {
    const layer = pointsLayerOf({ total: 683, items: [quake] });
    expect(layer.layer).toBe('points');
    expect(layer.events).toBe(1);
    expect(layer.total).toBe(683);
    expect(layer.points[0]?.id).toBe('evt:usgs:ushis2739');
  });
});
