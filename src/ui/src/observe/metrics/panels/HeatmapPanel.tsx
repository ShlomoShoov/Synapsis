/**
 * Activity by source: each source's records over the period, one cell per interval. Feeder rhythms show at a glance
 * (an hourly feeder is a dotted line, a weekly one a few marks), and so do the gaps.
 */
import { useMemo } from 'react';
import { bySourceOrder, sourceInfo } from '../../data/catalog';
import { domainById } from '../../domains';
import { formatCount, formatSpan } from '../../lib/format';
import { useTimeFormats } from '../../lib/timeZone';
import { sequential } from '../../theme/palette';
import { Heatmap, type HeatRow } from '../charts/Heatmap';
import { DataTable } from '../charts/parts';
import type { PanelInfo, PanelSize } from '../layout';
import styles from '../MetricsSection.module.css';
import { Panel } from '../Panel';
import type { MetricsData } from './common';

/** The activity panel. */
export function HeatmapPanel({
  panel,
  size,
  data,
}: {
  panel: PanelInfo;
  size: PanelSize;
  data: MetricsData;
}) {
  const formats = useTimeFormats();
  const buckets = useMemo(() => data.snapshot?.buckets ?? [], [data.snapshot]);
  const starts = useMemo(() => buckets.map((bucket) => bucket.start), [buckets]);
  const rows = useMemo<HeatRow[]>(() => {
    const seen = new Set(buckets.flatMap((bucket) => Object.keys(bucket.bySource)));
    const wanted = data.sources ? new Set(data.sources) : null;
    const ids = new Set([...data.catalog.sources.map((source) => source.id), ...seen]);
    return [...ids]
      .filter((id) => !wanted || wanted.has(id))
      .map((id) => sourceInfo(data.catalog, id))
      .sort(bySourceOrder)
      .map((info) => ({
        key: info.id,
        label: info.short,
        color: domainById(info.domain).color,
        values: buckets.map((bucket) => bucket.bySource[info.id] ?? 0),
      }));
  }, [buckets, data.catalog, data.sources]);

  return (
    <Panel
      panel={panel}
      size={size}
      refreshing={data.refreshing}
      subtitle={`Records each source sent, per ${formatSpan(data.intervalMs)}; brighter is more (on a log scale).`}
      table={
        <DataTable<HeatRow>
          caption="Records per source and interval"
          rows={rows}
          rowKey={(row) => row.key}
          columns={[
            {
              key: 'source',
              header: 'Source',
              sortValue: (row) => row.label,
              render: (row) => <span className={styles.timeCell}>{row.label}</span>,
            },
            {
              key: 'total',
              header: 'Records',
              numeric: true,
              sortValue: (row) => row.values.reduce((sum, value) => sum + value, 0),
              render: (row) => formatCount(row.values.reduce((sum, value) => sum + value, 0)),
            },
            {
              key: 'busiest',
              header: 'Busiest interval',
              render: (row) => {
                const peak = Math.max(...row.values);
                const index = row.values.indexOf(peak);
                return peak > 0
                  ? `${formats.dateMinute(starts[index] ?? 0)} (${formatCount(peak)})`
                  : '—';
              },
            },
          ]}
        />
      }
    >
      <Heatmap
        rows={rows}
        starts={starts}
        intervalMs={data.intervalMs}
        formatValue={(value) => `${formatCount(value)} records`}
        legend={
          <div className={styles.legendRow} style={{ justifyContent: 'flex-end' }}>
            <span
              className={styles.note}
              style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 10 }}
            >
              fewer
              <span
                aria-hidden="true"
                style={{
                  width: 120,
                  height: 8,
                  borderRadius: 2,
                  background: `linear-gradient(90deg, ${sequential(0.18)}, ${sequential(0.6)}, ${sequential(1)})`,
                }}
              />
              more
            </span>
          </div>
        }
      />
    </Panel>
  );
}
