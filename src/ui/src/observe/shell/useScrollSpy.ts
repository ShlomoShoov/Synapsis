/**
 * Follows the reader down the page: which section, and which anchor inside it, is under the reading line (a third of
 * the way down the window). Also finds the anchors themselves (see sections.ts), and finds them again when the page
 * changes (a panel shown, hidden or moved).
 */
import { useEffect, useState } from 'react';
import type { SectionId } from './sections';

/** An anchor inside a section. */
export interface NavAnchor {
  id: string;
  label: string;
  section: SectionId;
}

/** Where the reader is. */
export interface ScrollPosition {
  section: SectionId | null;
  anchor: string | null;
  anchors: NavAnchor[];
}

/** The reading line, as a share of the window's height. */
const readingLine = 0.32;

function findAnchors(root: ParentNode): NavAnchor[] {
  return [...root.querySelectorAll<HTMLElement>('[data-nav-anchor][id]')].map((element) => ({
    id: element.id,
    label: element.dataset.navAnchor ?? element.id,
    section: (element.closest<HTMLElement>('[data-nav-section]')?.dataset.navSection ??
      'flow') as SectionId,
  }));
}

function sameAnchors(a: NavAnchor[], b: NavAnchor[]): boolean {
  return (
    a.length === b.length &&
    a.every((anchor, i) => anchor.id === b[i]?.id && anchor.label === b[i]?.label)
  );
}

/** The reader's position on the page inside `root` (the main element). */
export function useScrollSpy(root: HTMLElement | null): ScrollPosition {
  const [position, setPosition] = useState<ScrollPosition>({
    section: null,
    anchor: null,
    anchors: [],
  });

  useEffect(() => {
    if (!root) {
      return;
    }
    let anchors = findAnchors(root);
    let frame = 0;

    const measure = () => {
      frame = 0;
      const line = window.innerHeight * readingLine;
      let section: SectionId | null = null;
      for (const element of root.querySelectorAll<HTMLElement>('[data-nav-section]')) {
        if (element.getBoundingClientRect().top <= line) {
          section = element.dataset.navSection as SectionId;
        }
      }
      let anchor: string | null = null;
      for (const item of anchors) {
        const element = document.getElementById(item.id);
        if (item.section === section && element && element.getBoundingClientRect().top <= line) {
          anchor = item.id;
        }
      }
      // At the very bottom the last section is current, even if it is too short to reach the line.
      if (window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 4) {
        const last = [...root.querySelectorAll<HTMLElement>('[data-nav-section]')].pop();
        section = (last?.dataset.navSection as SectionId | undefined) ?? section;
      }
      setPosition((previous) =>
        previous.section === section &&
        previous.anchor === anchor &&
        sameAnchors(previous.anchors, anchors)
          ? previous
          : { section, anchor, anchors },
      );
    };
    const schedule = () => {
      if (!frame) {
        frame = requestAnimationFrame(measure);
      }
    };
    const observer = new MutationObserver(() => {
      const found = findAnchors(root);
      if (!sameAnchors(found, anchors)) {
        anchors = found;
        schedule();
      }
    });
    observer.observe(root, {
      subtree: true,
      childList: true,
      attributes: true,
      attributeFilter: ['data-nav-anchor', 'id'],
    });
    window.addEventListener('scroll', schedule, { passive: true });
    window.addEventListener('resize', schedule);
    schedule();
    return () => {
      observer.disconnect();
      window.removeEventListener('scroll', schedule);
      window.removeEventListener('resize', schedule);
      cancelAnimationFrame(frame);
    };
  }, [root]);

  return position;
}

/** Scrolls smoothly to an element by id and writes it in the address (without a jump or a new history entry). */
export function scrollToId(id: string): void {
  const element = document.getElementById(id);
  if (!element) {
    return;
  }
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  element.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' });
  const url = new URL(window.location.href);
  url.hash = id;
  window.history.replaceState(window.history.state, '', url);
}
