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

/** The 3D astronaut's code (three.js), fetched once: asked for early (from Home), so it is ready when the intro shows. */
let scene: Promise<typeof import('./AstronautScene')> | null = null;
export const loadScene = () => (scene ??= import('./AstronautScene'));

/** Starts fetching the 3D astronaut when the intro will show in this visit. */
export function preloadIntro() {
  let storage: Storage | null = null;
  try {
    storage = window.sessionStorage;
  } catch {
    // No storage: the intro does not show.
  }
  if (shouldShowIntro(storage, window.matchMedia('(prefers-reduced-motion: reduce)').matches)) {
    loadScene().catch(() => undefined);
  }
}

export function markIntroSeen(storage: Pick<Storage, 'setItem'> | null) {
  try {
    storage?.setItem(seenKey, '1');
  } catch {
    // Not kept: the intro may show again in this visit.
  }
}
