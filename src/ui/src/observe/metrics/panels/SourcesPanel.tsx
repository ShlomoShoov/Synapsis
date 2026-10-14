/**
 * Sources: one row per source, what it sent over the period and what became of it, and whether its feeder runs on
 * time (freshness.ts, measured over the last 30 days whatever the period). A source's name narrows every panel to it.
 */
import { useMemo } from 'react';
import { bySourceOrder, sourceInfo, type SourceInfo } from '../../data/catalog';
import { emptyCounts, writtenOf, type SourceStats } from '../../data/model';
import {
  formatAge,
  formatCount,
  formatDuration,
  formatPercent,
  formatSpan,
} from '../../lib/format';
import { DomainDot, OutcomeBar } from '../../shared/OutcomeBar';
import { DataTable } from '../charts/parts';
import { freshnessOf, type Freshness } from '../freshness';
import type { PanelInfo, PanelSize } from '../layout';
import styles from '../MetricsSection.module.css';
import { Panel } from '../Panel';
import type { MetricsData } from './common';

/** One row of the table. */
interface Row {
  info: SourceInfo;
  stats: SourceStats;
  freshness: Freshness;
}

/** The sources panel. */
export function SourcesPanel({
  panel,
  size,
  data,
}: {
  panel: PanelInfo;
  size: PanelSize;
  data: MetricsData;
}) {
  const rows = useMemo<Row[]>(() => {
    const stats = new Map((data.snapshot?.sources ?? []).map((item) => [item.source, item]));
    const history = new Map((data.history ?? []).map((item) => [item.source, item]));
    const wanted = data.sources ? new Set(data.sources) : null;
    const ids = new Set([...data.catalog.sources.map((source) => source.id), ...stats.keys()]);
    return [...ids]
      .filter((id) => !wanted || wanted.has(id))
      .map((id) => sourceInfo(data.catalog, id))
      .sort(bySourceOrder)
      .map((info) => ({
        info,
        stats: stats.get(info.id) ?? {
          source: info.id,
          ...emptyCounts(),
          entries: 0,
          batches: 0,
          first: 0,
          last: 0,
          durationAvg: null,
          codes: [],
        },
        freshness: freshnessOf(history.get(info.id), data.now),
      }));
  }, [data.snapshot, data.history, data.catalog, data.sources, data.now]);

  const max = Math.max(1, ...rows.map((row) => row.stats.received));
  const late = rows.filter(
    (row) => row.freshness.status === 'warning' || row.freshness.status === 'critical',
  ).length;

  return (
    <Panel
      panel={panel}
      size={size}
      refreshing={data.refreshing}
      subtitle={`${rows.filter((row) => row.stats.received > 0).length} of ${rows.length} sources sent records in this period${late ? `; ${late} running late or silent` : ''}.`}
    >
      <div className={styles.tableView} style={{ maxHeight: 'none' }}>
        <DataTable<Row>
          caption="Sources and what they sent"
          rows={rows}
          rowKey={(row) => row.info.id}
          initialSort={{ key: 'records', descending: true }}
          limit={12}
          noun="sources"
          columns={[
            {
              key: 'source',
              header: 'Source',
              sortValue: (row) => row.info.id,
              render: (row) => (
                <button
                  type="button"
                  className={`${styles.linkButton} ${styles.sourceCell}`}
                  onClick={() => data.focusSource(row.info.id)}
                  title="Show only this source"
                >
                  <span className={styles.sourceName}>
                    <DomainDot domain={row.info.domain} />
                    {row.info.short}
                  </span>
                  <span className={styles.sourceTitle}>{row.info.title ?? ''}</span>
                </button>
              ),
            },
            {
              key: 'records',
              header: 'Records',
              sortValue: (row) => row.stats.received,
              width: '22%',
              render: (row) => (
                <span className={styles.recordsCell}>
                  <span className={row.stats.received ? styles.recordsValue : styles.muted}>
                    {formatCount(row.stats.received)}
                  </span>
                  {row.stats.received > 0 ? (
                    <span style={{ width: `${Math.max(4, (row.stats.received / max) * 100)}%` }}>
                      <OutcomeBar counts={row.stats} height={5} />
                    </span>
                  ) : null}
                </span>
              ),
            },
            {
              key: 'written',
              header: 'Written',
              numeric: true,
              sortValue: (row) => writtenOf(row.stats),
              render: (row) =>
                row.stats.received ? (
                  <>
                    {formatCount(writtenOf(row.stats))}
                    <span className={styles.statusNote}>
                      {formatPercent(writtenOf(row.stats) / row.stats.received)}
                    </span>
                  </>
                ) : (
                  <span className={styles.muted}>—</span>
                ),
            },
            {
              key: 'refused',
              header: 'Refused',
              numeric: true,
              sortValue: (row) => row.stats.rejected,
              render: (row) =>
                row.stats.rejected ? (
                  <span className={styles.refused}>
                    {formatCount(row.stats.rejected)}
                    <span className={styles.statusNote}>
                      {formatPercent(row.stats.rejected / row.stats.received)}
                    </span>
                  </span>
                ) : (
                  <span className={styles.muted}>{row.stats.received ? '0' : '—'}</span>
                ),
            },
            {
              key: 'batches',
              header: 'Batches',
              numeric: true,
              sortValue: (row) => row.stats.batches,
              render: (row) =>
                row.stats.batches ? (
                  formatCount(row.stats.batches)
                ) : (
                  <span className={styles.muted}>—</span>
                ),
            },
            {
              key: 'duration',
              header: 'Avg. duration',
              numeric: true,
              sortValue: (row) => row.stats.durationAvg ?? -1,
              render: (row) =>
                row.stats.durationAvg === null ? (
                  <span className={styles.muted}>—</span>
                ) : (
                  formatDuration(row.stats.durationAvg)
                ),
            },
            {
              key: 'last',
              header: 'Last batch',
              sortValue: (row) => row.freshness.last ?? 0,
              render: (row) =>
                row.freshness.last ? (
                  formatAge(row.freshness.last, data.now)
                ) : (
                  <span className={styles.muted}>never</span>
                ),
            },
            {
              key: 'freshness',
              header: 'Runs',
              sortValue: (row) =>
                ['critical', 'warning', 'good', undefined].indexOf(row.freshness.status),
              render: (row) => (
                <span className={styles.status} data-status={row.freshness.status}>
                  <span className={styles.statusDot} aria-hidden="true" />
                  <span>
                    {row.freshness.label}
                    {row.freshness.cadenceMs ? (
                      <span className={styles.statusNote}>
                        every {formatSpan(row.freshness.cadenceMs)}
                      </span>
                    ) : null}
                  </span>
                </span>
              ),
            },
          ]}
        />
      </div>
    </Panel>
  );
}
