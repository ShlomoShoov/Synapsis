/**
 * The home page (route /home): Shlomo's image with the name and the promise (Hero), then the paper below, where
 * a centred column holds what Synapsis is and where to go (Overview), the explanations (Sections) and the footer.
 * The header follows the scroll: glass over the dawn, paper over paper, and the small name joins it once the first
 * screen has gone (useHeroScroll).
 */
import { useRef } from 'react';
import { Header } from '@/site/Header';
import { Footer } from './Footer';
import { Hero } from './Hero';
import { Overview } from './Overview';
import { Sections } from './Sections';
import { useHeroScroll } from './useHeroScroll';
import styles from './HomePage.module.css';

/** The home page. */
export function HomePage() {
  const pageRef = useRef<HTMLDivElement>(null);
  const paperRef = useRef<HTMLDivElement>(null);
  const onPaper = useHeroScroll(pageRef, paperRef);

  return (
    <div ref={pageRef}>
      <Header onPaper={onPaper} wordmark={onPaper} late />
      <main>
        <Hero />
        <div ref={paperRef} className={styles.paper}>
          <div className={styles.column}>
            <Overview />
            <Sections />
            <Footer />
          </div>
        </div>
      </main>
    </div>
  );
}
