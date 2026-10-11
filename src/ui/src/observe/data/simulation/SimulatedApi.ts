/**
 * The API of the simulation: it always answers, after a short pause.
 */
import type { ApiService, Ping } from '../model';
import { delay } from './SimulatedIngestLog';

/** The simulated API. */
export class SimulatedApi implements ApiService {
  readonly kind = 'simulated';
  readonly location = 'Simulation';

  async ping(signal?: AbortSignal): Promise<Ping> {
    await delay(40, signal);
    return { ok: true, latencyMs: 40, detail: 'Simulated API' };
  }
}
