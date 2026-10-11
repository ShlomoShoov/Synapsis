/**
 * The schema packages read from the C# API: GET /schemas lists them, GET /schemas/{package} gives each one's content.
 * A package is fetched again only when its fingerprint changed, so a refresh of 24 unchanged packages costs one list.
 */
import { apiUrl } from '../../config';
import type { SchemaDocument, SchemaSummary } from '../contracts';
import { isAbort, requestJson, requestText } from '../http';
import type { Ping, SchemaRegistry } from '../model';

const service = 'The API';

/** The schema packages, from the API. */
export class ApiSchemaRegistry implements SchemaRegistry {
  readonly kind = 'api';
  readonly location: string;
  /** The packages already read, by name; reused while their fingerprint is the same. */
  private readonly cache = new Map<string, SchemaDocument>();

  /** Reads from the API at the given address (config.apiUrl by default). */
  constructor(baseUrl: string = apiUrl) {
    this.location = baseUrl;
  }

  list(signal?: AbortSignal): Promise<SchemaSummary[]> {
    return requestJson<SchemaSummary[]>(
      `${this.location}/schemas`,
      { method: 'GET' },
      { service, signal },
    );
  }

  async all(signal?: AbortSignal): Promise<SchemaDocument[]> {
    const summaries = await this.list(signal);
    const documents = await Promise.all(
      summaries.map(async (summary) => {
        const cached = this.cache.get(summary.name);
        if (cached && cached.fingerprint === summary.fingerprint) {
          return cached;
        }
        const document = await requestJson<SchemaDocument>(
          `${this.location}/schemas/${encodeURIComponent(summary.name)}`,
          { method: 'GET' },
          { service, signal },
        );
        this.cache.set(summary.name, document);
        return document;
      }),
    );
    return documents.sort((a, b) => a.name.localeCompare(b.name));
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
