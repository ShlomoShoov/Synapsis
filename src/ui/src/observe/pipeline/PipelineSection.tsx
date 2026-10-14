/**
 * 03 Pipeline: what happens below the API, stage by stage (contracts/pipeline-log.md §3). One card per stage, in the
 * order a record goes through them: how fast it moves now, what it handled in the last 24 hours, what waits for it and
 * how long that would take at this rate, and what failed. Then every source, and the engine's last runs. Read straight
 * from Elasticsearch every 30 seconds; no API route.
 */
import { useEffect, useState } from 'react';
import { formatAge, formatCompact, formatCount, formatRate, formatSpan } from '../lib/format';
import { SectionHeader } from '../shell/SectionHeader';
import { sectionInfo } from '../shell/sections';
import { catchUpMs, type Pipeline, pipelinePollMs, readPipeline, silentMs, type Stage } from './pipeline';
import styles from './PipelineSection.module.css';

export function PipelineSection() {
  const section = sectionInfo('pipeline');
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
    <section
      id="pipeline"
      data-nav-section="pipeline"
      className={styles.section}
      aria-labelledby="pipeline-title"
    >
      <SectionHeader section={section} />
      {error && <p className={styles.error}>Could not read the logs: {error}</p>}
      {!pipeline ? (
        !error && <p className={styles.quiet}>Reading the logs…</p>
      ) : (
        <>
          <ol className={styles.stages} id="pipeline-stages" data-nav-anchor="Stages">
            {pipeline.stages.map((stage) => (
              <StageCard key={stage.id} stage={stage} now={now} />
            ))}
          </ol>

          <h3 className={styles.heading} id="pipeline-sources" data-nav-anchor="Per source">
            Per source <span>everything each source sent through the API</span>
          </h3>
          {pipeline.sources.length === 0 ? (
            <p className={styles.quiet}>No batch yet.</p>
          ) : (
            <div className={styles.tableWrap}>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th>Source</th>
                    <th>Sent by</th>
                    <th>Batches</th>
                    <th>Received</th>
                    <th>Stored</th>
                    <th>Refused</th>
                    <th>Last batch</th>
                  </tr>
                </thead>
                <tbody>
                  {pipeline.sources.map((row) => (
                    <tr key={row.source}>
                      <td>{row.source.replace(/^src:/, '')}</td>
                      <td className={styles.muted}>{row.producers.join(', ')}</td>
                      <td>{formatCount(row.batches)}</td>
                      <td>{formatCount(row.received)}</td>
                      <td>{formatCount(row.stored)}</td>
                      <td data-bad={row.refused > 0 || undefined}>{formatCount(row.refused)}</td>
                      <td className={styles.muted}>{row.lastAt ? formatAge(row.lastAt, now) : '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <h3 className={styles.heading} id="pipeline-engine" data-nav-anchor="Engine runs">
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

function StageCard({ stage, now }: { stage: Stage; now: number }) {
  const live = stage.lastAt !== null && now - stage.lastAt < silentMs;
  const catchUp = catchUpMs(stage);
  return (
    <li className={styles.stage} data-live={live || undefined}>
      <div className={styles.stageHead}>
        <span className={styles.dot} aria-hidden="true" />
        <strong>{stage.title}</strong>
        <span className={styles.muted}>→ {stage.into}</span>
      </div>
      <div className={styles.rate}>
        {stage.rate === null ? '—' : formatRate(stage.rate)}
        <span>{stage.id === 'engine' ? 'runs on demand' : 'now'}</span>
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
              {catchUp !== null && <small> · about {formatSpan(catchUp)} to catch up</small>}
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
