import { useEffect, useRef } from 'react';
import { topics, type TopicId } from './topics';
import styles from './GettingStartedPage.module.css';

interface Props {
  active: TopicId;
  open: boolean;
  onToggle: () => void;
  onSelect: (id: TopicId) => void;
}

export function DocsSidebar({ active, open, onToggle, onSelect }: Props) {
  const toggle = useRef<HTMLButtonElement>(null);
  const firstLink = useRef<HTMLAnchorElement>(null);
  useEffect(() => {
    if (open && window.matchMedia('(max-width: 900px)').matches) firstLink.current?.focus();
  }, [open]);
  useEffect(() => {
    if (!open) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onToggle();
        toggle.current?.focus();
      }
    };
    window.addEventListener('keydown', closeOnEscape);
    return () => window.removeEventListener('keydown', closeOnEscape);
  }, [open, onToggle]);

  return (
    <aside className={styles.sidebar}>
      <button
        ref={toggle}
        type="button"
        className={styles.topicToggle}
        aria-controls="docs-topics"
        aria-expanded={open}
        onClick={onToggle}
      >
        Topics <span aria-hidden="true">{open ? '−' : '+'}</span>
      </button>
      <nav
        id="docs-topics"
        aria-label="Getting Started topics"
        className={styles.topicNav}
        data-open={open || undefined}
      >
        {['Foundations', 'Analyze UI', 'API guide'].map((group) => (
          <div key={group} className={styles.topicGroup}>
            <p>{group}</p>
            {topics
              .filter((topic) => topic.group === group)
              .map((topic, index) => (
                <a
                  key={topic.id}
                  ref={group === 'Foundations' && index === 0 ? firstLink : undefined}
                  href={`#${topic.id}`}
                  aria-current={active === topic.id ? 'location' : undefined}
                  onClick={(event) => {
                    event.preventDefault();
                    onSelect(topic.id);
                  }}
                >
                  {topic.label}
                </a>
              ))}
          </div>
        ))}
      </nav>
    </aside>
  );
}
