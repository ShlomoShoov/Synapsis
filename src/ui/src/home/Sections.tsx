/**
 * The explanations of the home page (sections.ts), in order: each a title, a text and its image or video.
 * Infrastructure with the plainest styling; the design comes later. Shows nothing while the list is empty.
 */
import { sections as allSections, type Section, type SectionMedia } from './sections';
import styles from './Sections.module.css';

interface SectionsProps {
  /** The explanations to show (by default, the list in sections.ts). */
  items?: readonly Section[];
}

/** The list of explanations. */
export function Sections({ items = allSections }: SectionsProps) {
  if (items.length === 0) {
    return null;
  }
  return (
    <div className={styles.sections}>
      {items.map((item) => (
        <section key={item.title} className={styles.section}>
          <h2>{item.title}</h2>
          <p>{item.text}</p>
          <Media media={item.media} />
        </section>
      ))}
    </div>
  );
}

/** An explanation's picture, or its film with the browser's own controls. */
function Media({ media }: { media: SectionMedia }) {
  if (media.kind === 'image') {
    return <img className={styles.media} src={media.src} alt={media.alt} loading="lazy" />;
  }
  return (
    <video
      className={styles.media}
      src={media.src}
      aria-label={media.label}
      controls
      muted
      playsInline
      preload="metadata"
    />
  );
}
