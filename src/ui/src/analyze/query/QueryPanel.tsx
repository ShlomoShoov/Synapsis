/**
 * The query panel, on the left edge: every question sent to the API, the newest first, and for the chosen one the
 * query exactly as sent (route and parameters, one per line), how long it took, and the answer as it came (long lists
 * cut to their first items). Folded, it is a slim tab with the last query's time.
 */
import { useMemo, useState } from 'react';
import { analyzeApiUrl } from '../config';
import { clearLog, useQueryLog, type QueryRecord } from '../api/queryLog';
import { PanelFrame } from '../lib/PanelFrame';
import { sizeOf, tokensOf, trimmed } from './jsonView';
import styles from './QueryPanel.module.css';

export function QueryPanel() {
  const log = useQueryLog();
  const [open, setOpen] = useState(false);
  const [chosenId, setChosenId] = useState<number | null>(null);
  const chosen = log.find((record) => record.id === chosenId) ?? log[0];
  const last = log[0];

  if (!open) {
    return (
      <button
        type="button"
        className={styles.tab}
        onClick={() => setOpen(true)}
        aria-expanded={false}
      >
        <span className={styles.tabIcon} aria-hidden="true">
          {'{ }'}
        </span>
        <span className={styles.tabLabel}>Queries</span>
        <span className={styles.tabCount}>{log.length}</span>
        {last && <span className={styles.tabTime}>{timeOf(last)}</span>}
      </button>
    );
  }

  return (
    <PanelFrame name="queries" label="the query panel" width={460} minWidth={300} fill>
      <aside className={styles.panel} aria-label="Queries sent to the API">
        <header className={styles.header}>
          <span className={styles.title}>Queries</span>
          <span className={styles.base} title="The API these queries go to (VITE_ANALYZE_API_URL)">
            {analyzeApiUrl}
          </span>
          <button type="button" className={styles.small} onClick={clearLog}>
            Clear
          </button>
          <button
            type="button"
            className={styles.close}
            onClick={() => setOpen(false)}
            aria-label="Fold the query panel"
            aria-expanded
          >
            <svg viewBox="0 0 20 20" aria-hidden="true">
              <path d="M12.5 5 7.5 10l5 5" />
            </svg>
          </button>
        </header>

        <ol className={styles.list}>
          {log.length === 0 && <li className={styles.empty}>No query yet.</li>}
          {log.map((record) => (
            <li key={record.id}>
              <button
                type="button"
                className={styles.entry}
                aria-pressed={record.id === chosen?.id}
                onClick={() => setChosenId(record.id)}
              >
                <span className={styles.state} data-state={record.state}>
                  {stateOf(record)}
                </span>
                <span className={styles.route}>{record.route.replace('GET ', '')}</span>
                <span className={styles.summary}>{summaryOf(record)}</span>
                <span className={styles.ms}>{timeOf(record)}</span>
              </button>
            </li>
          ))}
        </ol>

        {chosen && <QueryDetail record={chosen} />}
      </aside>
    </PanelFrame>
  );
}

function QueryDetail({ record }: { record: QueryRecord }) {
  const answer = useMemo(
    () =>
      record.answer === undefined
        ? []
        : tokensOf(JSON.stringify(trimmed(record.answer, 3), null, 2)),
    [record.answer],
  );
  return (
    <section className={styles.detail}>
      <h3 className={styles.heading}>
        Query
        <span>
          {new Date(record.startedAt).toLocaleTimeString('en-GB')} · {timeOf(record)}
          {record.bytes !== undefined && ` · ${sizeOf(record.bytes)}`}
        </span>
      </h3>
      <pre className={styles.query}>
        <span className={styles.method}>GET</span> {record.route.replace('GET ', '')}
        {record.params.map(([name, value], index) => (
          <span key={`${name}-${index}`} className={styles.param}>
            {'\n  '}
            <span className={styles.paramName}>{name}</span>
            <span className={styles.equals}> = </span>
            <span className={styles.paramValue}>{value}</span>
          </span>
        ))}
      </pre>
      <h3 className={styles.heading}>
        Answer
        <span>{record.status !== undefined ? `HTTP ${record.status}` : stateOf(record)}</span>
      </h3>
      {record.error ? (
        <p className={styles.error}>{record.error}</p>
      ) : (
        <pre className={styles.answer}>
          {answer.map((token, index) => (
            <span key={index} data-kind={token.kind}>
              {token.text}
            </span>
          ))}
        </pre>
      )}
    </section>
  );
}

function stateOf(record: QueryRecord): string {
  if (record.state === 'running') return '…';
  if (record.state === 'memory') return 'memory';
  if (record.state === 'failed') return record.status ? String(record.status) : 'failed';
  return String(record.status ?? 'ok');
}

function timeOf(record: QueryRecord): string {
  if (record.state === 'running') return 'asking…';
  if (record.state === 'memory') return '0 ms';
  return `${record.ms ?? 0} ms`;
}

/** What an answer holds, in a few words: "points 470", "cells 163". */
function summaryOf(record: QueryRecord): string {
  const answer = record.answer as
    | { layer?: string; events?: number; total?: number; connections?: unknown[] }
    | unknown[]
    | undefined;
  if (!answer) return record.state === 'failed' ? 'no answer' : '';
  if (Array.isArray(answer)) return `${answer.length} statements`;
  if (answer.layer) return `${answer.layer} ${answer.total ?? 0} · ${answer.events ?? 0} events`;
  if (answer.connections) return `${answer.connections.length} groups`;
  return '';
}
