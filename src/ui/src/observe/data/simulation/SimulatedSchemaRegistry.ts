/**
 * The schema packages of the simulation: a snapshot of schemas/seed/core.yaml and feeders/packages/*.yaml, converted
 * to the JSON that GET /schemas/{package} answers (demo-packages.json). The snapshot is loaded only when the
 * simulation is used. It is demo data: the YAML files stay the truth, and the snapshot may lag behind them.
 */
import type { SchemaDocument, SchemaSummary } from '../contracts';
import type { Ping, SchemaRegistry } from '../model';
import { delay } from './SimulatedIngestLog';

/** The schema packages of the simulation. */
export class SimulatedSchemaRegistry implements SchemaRegistry {
  readonly kind = 'simulated';
  readonly location = 'Simulation';
  private documents: Promise<SchemaDocument[]> | null = null;

  async list(signal?: AbortSignal): Promise<SchemaSummary[]> {
    const documents = await this.all(signal);
    return documents.map(({ name, version, fingerprint }) => ({ name, version, fingerprint }));
  }

  async all(signal?: AbortSignal): Promise<SchemaDocument[]> {
    this.documents ??= import('./demo-packages.json').then(
      (module) => module.default as unknown as SchemaDocument[],
    );
    await delay(90, signal);
    return this.documents;
  }

  async ping(signal?: AbortSignal): Promise<Ping> {
    await delay(40, signal);
    return { ok: true, latencyMs: 40, detail: 'Simulated API' };
  }
}
