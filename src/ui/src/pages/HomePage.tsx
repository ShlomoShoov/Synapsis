/**
 * The home page: the name and the promise on the left, the film on the right, and below them the areas of
 * Synapsis that are on their way. A first, basic version, to be refined with Shlomo's feedback.
 */
import { useEffect, useRef } from 'react';
import { Link } from 'react-router';
import styles from './HomePage.module.css';

/** One area of the product the client opens onto. */
interface Area {
  /** The name shown in the navigation and on its card. */
  name: string;
  /** The anchor of its card on this page. */
  slug: string;
  /** One sentence on what the area does. */
  text: string;
  /** Its page, once it exists; until then its card says it is on its way. */
  page?: string;
}

/** The areas, in navigation order. */
const areas: Area[] = [
  {
    name: 'Getting started',
    slug: 'getting-started',
    text: 'How Synapsis works, and how a new source joins it.',
  },
  {
    name: 'Observe',
    slug: 'observe',
    text: 'What the system receives, live and in the past: its flow and its numbers.',
    page: '/observe',
  },
  { name: 'Analyze', slug: 'analyze', text: 'Questions answered from the record, with sources.' },
];

/** The home film: H.264, played by every browser (see assets/media/home/). */
const film = {
  poster: '/media/home/winter-flock.poster.avif',
  h264: '/media/home/winter-flock.h264.mp4',
};

/** The home page (route /home). */
export function HomePage() {
  const pageRef = useRef<HTMLDivElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);

  // Sections marked data-reveal fade in once they scroll into view; the film stays still for visitors who
  // ask their system for reduced motion.
  useEffect(() => {
    const page = pageRef.current;
    if (!page) {
      return;
    }
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      videoRef.current?.pause();
    }
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            entry.target.setAttribute('data-visible', '');
            observer.unobserve(entry.target);
          }
        }
      },
      { threshold: 0.15 },
    );
    for (const element of page.querySelectorAll('[data-reveal]')) {
      observer.observe(element);
    }
    return () => observer.disconnect();
  }, []);

  return (
    <div ref={pageRef}>
      <header className={styles.header}>
        <a className={styles.brand} href="/home">
          Synapsis
        </a>
        <nav aria-label="Main" className={styles.nav}>
          {areas.map((area) =>
            area.page ? (
              <Link key={area.slug} to={area.page}>
                {area.name}
              </Link>
            ) : (
              <a key={area.slug} href={`#${area.slug}`}>
                {area.name}
              </a>
            ),
          )}
        </nav>
      </header>

      <main>
        <section className={styles.hero}>
          <div className={styles.heroText}>
            <h1 className={styles.title}>Synapsis</h1>
            <p className={styles.tagline}>
              <span>everything we know,</span>
              <span className={styles.beyond}>and beyond.</span>
            </p>
          </div>
          <div className={styles.film}>
            <video ref={videoRef} autoPlay muted loop playsInline poster={film.poster} aria-hidden="true">
              <source src={film.h264} type="video/mp4" />
            </video>
          </div>
          <a className={styles.scrollHint} href="#explore">
            Scroll to explore
          </a>
        </section>

        <section id="explore" className={styles.explore} data-reveal>
          <h2 className={styles.exploreTitle}>Explore</h2>
          <p className={styles.exploreIntro}>
            Synapsis keeps public records from many sources in one language: when each thing happened, and
            when it became known. On top of them it learns routines, notices what breaks them, and forecasts
            what comes next.
          </p>
          <ul className={styles.areas}>
            {areas.map((area) => (
              <li key={area.slug} id={area.slug} className={styles.area}>
                <h3>{area.name}</h3>
                <p>{area.text}</p>
                {area.page ? (
                  <Link className={styles.open} to={area.page}>
                    Open {area.name}
                  </Link>
                ) : (
                  <span className={styles.soon}>Coming soon</span>
                )}
              </li>
            ))}
          </ul>
        </section>
      </main>

      <footer className={styles.footer}>
        <span>Synapsis, a knowledge engine.</span>
        <span>Film: Yetkin Ağaç, Pexels.</span>
      </footer>
    </div>
  );
}
