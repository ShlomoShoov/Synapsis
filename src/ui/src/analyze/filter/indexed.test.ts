import { describe, expect, it } from 'vitest';
import { indexedOf } from './indexed';

describe('indexedOf', () => {
  it('says how far the indexer is, and that it is still indexing', () => {
    expect(indexedOf(1_156_944, 3_071_181)).toEqual({
      text: 'indexed ~1.2M of 3.1M',
      indexing: true,
    });
  });

  it('is quiet when every record is searchable', () => {
    expect(indexedOf(28_925, 28_925)).toEqual({ text: 'all 28.9K indexed', indexing: false });
  });

  it('says nothing without the API count', () => {
    expect(indexedOf(10, undefined)).toBeNull();
  });
});
