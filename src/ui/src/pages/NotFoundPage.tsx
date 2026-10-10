/**
 * The answer to an address the client does not know.
 */

/** The not-found page (every route that is not listed in router.ts). */
export function NotFoundPage() {
  return (
    <main style={{ minHeight: '100vh', display: 'grid', placeContent: 'center', textAlign: 'center' }}>
      <h1 style={{ fontWeight: 300, fontSize: 48, margin: 0 }}>This page is beyond what we know.</h1>
      <p>
        <a href="/home">Return home</a>
      </p>
    </main>
  );
}
