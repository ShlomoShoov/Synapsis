/**
 * The small controls of Observe: a segmented choice, a switch, a popover and a hint. Built on native elements,
 * so keyboards and screen readers get the expected behaviour (arrow keys move a choice, Escape closes a popover).
 */
import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import styles from './controls.module.css';

/** One option of a segmented choice. */
export interface SegmentOption<T extends string> {
  value: T;
  label: ReactNode;
  /** Read by screen readers when the label is short or a symbol. */
  description?: string;
}

/**
 * A choice of one among a few options, shown side by side. Native radio buttons underneath, so the arrow keys move
 * the choice and screen readers announce the group.
 */
export function Segmented<T extends string>({
  options,
  value,
  onChange,
  label,
  small = false,
}: {
  options: readonly SegmentOption<T>[];
  value: T;
  onChange: (value: T) => void;
  /** What is being chosen, for screen readers. */
  label: string;
  small?: boolean;
}) {
  const name = useId();
  return (
    <fieldset className={`${styles.segmented} ${small ? styles.small : ''}`}>
      <legend className={styles.srOnly}>{label}</legend>
      {options.map((option) => (
        <label
          key={option.value}
          className={styles.segment}
          data-checked={option.value === value || undefined}
        >
          <input
            type="radio"
            className={styles.srOnly}
            name={name}
            value={option.value}
            checked={option.value === value}
            aria-label={option.description}
            onChange={() => onChange(option.value)}
          />
          {option.label}
        </label>
      ))}
    </fieldset>
  );
}

/** An on/off switch with its label. */
export function Switch({
  checked,
  onChange,
  children,
}: {
  checked: boolean;
  onChange: (checked: boolean) => void;
  children: ReactNode;
}) {
  return (
    <label className={styles.switch}>
      <input
        type="checkbox"
        role="switch"
        aria-checked={checked}
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <span className={styles.switchTrack} aria-hidden="true" />
      {children}
    </label>
  );
}

/**
 * A popover: a trigger and a panel that opens under it (a disclosure: the trigger says it is expanded and which
 * panel it controls). The panel closes on Escape, on a click outside, and when `close` is called from inside it.
 */
export function Popover({
  trigger,
  children,
  align = 'start',
  label,
  panelClassName,
}: {
  /** Draws the trigger; call `toggle` from it. */
  trigger: (props: { open: boolean; toggle: () => void; id: string }) => ReactNode;
  /** The panel's content; `close` closes it. */
  children: (close: () => void) => ReactNode;
  align?: 'start' | 'end';
  /** What the panel holds, for screen readers. */
  label: string;
  panelClassName?: string;
}) {
  const [open, setOpen] = useState(false);
  const anchor = useRef<HTMLDivElement>(null);
  const id = useId();
  useEffect(() => {
    if (!open) {
      return;
    }
    const onPointer = (event: PointerEvent) => {
      if (!anchor.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const onKey = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
        anchor.current?.querySelector<HTMLElement>('[aria-controls]')?.focus();
      }
    };
    document.addEventListener('pointerdown', onPointer);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onPointer);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);
  return (
    <div ref={anchor} className={styles.popoverAnchor}>
      {trigger({ open, toggle: () => setOpen((value) => !value), id })}
      {open ? (
        <section
          id={id}
          aria-label={label}
          data-align={align}
          className={`${styles.popover} ${panelClassName ?? ''}`}
        >
          {children(() => setOpen(false))}
        </section>
      ) : null}
    </div>
  );
}

/** Wraps an element with a small label shown on hover and focus. */
export function Hint({
  text,
  side = 'top',
  children,
}: {
  text: string;
  side?: 'top' | 'right';
  children: ReactNode;
}) {
  return (
    <span className={styles.hint} data-hint={text} data-side={side}>
      {children}
    </span>
  );
}

/** A keyboard key, as shortcuts are written. */
export function Kbd({ children }: { children: ReactNode }) {
  return <kbd className={styles.kbd}>{children}</kbd>;
}
