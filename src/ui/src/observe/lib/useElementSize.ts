/**
 * The size of an element, kept up to date as it changes (a ResizeObserver).
 */
import { useLayoutEffect, useState } from 'react';

/** The content size of `element` in CSS pixels; zero until it is measured. */
export function useElementSize(element: HTMLElement | null): { width: number; height: number } {
  const [size, setSize] = useState({ width: 0, height: 0 });
  useLayoutEffect(() => {
    if (!element) {
      return;
    }
    const observer = new ResizeObserver(([entry]) => {
      if (entry) {
        const { width, height } = entry.contentRect;
        setSize((previous) =>
          previous.width === width && previous.height === height ? previous : { width, height },
        );
      }
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [element]);
  return size;
}
