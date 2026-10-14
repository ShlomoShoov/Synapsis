/**
 * The way into Analyze: an astronaut floats in space above one line. A click (or Esc, or "skip") and the astronaut
 * falls away while the Earth rises below, then the page shows. Once per visit; never with reduced motion.
 */
import { useEffect, useState } from 'react';
import { markIntroSeen, shouldShowIntro } from './intro';
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
      const timer = window.setTimeout(() => setStage('gone'), 1500);
      return () => window.clearTimeout(timer);
    }
    return undefined;
  }, [stage]);

  if (stage === 'gone') return null;
  return (
    <div className={styles.intro} data-stage={stage}>
      <div className={styles.earth} aria-hidden="true" />
      <button
        type="button"
        className={styles.land}
        onClick={go}
        aria-label="Go look for knowledge: land on the Earth"
        // oxlint-disable-next-line jsx-a11y/no-autofocus -- the intro covers the page; Enter lands at once
        autoFocus
      >
        <span className={styles.float}>
          <svg className={styles.astronaut} viewBox="0 0 120 150" aria-hidden="true">
            <rect x="34" y="62" width="52" height="56" rx="18" className={styles.pack} />
            <rect x="38" y="58" width="44" height="58" rx="20" className={styles.suit} />
            <path d="M40 74 C24 78 18 92 20 104" className={styles.limb} />
            <path d="M80 74 C96 70 104 58 104 46" className={styles.limb} />
            <path d="M50 112 C48 126 44 136 40 142" className={styles.limb} />
            <path d="M70 112 C74 126 80 134 88 138" className={styles.limb} />
            <circle cx="60" cy="40" r="26" className={styles.suit} />
            <rect x="42" y="28" width="36" height="24" rx="12" className={styles.visor} />
            <path d="M48 34 q6 -4 12 -2" className={styles.glint} />
            <rect x="52" y="78" width="16" height="10" rx="3" className={styles.panel} />
          </svg>
        </span>
        <span className={styles.words}>
          <span className={styles.line}>Go look for knowledge</span>
          <span className={styles.hint}>Click anywhere to land</span>
        </span>
      </button>
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
