import { describe, expect, it } from 'vitest';
import { markIntroSeen, shouldShowIntro } from './intro';

const memory = () => {
  const kept = new Map<string, string>();
  return {
    getItem: (k: string) => kept.get(k) ?? null,
    setItem: (k: string, v: string) => void kept.set(k, v),
  };
};

describe('the intro', () => {
  it('shows once per visit', () => {
    const storage = memory();
    expect(shouldShowIntro(storage, false)).toBe(true);
    markIntroSeen(storage);
    expect(shouldShowIntro(storage, false)).toBe(false);
  });

  it('never shows to a viewer who asks for less motion', () => {
    expect(shouldShowIntro(memory(), true)).toBe(false);
  });
});
