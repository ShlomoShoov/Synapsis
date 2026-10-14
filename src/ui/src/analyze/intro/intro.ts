/** The astronaut's intro: shown once per visit (this tab's session), and never to a viewer who asks for less motion. */

const seenKey = 'synapsis.analyze.intro-seen';

/** How long the fall lasts, from the click to the page (Intro.module.css times its steps within it). */
export const fallMs = 2600;

export function shouldShowIntro(
  storage: Pick<Storage, 'getItem'> | null,
  reducedMotion: boolean,
): boolean {
  if (reducedMotion) return false;
  try {
    return storage?.getItem(seenKey) !== '1';
  } catch {
    return false;
  }
}

export function markIntroSeen(storage: Pick<Storage, 'setItem'> | null) {
  try {
    storage?.setItem(seenKey, '1');
  } catch {
    // Not kept: the intro may show again in this visit.
  }
}
