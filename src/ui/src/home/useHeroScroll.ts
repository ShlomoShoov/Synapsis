/**
 * Follows the page as it scrolls past its first screen.
 *
 * Writes how far as the CSS variable --scrolled on the page (0 at the top, 1 once the paper below the first screen
 * reaches the top of the window); the first screen reads it to fade its words. Returns whether the paper is under
 * the header (the header then turns to paper). Reads the page once per frame at most.
 */
import { useEffect, useState, type RefObject } from 'react';

/** Whether the paper is under the header. */
export function useHeroScroll(
  page: RefObject<HTMLElement | null>,
  paper: RefObject<HTMLElement | null>,
): boolean {
  const [onPaper, setOnPaper] = useState(false);

  useEffect(() => {
    const pageElement = page.current;
    const paperElement = paper.current;
    if (!pageElement || !paperElement) {
      return;
    }
    const header =
      parseFloat(getComputedStyle(pageElement).getPropertyValue('--header-height')) || 0;
    let frame = 0;

    const measure = () => {
      frame = 0;
      const top = paperElement.getBoundingClientRect().top;
      const start = top + window.scrollY; // where the paper starts on the page
      const scrolled = start > 0 ? Math.min(1, Math.max(0, window.scrollY / start)) : 1;
      pageElement.style.setProperty('--scrolled', scrolled.toFixed(4));
      setOnPaper(top <= header);
    };
    const schedule = () => {
      if (!frame) {
        frame = requestAnimationFrame(measure);
      }
    };

    measure();
    window.addEventListener('scroll', schedule, { passive: true });
    window.addEventListener('resize', schedule);
    return () => {
      window.removeEventListener('scroll', schedule);
      window.removeEventListener('resize', schedule);
      cancelAnimationFrame(frame);
    };
  }, [page, paper]);

  return onPaper;
}
