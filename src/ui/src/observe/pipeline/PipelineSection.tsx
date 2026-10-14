/**
 * 03 Pipeline: what happens below the API, stage by stage (contracts/pipeline-log.md §3). One card per stage, in the
 * order a record goes through them: how fast it moves now, what it handled in the last 24 hours, what waits for it and
 * how long that would take at this rate, and what failed. Then every source, and the engine's last runs. Read straight
 * from Elasticsearch every 30 seconds; no API route.
 */
import { useEffect, useState } from 'react';
import { formatAge, formatCompact, formatCount, formatRate, formatSpan } from '../lib/format';
import {
  catchUpMs,
  type Pipeline,
  pipelinePollMs,
  readPipeline,
  silentMs,
  type Stage,
} from './pipeline';
import styles from './PipelineSection.module.css';

export function PipelineSection() {
  const [pipeline, setPipeline] = useState<Pipeline | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const controller = new AbortController();
    let timer = 0;
    const load = async () => {
      const at = Date.now();
      try {
        setPipeline(await readPipeline(at, controller.signal));
        setError(null);
      } catch (reason) {
        if (controller.signal.aborted) return;
        setError(reason instanceof Error ? reason.message : String(reason));
      }
      if (controller.signal.aborted) return;
      setNow(at);
      timer = window.setTimeout(() => void load(), pipelinePollMs);
    };
    void load();
    return () => {
      controller.abort();
      window.clearTimeout(timer);
    };
  }, []);

  return (
    <section id="below-api" className={styles.section} aria-labelledby="below-api-title">
      <div className={styles.stem} aria-hidden="true" />
      <header className={styles.intro}>
        <h3 id="below-api-title" className={styles.title}>
          Below the API
        </h3>
        <p>
          Where each record goes once the API has stored it: every stage live, what waits for it and
          when it catches up.
        </p>
      </header>
      {error && (
        <p className={styles.error}>
          The services' logs cannot be read now ({error}). The flow above is not affected.
        </p>
      )}
      {!pipeline ? (
        !error && <p className={styles.quiet}>Reading the services' logs…</p>
      ) : (
        <>
          <ol className={styles.stages}>
            {pipeline.stages.map((stage) => (
              <StageCard key={stage.id} stage={stage} now={now} />
            ))}
          </ol>

          <h3 className={styles.heading}>
            The engine's last runs <span>conclusions it sent back through the API</span>
          </h3>
          {pipeline.runs.length === 0 ? (
            <p className={styles.quiet}>The engine has not run yet.</p>
          ) : (
            <ul className={styles.runs}>
              {pipeline.runs.map((run) => (
                <li key={`${run.at}-${run.source}`}>
                  <span className={styles.muted}>{formatAge(run.at, now)}</span>
                  <strong>{run.source.replace(/^src:/, '') || 'all sources'}</strong>
                  <span>{run.message || `${formatCount(run.written)} conclusions sent`}</span>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  );
}

/** What each stage does, in plain words. */
const roles: Record<Stage['id'], string> = {
  api: 'Checks each batch the feeders send and stores every record.',
  relay: 'Passes each stored record on to the queue.',
  indexer: 'Makes each record searchable: the map, search and lists.',
  projector: 'Draws each record into the graph of connections.',
  engine: 'Reads the records and sends back its conclusions.',
};

function StageCard({ stage, now }: { stage: Stage; now: number }) {
  const live = stage.lastAt !== null && now - stage.lastAt < silentMs;
  const catchUp = catchUpMs(stage);
  return (
    <li
      className={styles.stage}
      data-live={live || undefined}
      data-moving={(live && stage.rate !== null && stage.rate > 0) || undefined}
    >
      <div className={styles.stageHead}>
        <span className={styles.dot} aria-hidden="true" />
        <strong>{stage.title}</strong>
        <span className={styles.muted}>→ {stage.into}</span>
        {stage.instances && (
          <span
            className={styles.copies}
            title="Copies of this service in its last heartbeat. Each reads its own share of the queue: the rate and the waiting are their sum."
          >
            {stage.instances} copies
          </span>
        )}
      </div>
      <p className={styles.role}>{roles[stage.id]}</p>
      <div className={styles.rate}>
        {stage.rate === null ? '—' : formatRate(stage.rate)}
        <span>{stage.id === 'engine' ? ' runs on demand' : ' now'}</span>
      </div>
      <dl className={styles.facts}>
        <dt>Last 24 h</dt>
        <dd>
          {formatCompact(stage.handled)} {stage.id === 'engine' ? 'conclusions' : 'handled'}
        </dd>
        {stage.lag !== null && (
          <>
            <dt>Waiting</dt>
            <dd data-bad={catchUp !== null && catchUp > 3_600_000 ? true : undefined}>
              {formatCompact(stage.lag)}
              {catchUp !== null ? (
                <small> · about {formatSpan(catchUp)} to catch up</small>
              ) : (
                stage.lag === 0 && <small> · caught up</small>
              )}
            </dd>
          </>
        )}
        <dt>{stage.id === 'api' ? 'Refused' : 'Failed'}</dt>
        <dd data-bad={stage.failed > 0 || undefined}>{formatCount(stage.failed)}</dd>
        {stage.skipped > 0 && (
          <>
            <dt>Skipped</dt>
            <dd>{formatCompact(stage.skipped)}</dd>
          </>
        )}
      </dl>
      <p className={styles.seen}>
        {stage.lastAt === null ? 'No word yet' : `Last word ${formatAge(stage.lastAt, now)}`}
        {stage.message && <span> · {stage.message}</span>}
      </p>
    </li>
  );
}
