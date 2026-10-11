/**
 * The head of a section: its number and title in the brand serif, one line on what it shows, and room on the right
 * for its own controls.
 */
import type { ReactNode } from 'react';
import type { SectionInfo } from './sections';
import styles from './SectionHeader.module.css';

/** The head of a section. Its title's id is "<section id>-title", for the section's aria-labelledby. */
export function SectionHeader({
  section,
  children,
  aside,
}: {
  section: SectionInfo;
  /** Extra lines under the summary. */
  children?: ReactNode;
  /** Controls on the right. */
  aside?: ReactNode;
}) {
  return (
    <header className={styles.header}>
      <div className={styles.text}>
        <p className={styles.eyebrow}>
          <span>{section.number}</span>
          <span className={styles.rule} aria-hidden="true" />
          <span>Observe</span>
        </p>
        <h2 id={`${section.id}-title`} className={styles.title}>
          {section.title}
        </h2>
        <p className={styles.summary}>{section.summary}</p>
        {children}
      </div>
      {aside ? <div className={styles.aside}>{aside}</div> : null}
    </header>
  );
}
