import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useNow } from './clock';

describe('useNow', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(1_000_000);
  });
  afterEach(() => vi.useRealTimers());

  // Each test uses its own resolution: clocks are shared by resolution across the whole page.
  it('holds one moment between ticks, however often the view redraws', () => {
    const { result, rerender, unmount } = renderHook(() => useNow(5_000));
    const first = result.current;
    for (let i = 0; i < 20; i++) {
      rerender();
    }
    expect(result.current).toBe(first);
    unmount();
  });

  it('advances on its interval', () => {
    const { result, unmount } = renderHook(() => useNow(7_000));
    const first = result.current;
    act(() => {
      vi.advanceTimersByTime(7_000);
    });
    expect(result.current).toBe(first + 7_000);
    unmount();
  });
});
