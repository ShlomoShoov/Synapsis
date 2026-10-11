/**
 * Whether an element is on screen (an IntersectionObserver), so work that only shows there can stop when it is not.
 */
import { useEffect, useState } from 'react';

/** True while any part of `element` is inside the viewport, widened by `margin` (a CSS margin, "200px"). */
export function useInView(element: Element | null, margin = '0px'): boolean {
  const [inView, setInView] = useState(false);
  useEffect(() => {
    if (!element) {
      return;
    }
    const observer = new IntersectionObserver(([entry]) => setInView(!!entry?.isIntersecting), {
      rootMargin: margin,
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [element, margin]);
  return inView;
}
