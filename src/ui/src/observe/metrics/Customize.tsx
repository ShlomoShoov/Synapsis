/**
 * The customize sheet: the viewer arranges the dashboards. Drag a panel by its grip to move it (or focus the grip and
 * use the arrow keys, or the arrows beside it), show or hide it, give it the whole row or half of it. The arrangement
 * is remembered in this browser; "Reset" brings back the default.
 *
 * Dragging uses pointer events on the grip (a button), not HTML drag and drop: it works the same with a mouse, a pen
 * or a finger, and every handler sits on a control the keyboard can reach.
 */
import { useEffect, useRef, useState } from 'react';
import controls from '../ui/controls.module.css';
import { Icon } from '../ui/Icon';
import { useLightDismiss } from '../ui/useLightDismiss';
import { panels, sizeOf, type Layout, type PanelId } from './layout';
import styles from './MetricsSection.module.css';

/** The customize sheet; open while `open` is true. */
export function Customize({
  open,
  layout,
  onChange,
  onReset,
  onClose,
}: {
  open: boolean;
  layout: Layout;
  onChange: (layout: Layout) => void;
  onReset: () => void;
  onClose: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  useLightDismiss(dialog, onClose);
  /** The panel being dragged, and where it would land: its index among the others. */
  const [drag, setDrag] = useState<{ id: PanelId; index: number } | null>(null);
  const rows = useRef(new Map<PanelId, HTMLLIElement>());

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

  /** Where a panel dragged to the height `y` would land: its index among the others. */
  const landingAt = (id: PanelId, y: number): number =>
    layout.order
      .filter((item) => item !== id)
      .filter((item) => {
        const box = rows.current.get(item)?.getBoundingClientRect();
        return box !== undefined && box.top + box.height / 2 < y;
      }).length;

  /** Moves a panel to an index among the others. */
  const moveTo = (id: PanelId, index: number) => {
    const order = layout.order.filter((item) => item !== id);
    order.splice(index, 0, id);
    if (order.some((item, i) => item !== layout.order[i])) {
      onChange({ ...layout, order });
    }
  };

  /** Moves a panel one place up or down. */
  const step = (id: PanelId, delta: -1 | 1) => {
    const order = [...layout.order];
    const from = order.indexOf(id);
    const to = from + delta;
    if (to < 0 || to >= order.length) {
      return;
    }
    order.splice(from, 1);
    order.splice(to, 0, id);
    onChange({ ...layout, order });
  };

  return (
    <dialog
      ref={dialog}
      className={styles.customize}
      aria-labelledby="customize-title"
      onClose={onClose}
    >
      <div className={styles.customizeBody}>
        <header className={styles.customizeHeader}>
          <h2 id="customize-title" className={styles.customizeTitle}>
            Arrange the dashboards
          </h2>
          <button
            type="button"
            className={controls.iconButton}
            onClick={onClose}
            aria-label="Close"
          >
            <Icon name="close" />
          </button>
        </header>
        <p className={styles.customizeIntro}>
          Drag a panel to move it, choose what is shown and how wide. Remembered in this browser.
        </p>
        <ol className={styles.customizeList}>
          {layout.order.map((id, index) => {
            // The line that shows where a dragged panel would land: above the panel it would precede, or under the
            // last one.
            const moving = drag && drag.index !== layout.order.indexOf(drag.id) ? drag : null;
            const others = moving ? layout.order.filter((item) => item !== moving.id) : [];
            const landing = moving ? others[moving.index] : undefined;
            const landingAfter =
              moving && moving.index >= others.length ? others[others.length - 1] : undefined;
            const panel = panels.find((item) => item.id === id);
            if (!panel) {
              return null;
            }
            const hidden = layout.hidden.includes(id);
            const size = sizeOf(layout, panel);
            return (
              <li
                key={id}
                ref={(row) => {
                  if (row) {
                    rows.current.set(id, row);
                  } else {
                    rows.current.delete(id);
                  }
                }}
                className={styles.customizeItem}
                data-dragging={drag?.id === id || undefined}
                data-over={landing === id ? 'before' : landingAfter === id ? 'after' : undefined}
                data-hidden={hidden || undefined}
              >
                <button
                  type="button"
                  className={styles.grip}
                  aria-label={`Move ${panel.title}: drag, or press the up and down arrows`}
                  onPointerDown={(event) => {
                    event.currentTarget.setPointerCapture(event.pointerId);
                    setDrag({ id, index });
                  }}
                  onPointerMove={(event) => {
                    if (drag?.id === id) {
                      const landingIndex = landingAt(id, event.clientY);
                      if (landingIndex !== drag.index) {
                        setDrag({ id, index: landingIndex });
                      }
                    }
                  }}
                  onPointerUp={() => {
                    if (drag?.id === id) {
                      moveTo(id, drag.index);
                    }
                    setDrag(null);
                  }}
                  onPointerCancel={() => setDrag(null)}
                  onKeyDown={(event) => {
                    if (event.key === 'ArrowUp' || event.key === 'ArrowDown') {
                      event.preventDefault();
                      step(id, event.key === 'ArrowUp' ? -1 : 1);
                    }
                  }}
                >
                  <Icon name="grip" />
                </button>
                <span>
                  <span className={styles.customizeName}>{panel.title}</span>
                  <span className={styles.customizeDescription}>{panel.description}</span>
                </span>
                <span className={styles.customizeControls}>
                  <button
                    type="button"
                    className={controls.iconButton}
                    onClick={() => step(id, -1)}
                    disabled={index === 0}
                    aria-label={`Move ${panel.title} up`}
                  >
                    <Icon name="chevronDown" size={14} style={{ transform: 'rotate(180deg)' }} />
                  </button>
                  <button
                    type="button"
                    className={controls.iconButton}
                    onClick={() => step(id, 1)}
                    disabled={index === layout.order.length - 1}
                    aria-label={`Move ${panel.title} down`}
                  >
                    <Icon name="chevronDown" size={14} />
                  </button>
                  <button
                    type="button"
                    className={controls.ghost}
                    style={{ height: 28, padding: '0 8px', minWidth: 46, justifyContent: 'center' }}
                    disabled={!panel.canBeHalf}
                    onClick={() =>
                      onChange({
                        ...layout,
                        sizes: { ...layout.sizes, [id]: size === 'full' ? 'half' : 'full' },
                      })
                    }
                    title={
                      panel.canBeHalf ? 'Whole row or half of it' : 'This panel needs the whole row'
                    }
                    aria-label={`${panel.title}: ${size === 'full' ? 'whole row' : 'half a row'}`}
                  >
                    {size === 'full' ? 'Full' : 'Half'}
                  </button>
                  <button
                    type="button"
                    className={controls.iconButton}
                    aria-pressed={!hidden}
                    aria-label={hidden ? `Show ${panel.title}` : `Hide ${panel.title}`}
                    onClick={() =>
                      onChange({
                        ...layout,
                        hidden: hidden
                          ? layout.hidden.filter((item) => item !== id)
                          : [...layout.hidden, id],
                      })
                    }
                  >
                    <Icon name={hidden ? 'eyeOff' : 'eye'} />
                  </button>
                </span>
              </li>
            );
          })}
        </ol>
        <footer className={styles.customizeFooter}>
          <button type="button" className={controls.ghost} onClick={onReset}>
            Reset to the default
          </button>
          <button type="button" className={controls.primary} onClick={onClose}>
            Done
          </button>
        </footer>
      </div>
    </dialog>
  );
}
