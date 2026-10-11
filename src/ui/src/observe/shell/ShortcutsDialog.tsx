/**
 * The list of keyboard shortcuts, in a native modal dialog (focus stays inside, Escape closes it).
 */
import { useEffect, useRef } from 'react';
import { Kbd } from '../ui/controls';
import { Icon } from '../ui/Icon';
import { useLightDismiss } from '../ui/useLightDismiss';
import styles from './ShortcutsDialog.module.css';

/** Every shortcut, grouped by where it acts. */
const groups: { title: string; items: { keys: string[]; text: string }[] }[] = [
  {
    title: 'Anywhere',
    items: [
      { keys: ['1', '2', '3'], text: 'Go to the live flow, the metrics or the schema map' },
      { keys: ['['], text: 'Fold or unfold the sidebar' },
      { keys: ['?'], text: 'Show this list' },
    ],
  },
  {
    title: 'Live flow',
    items: [
      { keys: ['Space'], text: 'Play or pause the replay' },
      { keys: ['←', '→'], text: 'Step back or forward in time' },
      { keys: ['L'], text: 'Return to live' },
    ],
  },
  {
    title: 'Schema map',
    items: [
      { keys: ['/'], text: 'Search the packages' },
      { keys: ['0'], text: 'Fit the map to the screen' },
    ],
  },
];

/** The shortcuts dialog; open while `open` is true. */
export function ShortcutsDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useLightDismiss(dialog, onClose);
  useEffect(() => {
    const element = dialog.current;
    if (!element) {
      return;
    }
    if (open && !element.open) {
      element.showModal();
    } else if (!open && element.open) {
      element.close();
    }
  }, [open]);

  return (
    <dialog
      ref={dialog}
      className={styles.dialog}
      aria-labelledby="shortcuts-title"
      onClose={onClose}
    >
      <header className={styles.header}>
        <h2 id="shortcuts-title" className={styles.title}>
          Keyboard shortcuts
        </h2>
        <button type="button" className={styles.close} onClick={onClose} aria-label="Close">
          <Icon name="close" />
        </button>
      </header>
      {groups.map((group) => (
        <section key={group.title} className={styles.group}>
          <h3 className={styles.groupTitle}>{group.title}</h3>
          <dl className={styles.list}>
            {group.items.map((item) => (
              <div key={item.text} className={styles.item}>
                <dt className={styles.keys}>
                  {item.keys.map((key) => (
                    <Kbd key={key}>{key}</Kbd>
                  ))}
                </dt>
                <dd className={styles.text}>{item.text}</dd>
              </div>
            ))}
          </dl>
        </section>
      ))}
    </dialog>
  );
}
