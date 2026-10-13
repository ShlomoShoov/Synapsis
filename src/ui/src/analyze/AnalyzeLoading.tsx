/**
 * What /analyze shows while its code loads (the map engine is in its own chunk): the night sky and a quiet word.
 */
export function AnalyzeLoading() {
  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        display: 'grid',
        placeItems: 'center',
        background: '#03050a',
        color: '#7f90a8',
        fontStyle: 'italic',
      }}
    >
      Opening Analyze…
    </div>
  );
}
