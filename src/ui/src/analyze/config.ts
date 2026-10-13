/**
 * Where Analyze reads from: the read API (EMERGENCY.md §12), GET /map/cells, /map/points, /search, /logical, /graph and
 * /records/{id}. Set VITE_ANALYZE_API_URL when starting the dev server or building to read another address.
 */

/** The base address of the Analyze routes, without a trailing slash. */
export const analyzeApiUrl: string = trimSlash(
  import.meta.env.VITE_ANALYZE_API_URL ?? 'http://localhost:8080',
);

/** How long a request may take before it counts as failed. */
export const requestTimeoutMs = 15_000;

/**
 * The elevation tiles (Terrarium encoding, public on AWS, no key): the relief shading and the 3D terrain. Everything
 * else on the map is drawn from the files in assets/geo/, so the globe needs no tile server.
 */
export const terrainTilesUrl =
  'https://elevation-tiles-prod.s3.amazonaws.com/terrarium/{z}/{x}/{y}.png';

function trimSlash(url: string): string {
  return url.endsWith('/') ? url.slice(0, -1) : url;
}
