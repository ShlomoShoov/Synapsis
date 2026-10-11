/**
 * Where Observe reads from, and how often. Two services (contracts/synapsis-api.yaml):
 *
 * - Elasticsearch, for the ingestion log (index synapsis-ingest-log). The browser reads it directly; Elasticsearch
 *   allows the deployed client's origin (http://localhost:1897).
 * - The C# API, for its health (GET /health).
 *
 * While developing (npm run dev), the Vite server forwards /es and /api to the two services (vite.config.ts), so the
 * browser never needs cross-origin access. The built client calls the services by their own addresses. Either can be
 * changed at build time with VITE_ES_URL and VITE_API_URL.
 */

const env = import.meta.env;

/** The base address of Elasticsearch, without a trailing slash. */
export const elasticUrl: string = trimSlash(
  env.VITE_ES_URL ?? (env.DEV ? '/es' : 'http://localhost:9200'),
);

/** The base address of the API, without a trailing slash. */
export const apiUrl: string = trimSlash(
  env.VITE_API_URL ?? (env.DEV ? '/api' : 'http://localhost:8080'),
);

/** The index the API writes the ingestion log to. */
export const ingestLogIndex = 'synapsis-ingest-log';

/** How often the live flow asks for new log documents. */
export const livePollMs = 2_000;

/**
 * How far back each live poll looks before the newest document already shown. A batch is logged with the time it
 * was received but written once it is done, so a slow batch can appear after a faster one received later; looking
 * back this far (and dropping documents already shown) keeps it from being missed.
 */
export const liveOverlapMs = 90_000;

/** How often the connection status of each service is checked. */
export const healthCheckMs = 15_000;

/** How long a request may take before it counts as failed. */
export const requestTimeoutMs = 12_000;

function trimSlash(url: string): string {
  return url.endsWith('/') ? url.slice(0, -1) : url;
}
