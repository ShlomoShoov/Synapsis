/**
 * What shows while Observe's code arrives on a first visit: the night room, empty, so the page never flashes the
 * home page's paper before going dark. Kept in its own small file so the home page's bundle stays light.
 */

/** The loading screen of /observe. */
export function ObserveLoading() {
  return (
    <div
      aria-busy="true"
      style={{ position: 'fixed', inset: 0, background: '#0d0c0a', color: '#0d0c0a' }}
    >
      Loading Observe
    </div>
  );
}
