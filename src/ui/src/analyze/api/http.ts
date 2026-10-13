/**
 * One way to call the Analyze API: fetch with a time limit, and failures turned into an ApiError whose message a person
 * can act on (the API's problem answer, its "detail" or its "errors", when it gives one). A request its caller cancels
 * still rejects with the browser's AbortError, so callers can tell "cancelled" from "failed" with isAbort().
 */
import { requestTimeoutMs } from '../config';
import { logEnd, logStart } from './queryLog';

export type ApiErrorKind = 'unreachable' | 'timeout' | 'http' | 'invalid';

/** A failed request. */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  /** The HTTP status, for kind "http". */
  readonly status: number | undefined;

  constructor(kind: ApiErrorKind, message: string, status?: number) {
    super(message);
    this.name = 'ApiError';
    this.kind = kind;
    this.status = status;
  }
}

/** True for the error of a request its caller cancelled. */
export function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

/** GET a URL and read the answer as JSON. Every request is written to the query log (queryLog.ts). */
export async function getJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const id = logStart(url);
  const started = performance.now();
  const elapsed = () => Math.round(performance.now() - started);
  try {
    const answer = await fetchJson<T>(url, signal, (status, bytes) =>
      logEnd(id, { status, bytes }),
    );
    logEnd(id, { state: 'done', ms: elapsed(), answer });
    return answer;
  } catch (error) {
    logEnd(id, {
      state: 'failed',
      ms: elapsed(),
      error: isAbort(error)
        ? 'Cancelled: a newer question replaced it.'
        : String((error as Error).message),
    });
    throw error;
  }
}

async function fetchJson<T>(
  url: string,
  signal: AbortSignal | undefined,
  onResponse: (status: number, bytes: number) => void,
): Promise<T> {
  // The development mode (config.ts), tested on the setting itself so that the build drops it otherwise.
  if (import.meta.env.VITE_ANALYZE_FIXTURES === '1') {
    return fromFixtures<T>(url, signal, onResponse);
  }
  const timeout = AbortSignal.timeout(requestTimeoutMs);
  const combined = signal ? AbortSignal.any([signal, timeout]) : timeout;
  let response: Response;
  try {
    response = await fetch(url, { signal: combined, headers: { Accept: 'application/json' } });
  } catch {
    if (signal?.aborted) {
      throw new DOMException('The request was cancelled.', 'AbortError');
    }
    if (timeout.aborted) {
      throw new ApiError('timeout', `The Analyze API did not answer in time (${url}).`);
    }
    throw new ApiError(
      'unreachable',
      `The Analyze API cannot be reached at ${new URL(url).origin}. Is it running?`,
    );
  }
  const text = await response.text();
  onResponse(response.status, new TextEncoder().encode(text).length);
  if (!response.ok) {
    throw new ApiError(
      'http',
      `The Analyze API answered ${response.status}: ${problemOf(text) ?? response.statusText}`,
      response.status,
    );
  }
  try {
    return JSON.parse(text) as T;
  } catch {
    throw new ApiError('invalid', 'The Analyze API sent an answer that is not JSON.');
  }
}

/** The development mode: the answer saved in fixtures/analyze/ (api/fixtures.ts), read like a network answer. */
async function fromFixtures<T>(
  url: string,
  signal: AbortSignal | undefined,
  onResponse: (status: number, bytes: number) => void,
): Promise<T> {
  const { fixtureAnswer } = await import('./fixtures');
  const { status, text } = await fixtureAnswer(url);
  if (signal?.aborted) throw new DOMException('The request was cancelled.', 'AbortError');
  onResponse(status, new TextEncoder().encode(text).length);
  if (status !== 200) {
    throw new ApiError('http', `The Analyze API answered ${status}: ${problemOf(text)}`, status);
  }
  return JSON.parse(text) as T;
}

/** The reason inside an ASP.NET problem answer: its errors (per parameter), its detail, or its title. */
export function problemOf(body: string): string | null {
  try {
    const problem = JSON.parse(body) as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
    };
    if (problem.errors) {
      const messages = Object.values(problem.errors).flat();
      if (messages.length > 0) {
        return messages.join(' ');
      }
    }
    return problem.detail ?? problem.title ?? null;
  } catch {
    return body.trim().slice(0, 200) || null;
  }
}
