/**
 * The C# API as Observe sees it: only its health (GET /health). The ingestion log is read from Elasticsearch.
 */
import { apiUrl } from '../../config';
import { isAbort, requestText } from '../http';
import type { ApiService, Ping } from '../model';

const service = 'The API';

/** The C# API. */
export class ApiClient implements ApiService {
  readonly kind = 'api';
  readonly location: string;

  /** Talks to the API at the given address (config.apiUrl by default). */
  constructor(baseUrl: string = apiUrl) {
    this.location = baseUrl;
  }

  async ping(signal?: AbortSignal): Promise<Ping> {
    const started = performance.now();
    try {
      const text = await requestText(
        `${this.location}/health`,
        { method: 'GET' },
        { service, signal, timeoutMs: 5_000 },
      );
      return { ok: true, latencyMs: performance.now() - started, detail: text.trim() || 'Healthy' };
    } catch (error) {
      if (isAbort(error)) {
        throw error;
      }
      return {
        ok: false,
        latencyMs: performance.now() - started,
        detail: error instanceof Error ? error.message : String(error),
      };
    }
  }
}
