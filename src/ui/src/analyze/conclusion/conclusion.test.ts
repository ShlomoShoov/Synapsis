import { describe, expect, it } from 'vitest';
import { itemOf } from '../api/adapt';
import type { WireDoc } from '../api/contract';
import { marksOfLayer } from '../map/marks';
import {
  badgeOf,
  conclusionTypeOf,
  forecastPartsOf,
  keyNumbers,
  numbersOfItem,
  numbersOfValue,
  saidAbout,
  scorePairs,
} from './conclusion';

const forecast: WireDoc = {
  id: 'con:engine:forecast:84bcf59ffffffff:1989-02',
  record_key: 'con:engine:forecast:84bcf59ffffffff:1989-02|src:synapsis:engine',
  state: 1,
  kind: 'conclusion',
  type: 'forecast',
  source: 'src:synapsis:engine',
  known_at: '1989-02-01T00:00:00Z',
  title: 'Feb 1989 in this cell: 6.4 conflict events expected.',
  time_start: '1989-02-01T00:00:00Z',
  location: [30.64, -29.81],
  measures: {
    expected: [6.4],
    routine: [3.1],
    self: [2.2],
    neighbours: [1.1],
    lower80: [2],
    upper80: [11],
    rank: [3],
    of: [1257],
    confidence: [70],
  },
};

describe('conclusions', () => {
  it('give each type its own glyph, and unknown types a fallback', () => {
    const shapes = [
      'spark',
      'forecast',
      'joint_anomaly',
      'similar_pattern',
      'calibrated_estimate',
    ].map((type) => conclusionTypeOf(type).shape);
    expect(new Set(shapes).size).toBe(shapes.length);
    expect(conclusionTypeOf('new_kind')).toMatchObject({ name: 'New kind', shape: 'octagon' });
    expect(badgeOf('joint_anomaly')).toBe('Conclusion · Joint anomaly');
  });

  it('read their numbers from a document or a statement', () => {
    const numbers = numbersOfItem(itemOf(forecast));
    expect(numbers.expected).toBe(6.4);
    expect(keyNumbers('forecast', numbers)).toEqual([
      'expected 6.4 (80%: 2–11)',
      'rank 3 of 1,257',
    ]);
    expect(numbersOfValue(0.31)).toEqual({ value: 0.31 });
    expect(numbersOfValue({ poll: 48.6, note: 'x' })).toEqual({ poll: 48.6 });
  });

  it('split a forecast into its parts and score the model against the routine', () => {
    expect(forecastPartsOf(numbersOfItem(itemOf(forecast)))).toMatchObject({
      routine: 3.1,
      self: 2.2,
      neighbours: 1.1,
      expected: 6.4,
    });
    expect(
      scorePairs({
        events: 12,
        captured_model: 7,
        captured_routine: 4,
        auc_model: 0.81,
        auc_routine: 0.74,
      }),
    ).toEqual([
      { label: 'captured', model: 7, routine: 4, of: 12 },
      { label: 'AUC', model: 0.81, routine: 0.74, of: undefined },
    ]);
  });

  it('say when they were said, about what period', () => {
    const said = saidAbout('1989-01-25T00:00:00Z', {
      start: '1989-02-01T00:00:00Z',
      precision: 'month',
    });
    expect(said).toMatchObject({
      about: 'Feb 1989',
      before: 'said 7 days before the period began',
    });
  });

  it('become marks on the map, one per conclusion with a point', () => {
    const marks = marksOfLayer({
      layer: 'points',
      events: 1,
      total: 1,
      omitted: [],
      points: [itemOf(forecast), { ...itemOf(forecast), id: 'x', place: undefined }],
      cells: [],
    });
    expect(marks.ids).toEqual([forecast.id]);
    expect(marks.features.features[0]!.properties.icon).toBe('con:diamond:#5ef2c8');
  });
});
