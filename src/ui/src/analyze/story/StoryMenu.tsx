/**
 * The stories menu, in the header: the proof cases of the demo, one click each. Choosing one opens its address
 * (Analyze reads the address when it opens, so the page starts again from the story).
 */
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import { addressOf, stories } from './stories';
import styles from './StoryMenu.module.css';

export function StoryMenu() {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const away = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.stopPropagation();
        setOpen(false);
      }
    };
    document.addEventListener('pointerdown', away);
    window.addEventListener('keydown', escape, true);
    return () => {
      document.removeEventListener('pointerdown', away);
      window.removeEventListener('keydown', escape, true);
    };
  }, [open]);

  return (
    <div className={styles.root} ref={rootRef}>
      <button
        type="button"
        className={styles.button}
        aria-expanded={open}
        aria-haspopup="true"
        onClick={() => setOpen((was) => !was)}
      >
        Stories
        <svg viewBox="0 0 12 12" aria-hidden="true">
          <path d="m3 4.5 3 3 3-3" />
        </svg>
      </button>
      {open && (
        <nav className={styles.menu} aria-label="Stories">
          <p className={styles.intro}>The proof cases, one click each.</p>
          <ul>
            {stories.map((story) => (
              <li key={story.id}>
                <Link className={styles.story} to={addressOf(story)} onClick={() => setOpen(false)}>
                  <span className={styles.title}>{story.title}</span>
                  <span className={styles.line}>{story.line}</span>
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </div>
  );
}
