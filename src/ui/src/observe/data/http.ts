/**
 * One way to call a service: fetch with a time limit, and failures turned into a ServiceError that says what went
 * wrong in words a person can act on. A request the caller cancels (its AbortSignal) still rejects with the
 * browser's AbortError, so callers can tell "cancelled" from "failed" with isAbort().
 */
import { requestTimeoutMs } from '../config';

/** Why a request failed. */
export type ServiceErrorKind = 'unreachable' | 'timeout' | 'http' | 'invalid';

/** A failed request to a service. */
export class ServiceError extends Error {
  readonly kind: ServiceErrorKind;
  /** The service, such as "Elasticsearch". */
  readonly service: string;
  /** The HTTP status, for kind "http". */
  readonly status: number | undefined;

  constructor(service: string, kind: ServiceErrorKind, message: string, status?: number) {
    super(message);
    this.name = 'ServiceError';
    this.service = service;
    this.kind = kind;
    this.status = status;
  }
}

/** True for the error of a request its caller cancelled. */
export function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

/** Options of one request. */
export interface RequestOptions {
  /** The service's name, for messages. */
  service: string;
  /** Cancels the request. */
  signal?: AbortSignal;
  /** The time limit; the default is config.requestTimeoutMs. */
  timeoutMs?: number;
}

/**
 * Sends a request and returns the answer's body as text. Throws ServiceError when the service cannot be reached,
 * takes too long, or answers with an error status (the body's reason is in the message).
 */
export async function requestText(
  url: string,
  init: RequestInit,
  options: RequestOptions,
): Promise<string> {
  const { service, signal } = options;
  const timeout = AbortSignal.timeout(options.timeoutMs ?? requestTimeoutMs);
  const combined = signal ? AbortSignal.any([signal, timeout]) : timeout;
  let response: Response;
  try {
    response = await fetch(url, { ...init, signal: combined });
  } catch (error) {
    if (signal?.aborted) {
      throw signal.reason instanceof DOMException
        ? signal.reason
        : new DOMException('The request was cancelled.', 'AbortError');
    }
    if (timeout.aborted) {
      throw new ServiceError(service, 'timeout', `${service} did not answer in time (${url}).`);
    }
    throw new ServiceError(
      service,
      'unreachable',
      `${service} cannot be reached at ${url}. ${error instanceof Error ? error.message : ''}`.trim(),
    );
  }
  const text = await response.text();
  if (!response.ok) {
    throw new ServiceError(
      service,
      'http',
      `${service} answered ${response.status}: ${reasonOf(text) ?? response.statusText}`,
      response.status,
    );
  }
  return text;
}

/** Sends a request and returns the answer's body read as JSON. */
export async function requestJson<T>(
  url: string,
  init: RequestInit,
  options: RequestOptions,
): Promise<T> {
  const text = await requestText(url, init, options);
  try {
    return JSON.parse(text) as T;
  } catch {
    throw new ServiceError(
      options.service,
      'invalid',
      `${options.service} sent an answer that is not JSON.`,
    );
  }
}

/** The reason inside an error body: Elasticsearch's error.reason, or the API's message. */
function reasonOf(body: string): string | null {
  try {
    const parsed = JSON.parse(body) as {
      error?: { reason?: string; root_cause?: { reason?: string }[] } | string;
      message?: string;
    };
    if (typeof parsed.error === 'object' && parsed.error) {
      return parsed.error.root_cause?.[0]?.reason ?? parsed.error.reason ?? null;
    }
    if (typeof parsed.error === 'string') {
      return parsed.error;
    }
    return parsed.message ?? null;
  } catch {
    return body.trim().slice(0, 200) || null;
  }
}
