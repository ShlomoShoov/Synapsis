/**
 * The way into Analyze: an astronaut floats in space above one line, the Earth's limb below, the sun off to the
 * right. A click (or Esc, or "skip") and the astronaut lets go: a small push back, then the fall, tumbling, shrinking
 * as it drops away towards the Earth, glowing as it meets the air, while the view follows it down and the Earth rises
 * to fill it; the atmosphere's light, then the page. Once per visit; never with reduced motion.
 */
import { useEffect, useState } from 'react';
import { Astronaut } from './Astronaut';
import { fallMs, markIntroSeen, shouldShowIntro } from './intro';
import styles from './Intro.module.css';

const sessionStore = () => {
  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
};

export function Intro() {
  const [stage, setStage] = useState<'float' | 'fall' | 'gone'>(() =>
    shouldShowIntro(sessionStore(), window.matchMedia('(prefers-reduced-motion: reduce)').matches)
      ? 'float'
      : 'gone',
  );
  const go = () => {
    markIntroSeen(sessionStore());
    setStage((now) => (now === 'float' ? 'fall' : now));
  };

  useEffect(() => {
    if (stage === 'float') {
      const key = (event: KeyboardEvent) => {
        if (event.key === 'Escape') {
          event.preventDefault();
          event.stopPropagation();
          go();
        }
      };
      window.addEventListener('keydown', key, true);
      return () => window.removeEventListener('keydown', key, true);
    }
    if (stage === 'fall') {
      const timer = window.setTimeout(() => setStage('gone'), fallMs);
      return () => window.clearTimeout(timer);
    }
    return undefined;
  }, [stage]);

  if (stage === 'gone') return null;
  return (
    <div className={styles.intro} data-stage={stage}>
      <div className={styles.sky} aria-hidden="true">
        <div className={styles.starsFar} />
        <div className={styles.starsNear} />
      </div>
      <div className={styles.sun} aria-hidden="true" />
      <div className={styles.earth} aria-hidden="true">
        <div className={styles.continents} />
        <div className={styles.clouds} />
        <div className={styles.night} />
      </div>
      <div className={styles.air} aria-hidden="true" />
      <button
        type="button"
        className={styles.land}
        onClick={go}
        aria-label="Go look for knowledge: land on the Earth"
        // oxlint-disable-next-line jsx-a11y/no-autofocus -- the intro covers the page; Enter lands at once
        autoFocus
      >
        <span className={styles.float}>
          <span className={styles.sway}>
            <span className={styles.fall}>
              <span className={styles.heat} aria-hidden="true" />
              <span className={styles.tumble}>
                <Astronaut />
              </span>
            </span>
          </span>
        </span>
        <span className={styles.words}>
          <span className={styles.line}>Go look for knowledge</span>
          <span className={styles.hint}>Click anywhere to land</span>
        </span>
      </button>
      <div className={styles.flash} aria-hidden="true" />
      <button
        type="button"
        className={styles.skip}
        onClick={(event) => {
          event.stopPropagation();
          markIntroSeen(sessionStore());
          setStage('gone');
        }}
      >
        Skip
      </button>
    </div>
  );
}
