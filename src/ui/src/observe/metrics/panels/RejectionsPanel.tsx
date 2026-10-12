/**
 * Rejections: which rules refused records, from which sources, and the latest refused records themselves (the log
 * keeps the first 50 of each batch), each opening its batch.
 */
import { sourceInfo } from '../../data/catalog';
import { errorCodeInfo } from '../../data/errorCodes';
import type { LogEntry, LoggedRejection } from '../../data/model';
import { useObserveData } from '../../data/observeContext';
import { useResource } from '../../data/useResource';
import { formatCount, formatPercent } from '../../lib/format';
import { useTimeFormats } from '../../lib/timeZone';
import { statusColors } from '../../outcomes';
import { DomainDot } from '../../shared/OutcomeBar';
import { Icon } from '../../ui/Icon';
import { BarList, DataTable } from '../charts/parts';
import type { PanelInfo, PanelSize } from '../layout';
import styles from '../MetricsSection.module.css';
import { Panel } from '../Panel';
import type { MetricsData } from './common';

/** One refused record of the latest batches, with its batch. */
interface Sample {
  key: string;
  entry: LogEntry;
  rejection: LoggedRejection;
}

/** The rejections panel. */
export function RejectionsPanel({
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
  const snapshot = data.snapshot;
  const refused = snapshot?.totals.rejected ?? 0;

  const latest = useResource(`rejections:${data.scope}`, (signal) =>
    log.entries(
      { range: data.range, sources: data.sources, order: 'desc', limit: 8, onlyRejected: true },
      signal,
    ),
  );
  const samples: Sample[] = (latest.data?.entries ?? [])
    .flatMap((entry) => entry.rejections.slice(0, 2).map((rejection) => ({ entry, rejection })))
    .slice(0, 10)
    .map((item) => ({ ...item, key: `${item.entry.id}-${item.rejection.index}` }));

  const sources = (snapshot?.sources ?? [])
    .filter((item) => item.rejected > 0)
    .sort((a, b) => b.rejected - a.rejected)
    .slice(0, 8);

  return (
    <Panel
      panel={panel}
      size={size}
      refreshing={data.refreshing}
      subtitle={
        snapshot
          ? refused
            ? `${formatCount(refused)} records refused, ${formatPercent(refused / Math.max(1, snapshot.totals.received))} of those received.`
            : 'Nothing was refused in this period.'
          : undefined
      }
    >
      {snapshot && refused === 0 ? (
        <p className={styles.note} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Icon name="good" style={{ color: statusColors.good }} />
          Every record of this period passed the rules.
        </p>
      ) : (
        <>
          <div className={styles.rejectionsGrid}>
            <div>
              <h4 className={styles.subhead}>By error code</h4>
              <BarList
                items={(snapshot?.codes ?? []).slice(0, 8).map((code) => {
                  const meaning = errorCodeInfo(code.code);
                  return {
                    key: code.code,
                    label: <span className={styles.codeLabel}>{code.code}</span>,
                    value: code.count,
                    display: formatCount(code.count),
                    note: formatPercent(code.count / Math.max(1, refused)),
                    color: statusColors.critical,
                    title: `${meaning.rule ? `${meaning.rule}. ` : ''}${meaning.text}`,
                  };
                })}
              />
            </div>
            <div>
              <h4 className={styles.subhead}>By source</h4>
              <BarList
                items={sources.map((item) => {
                  const info = sourceInfo(data.catalog, item.source);
                  return {
                    key: item.source,
                    label: (
                      <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                        <DomainDot domain={info.domain} />
                        {info.short}
                      </span>
                    ),
                    value: item.rejected,
                    display: formatCount(item.rejected),
                    note: `${formatPercent(item.rejected / Math.max(1, item.received))} of its records`,
                    color: statusColors.critical,
                    title: item.codes
                      .map((code) => `${code.code} ${formatCount(code.count)}`)
                      .join(', '),
                  };
                })}
                onSelect={data.focusSource}
              />
            </div>
          </div>

          <div className={styles.samples}>
            <h4 className={styles.subhead}>Latest refused records</h4>
            <DataTable<Sample>
              caption="The latest refused records"
              rows={samples}
              rowKey={(row) => row.key}
              columns={[
                {
                  key: 'time',
                  header: 'Received',
                  render: (row) => (
                    <button
                      type="button"
                      className={styles.linkButton}
                      onClick={() => data.openBatch(row.entry)}
                      title="Open the batch"
                    >
                      <span className={styles.timeCell}>{formats.dateTime(row.entry.time)}</span>
                    </button>
                  ),
                },
                {
                  key: 'source',
                  header: 'Source',
                  render: (row) => {
                    const info = sourceInfo(data.catalog, row.entry.source);
                    return (
                      <span className={styles.sourceName}>
                        <DomainDot domain={info.domain} />
                        {info.short}
                      </span>
                    );
                  },
                },
                {
                  key: 'record',
                  header: 'Record',
                  render: (row) => (
                    <span className={styles.batchId} title={row.rejection.id ?? undefined}>
                      {row.rejection.id ?? 'no readable id'}
                    </span>
                  ),
                },
                {
                  key: 'code',
                  header: 'Code',
                  render: (row) => (
                    <span className={`${styles.timeCell} ${styles.codeLabel}`}>
                      {row.rejection.code}
                    </span>
                  ),
                },
                {
                  key: 'path',
                  header: 'Reason',
                  render: (row) => (
                    <span className={styles.timeCell}>{row.rejection.reason || '—'}</span>
                  ),
                },
              ]}
            />
          </div>
        </>
      )}
    </Panel>
  );
}
