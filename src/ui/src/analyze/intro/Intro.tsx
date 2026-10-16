/**
 * The way into Analyze: an astronaut floats in space above one line, the Earth's limb below, the sun off to the
 * right. A click (or Esc, or "skip") and the astronaut lets go: a small push back, then the fall, tumbling, shrinking
 * as it drops away towards the Earth, glowing as it meets the air, while the view follows it down and the Earth rises
 * to fill it; the atmosphere's light, then the page. Once per visit; never with reduced motion.
 *
 * The astronaut is a 3D one (AstronautScene.ts), fetched early (from Home). Until its first frame only the stars show,
 * and a click does nothing (Esc and "skip" go straight to the page). The SVG drawing stands in only where WebGL is
 * missing, or when the 3D one is not ready within 10 seconds.
 */
import { useEffect, useRef, useState } from 'react';
import { Astronaut } from './Astronaut';
import type { AstronautScene } from './AstronautScene';
import { fallMs, loadScene, markIntroSeen, shouldShowIntro } from './intro';
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
  const skip = () => {
    markIntroSeen(sessionStore());
    setStage('gone');
  };
  const canvas = useRef<HTMLCanvasElement>(null);
  const scene = useRef<AstronautScene | null>(null);
  const floating = useRef(stage === 'float');
  // Which astronaut is ready to be seen: none yet (only the stars), the 3D one, or the drawing standing in.
  const [ready, setReady] = useState<'3d' | 'svg' | null>(null);
  const threeD = ready === '3d';
  const shown = stage !== 'gone';

  // The 3D astronaut: loaded while the drawing floats, shown from its first frame, let go with the page.
  useEffect(() => {
    if (!shown) return undefined;
    let cancelled = false;
    let created: AstronautScene | null = null;
    const late = window.setTimeout(() => setReady((now) => now ?? 'svg'), 10_000);
    loadScene()
      .then(({ createAstronautScene }) => {
        if (cancelled || !canvas.current || !floating.current) return;
        created = createAstronautScene(canvas.current, () => {
          if (!cancelled && floating.current) setReady((now) => (now === 'svg' ? now : '3d'));
        });
        scene.current = created;
      })
      .catch(() => {
        // No WebGL (or the code could not load): the drawing stands in.
        if (!cancelled) setReady('svg');
      });
    return () => {
      cancelled = true;
      window.clearTimeout(late);
      created?.dispose();
      scene.current = null;
    };
  }, [shown]);

  useEffect(() => {
    floating.current = stage === 'float';
    if (stage === 'fall' && threeD) scene.current?.fall();
  }, [stage, threeD]);

  useEffect(() => {
    if (stage === 'float') {
      const key = (event: KeyboardEvent) => {
        if (event.key === 'Escape') {
          event.preventDefault();
          event.stopPropagation();
          if (ready) go();
          else skip();
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
  }, [stage, ready]);

  if (stage === 'gone') return null;
  return (
    <div
      className={styles.intro}
      data-stage={stage}
      data-three={threeD || undefined}
      data-ready={ready ?? undefined}
    >
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
      <canvas ref={canvas} className={styles.scene} aria-hidden="true" />
      <div className={styles.air} aria-hidden="true" />
      <button
        type="button"
        className={styles.land}
        onClick={() => {
          if (ready) go();
        }}
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
          skip();
        }}
      >
        Skip
      </button>
    </div>
  );
}
