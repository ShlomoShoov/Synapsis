/**
 * Closes a modal dialog when the viewer clicks outside it (on its backdrop), as Escape already does. The listener is
 * attached to the dialog itself; a click counts as outside only when it lands beyond the dialog's box.
 */
import { useEffect, useLayoutEffect, useRef, type RefObject } from 'react';

/** Calls `onClose` when a click lands on the backdrop of `dialog`. */
export function useLightDismiss(
  dialog: RefObject<HTMLDialogElement | null>,
  onClose: () => void,
): void {
  const close = useRef(onClose);
  useLayoutEffect(() => {
    close.current = onClose;
  });
  useEffect(() => {
    const element = dialog.current;
    if (!element) {
      return;
    }
    const onClick = (event: MouseEvent) => {
      if (event.target !== element) {
        return;
      }
      const box = element.getBoundingClientRect();
      const inside =
        event.clientX >= box.left &&
        event.clientX <= box.right &&
        event.clientY >= box.top &&
        event.clientY <= box.bottom;
      if (!inside) {
        close.current();
      }
    };
    element.addEventListener('click', onClick);
    return () => element.removeEventListener('click', onClick);
  }, [dialog]);
}
