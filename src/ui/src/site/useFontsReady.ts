/**
 * Whether the fonts the page has asked for are ready, so an opening animation never starts in a fallback font and
 * then jumps to the real one. Stops waiting after `timeout` milliseconds: a slow or missing file must not hold the
 * page back (it then simply opens in the fallback).
 */
import { useEffect, useState } from 'react';

export function useFontsReady(timeout = 1500): boolean {
  // Where the browser cannot tell (tests, very old browsers), there is nothing to wait for.
  const [ready, setReady] = useState(() => typeof document === 'undefined' || !document.fonts);

  useEffect(() => {
    if (ready) {
      return;
    }
    let live = true;
    const done = () => {
      if (live) {
        setReady(true);
      }
    };
    const timer = window.setTimeout(done, timeout);
    // One frame first, so the page has been laid out and its fonts requested before asking whether they arrived.
    const frame = requestAnimationFrame(() => void document.fonts.ready.then(done));
    return () => {
      live = false;
      window.clearTimeout(timer);
      cancelAnimationFrame(frame);
    };
  }, [ready, timeout]);

  return ready;
}
