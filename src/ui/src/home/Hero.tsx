/**
 * The first screen of the home page: Shlomo's image as he made it, filling the window, with the name just above the
 * line where the orange light meets the blue and the promise centred under it. The words fade in once the fonts are ready (so they never jump from one face to another). As the page
 * scrolls, the image dissolves into the paper and moves slower than the page, and the words fade away (--scrolled,
 * set by the page, see useHeroScroll). A thin line at the foot hints that the page goes on.
 */
import { horizonImage } from '@/site/Horizon';
import { useFontsReady } from '@/site/useFontsReady';
import styles from './Hero.module.css';

/** The home page's first screen. */
export function Hero() {
  const ready = useFontsReady();

  return (
    <section className={styles.hero} aria-labelledby="home-title" data-ready={ready || undefined}>
      <div className={styles.backdrop}>
        <div className={styles.depth}>
          <img className={styles.image} src={horizonImage} alt="" fetchPriority="high" />
        </div>
      </div>
      <div className={styles.content}>
        <div className={styles.words}>
          <h1 id="home-title" className={styles.title}>
            Synapsis
          </h1>
          <p className={styles.tagline}>
            <span className={styles.know}>everything we know,</span>
            <span className={styles.beyond}>and beyond.</span>
          </p>
        </div>
      </div>
      <div className={styles.cue} aria-hidden="true">
        <span className={styles.line} />
      </div>
    </section>
  );
}
