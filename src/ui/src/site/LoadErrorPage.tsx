/**
 * The answer when a page's code cannot be loaded. After a new build the code's file names change: a tab opened
 * before it asks for the old names and gets 404. It reloads once to fetch the new build; if that does not help
 * (within a minute), it says so instead of reloading again.
 */
import { useEffect, useState } from 'react';
import { NoticePage } from './NoticePage';

const reloadedKey = 'synapsis.reloadedAt';
const retryMs = 60_000;

/** Whether to reload: not when it already did so within the last minute (or cannot tell). */
function shouldReload(): boolean {
  try {
    return Date.now() - Number(sessionStorage.getItem(reloadedKey) ?? 0) >= retryMs;
  } catch {
    return false;
  }
}

/** The route error page. */
export function LoadErrorPage() {
  const [reload] = useState(shouldReload);
  useEffect(() => {
    if (!reload) return;
    try {
      sessionStorage.setItem(reloadedKey, String(Date.now()));
    } catch {
      // Storage full: reload anyway.
    }
    window.location.reload();
  }, [reload]);
  if (reload) return null;
  return (
    <NoticePage
      label="Could not load"
      title="This page could not be loaded."
      line="Reload the page to try again."
    />
  );
}
