import { useCallback, useEffect, useState } from 'react';
import { Header } from '../site/Header';
import { Horizon } from '../site/Horizon';
import { DocsSidebar } from './DocsSidebar';
import { DocsArticle } from './DocsArticle';
import { topicFromHash, topics, type TopicId } from './topics';
import styles from './GettingStartedPage.module.css';

export function GettingStartedPage() {
  const [active, setActive] = useState<TopicId>(() => topicFromHash(window.location.hash));
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [onPaper, setOnPaper] = useState(false);

  const goTo = useCallback((id: TopicId, addHistory = true) => {
    setActive(id);
    setDrawerOpen(false);
    if (addHistory && window.location.hash !== `#${id}`) window.location.hash = id;
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    document.getElementById(id)?.scrollIntoView({
      behavior: reducedMotion ? 'instant' : 'smooth',
      block: 'start',
    });
    if (addHistory) document.getElementById(`${id}-title`)?.focus({ preventScroll: true });
  }, []);

  useEffect(() => {
    const onHashChange = () => goTo(topicFromHash(window.location.hash), false);
    window.addEventListener('hashchange', onHashChange);
    const frame = window.requestAnimationFrame(() => {
      if (window.location.hash) onHashChange();
    });
    return () => {
      window.removeEventListener('hashchange', onHashChange);
      window.cancelAnimationFrame(frame);
    };
  }, [goTo]);

  useEffect(() => {
    let frame = 0;
    const update = () => {
      window.cancelAnimationFrame(frame);
      frame = window.requestAnimationFrame(() => {
        setOnPaper(window.scrollY > 220);
        const current = [...topics].reverse().find((topic) => {
          const element = document.getElementById(topic.id);
          return element && element.getBoundingClientRect().top <= 160;
        });
        if (current) setActive(current.id);
      });
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
    return () => {
      window.removeEventListener('scroll', update);
      window.cancelAnimationFrame(frame);
    };
  }, []);

  return (
    <div className={styles.page}>
      <a
        className={styles.skip}
        href="#why-synapsis"
        onClick={(event) => {
          event.preventDefault();
          goTo('why-synapsis');
          document.getElementById('docs-main')?.focus({ preventScroll: true });
        }}
      >
        Skip to documentation
      </a>
      <Header onPaper={onPaper} />
      <section className={styles.hero} aria-labelledby="docs-title">
        <Horizon />
        <div className={styles.heroText}>
          <p className={styles.heroLabel}>The Synapsis field guide</p>
          <h1 id="docs-title">Getting started</h1>
          <p className={styles.heroSubtitle}>
            From fragmented data to unified understanding — how Synapsis structures and connects
            intelligence across dimensions.
          </p>
        </div>
      </section>
      <div className={styles.paper}>
        <div className={styles.layout}>
          <DocsSidebar
            active={active}
            open={drawerOpen}
            onToggle={() => setDrawerOpen((value) => !value)}
            onSelect={goTo}
          />
          <main id="docs-main" tabIndex={-1} className={styles.main}>
            <DocsArticle onSelect={goTo} />
          </main>
        </div>
      </div>
    </div>
  );
}
