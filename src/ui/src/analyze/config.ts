/**
 * Where Analyze reads from. One address for the three reading routes (contracts/synapsis-api.yaml):
 * GET /analyze/map, GET /analyze/graph and GET /records/{id}.
 *
 * The mock (Synapsis.MockApi, http://localhost:8081) and the real API answer the same routes with the same shapes, so
 * switching is one change: set VITE_ANALYZE_API_URL when starting the dev server or building, for example
 * `VITE_ANALYZE_API_URL=http://localhost:8080 npm run dev`. Without it, Analyze reads the mock.
 */

/** The base address of the Analyze routes, without a trailing slash. */
export const analyzeApiUrl: string = trimSlash(
  import.meta.env.VITE_ANALYZE_API_URL ?? 'http://localhost:8081',
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
