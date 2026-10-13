/**
 * Whether the viewer asked their system for less motion, kept up to date if they change it. Analyze then jumps where
 * it would glide: columns take their new height at once, and the camera does not fly.
 */
import { useSyncExternalStore } from 'react';

const query = '(prefers-reduced-motion: reduce)';

function subscribe(listener: () => void): () => void {
  const media = window.matchMedia(query);
  media.addEventListener('change', listener);
  return () => media.removeEventListener('change', listener);
}

/** True when the viewer prefers reduced motion. */
export function useReducedMotion(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => window.matchMedia(query).matches,
    () => false,
  );
}
