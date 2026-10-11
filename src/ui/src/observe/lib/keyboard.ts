/**
 * Keyboard helpers for shortcuts.
 */

/** True when the keyboard is busy typing (a field, an editable area): shortcuts stay out of the way. */
export function isTyping(event: KeyboardEvent): boolean {
  const target = event.target as HTMLElement | null;
  return (
    !!target &&
    (target.isContentEditable ||
      target.tagName === 'INPUT' ||
      target.tagName === 'TEXTAREA' ||
      target.tagName === 'SELECT')
  );
}

/** True when a shortcut should be ignored: typing, or a modifier key held for the browser's own shortcuts. */
export function ignoreShortcut(event: KeyboardEvent): boolean {
  return (
    event.defaultPrevented || event.metaKey || event.ctrlKey || event.altKey || isTyping(event)
  );
}
