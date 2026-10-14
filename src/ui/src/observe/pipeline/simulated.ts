/**
 * The pipeline in the simulation (?data=simulated): the API's numbers come from the simulated ingestion log, the
 * services after it are made up the same way at any moment (smooth waves of time, no randomness), so a replay that
 * comes back to a moment finds the pipeline as it left it.
 */
import type { IngestLog } from '../data/model';
import type { EngineRun, Pipeline, Stage } from './pipeline';

const dayMs = 86_400_000;
const recentMs = 300_000;
const hourMs = 3_600_000;
/** The engine runs every two hours, a few minutes past. */
const engineEveryMs = 2 * hourMs;
const engineOffsetMs = 7 * 60_000;

const wave = (at: number, periodMs: number, phase = 0) =>
  Math.sin((at / periodMs) * 2 * Math.PI + phase);

/** The services after an API that took `apiRate` records a second, at `at`. */
export function simulatedPipeline(
  at: number,
  api: { rate: number; handled: number; refused: number; lastAt: number | null },
): Pipeline {
  const heartbeat = Math.floor(at / 30_000) * 30_000;
  const relayRate = api.rate * (0.97 + 0.03 * wave(at, 7 * 60_000));
  // Six indexers share about 60 records a second; the projector is a little slower; both drain a slow backlog.
  const indexerRate = Math.max(0, 58 + 6 * wave(at, 11 * 60_000) + 3 * wave(at, 97_000, 1));
  const projectorRate = Math.max(0, 44 + 5 * wave(at, 13 * 60_000, 2) + 2 * wave(at, 71_000));
  const indexerLag = Math.round(
    1_400_000 + 900_000 * wave(at, 9 * hourMs) + 60_000 * wave(at, hourMs),
  );
  const projectorLag = Math.round(
    2_100_000 + 1_200_000 * wave(at, 11 * hourMs, 1) + 80_000 * wave(at, 1.3 * hourMs),
  );
  const failures = (periodMs: number, phase: number) =>
    Math.max(0, Math.round(4 * wave(at, periodMs, phase)));

  const service = (
    id: 'relay' | 'indexer' | 'projector',
    title: string,
    into: string,
    rate: number,
    lag: number | null,
    instances?: number,
  ): Stage => ({
    id,
    title,
    into,
    rate,
    handled: Math.round(rate * (dayMs / 1000) * 0.92),
    failed: id === 'relay' ? 0 : failures(id === 'indexer' ? 5 * hourMs : 7 * hourMs, at % 3),
    skipped: id === 'projector' ? Math.round(rate * 900) : 0,
    lag,
    instances,
    lastAt: heartbeat,
  });

  const runs: EngineRun[] = [];
  const lastRun =
    Math.floor((at - engineOffsetMs) / engineEveryMs) * engineEveryMs + engineOffsetMs;
  const sources = ['src:ucdp:ged', 'src:gdelt:events', 'src:acled:events'];
  for (let i = 0; i < 4; i += 1) {
    const runAt = lastRun - i * engineEveryMs;
    const written = Math.round(900 + 400 * (1 + wave(runAt, 5 * hourMs)));
    runs.push({
      at: runAt,
      source: sources[i % sources.length] ?? '',
      written,
      message: i % 2 === 0 ? '' : '7/7 proofs; golden 5 pass, 0 fail, 3 pending',
    });
  }

  return {
    stages: [
      {
        id: 'api',
        title: 'API',
        into: 'MongoDB',
        rate: api.rate,
        handled: api.handled,
        failed: api.refused,
        skipped: 0,
        lag: null,
        lastAt: api.lastAt,
      },
      service('relay', 'Relay', 'Kafka', relayRate, null),
      service('indexer', 'Indexer', 'Elasticsearch', indexerRate, indexerLag, 6),
      service('projector', 'Projector', 'Neo4j', projectorRate, projectorLag),
      {
        id: 'engine',
        title: 'Engine',
        into: 'API',
        rate: null,
        handled: runs.reduce((total, run) => total + run.written, 0) * 3,
        failed: 0,
        skipped: 0,
        lag: null,
        lastAt: runs[0]?.at ?? null,
        message: runs[0]?.message || undefined,
      },
    ],
    sources: [],
    runs,
  };
}

/** Reads the simulated pipeline at `at`: the API's part from the simulated log. */
export async function readSimulatedPipeline(
  log: IngestLog,
  at: number,
  signal?: AbortSignal,
): Promise<Pipeline> {
  const [recent, day] = await Promise.all([
    log.totals({ range: { from: at - recentMs, to: at } }, signal),
    log.totals({ range: { from: at - dayMs, to: at } }, signal),
  ]);
  return simulatedPipeline(at, {
    rate: recent.received / (recentMs / 1000),
    handled: day.received,
    refused: day.rejected,
    lastAt: recent.entries > 0 ? at : day.entries > 0 ? at - recentMs : null,
  });
}
