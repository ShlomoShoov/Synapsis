/**
 * What /analyze shows while its code loads (the map engine is in its own chunk): the page's paper and a quiet word.
 */
export function AnalyzeLoading() {
  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        display: 'grid',
        placeItems: 'center',
        background: 'var(--paper)',
        color: 'var(--ink-muted)',
        fontStyle: 'italic',
      }}
    >
      Opening Analyze…
    </div>
  );
}
