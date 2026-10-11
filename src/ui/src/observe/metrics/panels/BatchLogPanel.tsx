/**
 * The batch log: every log entry of the period, newest first (one per batch and source), read a page at a time from
 * the log. Narrow it to batches with refusals, or to batch ids that start with some text; open one for its details.
 */
import { useState } from 'react';
import { sourceInfo } from '../../data/catalog';
import type { EntriesPage, LogEntry } from '../../data/model';
import { useObserveData } from '../../data/observeContext';
import { useResource } from '../../data/useResource';
import { formatCount, formatDuration } from '../../lib/format';
import { useTimeFormats } from '../../lib/timeZone';
import { DomainDot } from '../../shared/OutcomeBar';
import { Switch } from '../../ui/controls';
import controls from '../../ui/controls.module.css';
import { Icon } from '../../ui/Icon';
import { DataTable } from '../charts/parts';
import type { PanelInfo, PanelSize } from '../layout';
import styles from '../MetricsSection.module.css';
import { Panel } from '../Panel';
import type { MetricsData } from './common';

const pageSize = 25;

/** The batch log panel. */
export function BatchLogPanel({
  panel,
  size,
  data,
}: {
  panel: PanelInfo;
  size: PanelSize;
  data: MetricsData;
}) {
  const { log } = useObserveData();
  const formats = useTimeFormats();
  const [onlyRejected, setOnlyRejected] = useState(false);
  const [prefix, setPrefix] = useState('');
  /** Older pages read with "Load older", for the question they were read for. */
  const [older, setOlder] = useState<{ question: string; pages: EntriesPage[] }>({
    question: '',
    pages: [],
  });
  const [loadingMore, setLoadingMore] = useState(false);

  const question = `${data.scope}:${onlyRejected}:${prefix.trim()}`;
  const first = useResource(`log:${question}`, (signal) =>
    log.entries(
      {
        range: data.range,
        sources: data.sources,
        order: 'desc',
        limit: pageSize,
        onlyRejected,
        batchPrefix: prefix.trim() || undefined,
      },
      signal,
    ),
  );
  const pages = [first.data, ...(older.question === question ? older.pages : [])].filter(
    (page): page is EntriesPage => !!page,
  );
  const entries = pages.flatMap((page) => page.entries);
  const next = pages[pages.length - 1]?.next ?? null;

  const loadOlder = async () => {
    if (!next) {
      return;
    }
    setLoadingMore(true);
    try {
      const page = await log.entries({
        range: data.range,
        sources: data.sources,
        order: 'desc',
        limit: pageSize,
        onlyRejected,
        batchPrefix: prefix.trim() || undefined,
        after: next,
      });
      setOlder((previous) => ({
        question,
        pages: [...(previous.question === question ? previous.pages : []), page],
      }));
    } finally {
      setLoadingMore(false);
    }
  };

  return (
    <Panel
      panel={panel}
      size={size}
      refreshing={first.refreshing}
      subtitle="One line per batch and source, newest first. Open a line for its refused records."
      actions={
        <div className={styles.logControls}>
          <label className={styles.search}>
            <Icon name="search" size={14} />
            <input
              type="search"
              value={prefix}
              placeholder="Batch id starts with…"
              aria-label="Batch id starts with"
              onChange={(event) => setPrefix(event.target.value)}
            />
          </label>
          <Switch checked={onlyRejected} onChange={setOnlyRejected}>
            Only with refusals
          </Switch>
        </div>
      }
    >
      <DataTable<LogEntry>
        caption="The batch log"
        rows={entries}
        rowKey={(row) => row.id}
        columns={[
          {
            key: 'time',
            header: 'Received',
            render: (row) => (
              <button
                type="button"
                className={styles.linkButton}
                onClick={() => data.openBatch(row)}
                title="Open the batch"
              >
                <span className={styles.timeCell}>{formats.dateTime(row.time)}</span>
              </button>
            ),
          },
          {
            key: 'source',
            header: 'Source',
            render: (row) => {
              const info = sourceInfo(data.catalog, row.source);
              return (
                <span className={styles.sourceName}>
                  <DomainDot domain={info.domain} />
                  {info.short}
                </span>
              );
            },
          },
          {
            key: 'batch',
            header: 'Batch',
            render: (row) => (
              <span className={styles.batchId} title={row.batchId}>
                {row.batchId}
              </span>
            ),
          },
          {
            key: 'received',
            header: 'Received',
            numeric: true,
            render: (row) => formatCount(row.received),
          },
          {
            key: 'written',
            header: 'Written',
            numeric: true,
            render: (row) => formatCount(row.new + row.changed),
          },
          {
            key: 'unchanged',
            header: 'Unchanged',
            numeric: true,
            render: (row) => (
              <span className={styles.muted}>{formatCount(row.unchanged + row.duplicates)}</span>
            ),
          },
          {
            key: 'refused',
            header: 'Refused',
            numeric: true,
            render: (row) =>
              row.rejected ? (
                <span className={styles.refused}>{formatCount(row.rejected)}</span>
              ) : (
                <span className={styles.muted}>0</span>
              ),
          },
          {
            key: 'duration',
            header: 'Duration',
            numeric: true,
            render: (row) => formatDuration(row.durationMs),
          },
        ]}
      />
      {entries.length === 0 && !first.loading ? (
        <p className={styles.note}>No batch matches in this period.</p>
      ) : null}
      {next ? (
        <div className={styles.more}>
          <button
            type="button"
            className={controls.button}
            onClick={() => void loadOlder()}
            disabled={loadingMore}
          >
            {loadingMore ? 'Loading…' : 'Load older batches'}
          </button>
        </div>
      ) : null}
    </Panel>
  );
}
