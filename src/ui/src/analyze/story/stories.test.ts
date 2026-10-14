import { describe, expect, it } from 'vitest';
import { filterFromAddress } from '../filter/viewFilter';
import { addressOf, stories } from './stories';

describe('stories', () => {
  it('each has a window whose start comes before its end', () => {
    for (const story of stories) expect(story.from < story.to).toBe(true);
  });

  it('the address carries the window, the camera and the filter', () => {
    const tohoku = stories.find((story) => story.id === 'tohoku')!;
    const params = new URLSearchParams(addressOf(tohoku).split('?')[1]);
    expect(params.get('from')).toBe('2011-03-10');
    expect(params.get('to')).toBe('2011-03-12');
    expect(params.get('view')).toBe('142.4,38.3,4.3');
    expect(filterFromAddress(params).sources).toEqual(['src:usgs:comcat', 'src:synapsis:engine']);
  });

  it('SVB opens the bank in the graph', () => {
    const svb = stories.find((story) => story.id === 'svb')!;
    const params = new URLSearchParams(addressOf(svb).split('?')[1]);
    expect(params.get('object')).toBe('ent:fdic:cert-24735');
    expect(params.get('dimension')).toBe('graph');
  });

  it('reads the kind from the address, and ignores a kind it does not know', () => {
    expect(filterFromAddress(new URLSearchParams('kind=conclusion')).kind).toBe('conclusion');
    expect(filterFromAddress(new URLSearchParams('kind=nonsense')).kind).toBeNull();
    expect(filterFromAddress(new URLSearchParams('source=a,b&source=a')).sources).toEqual([
      'a',
      'b',
    ]);
  });
});
