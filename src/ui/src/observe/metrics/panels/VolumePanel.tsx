/**
 * Records over time: what arrived in each interval, stacked by outcome (written, left alone, refused) or by domain.
 */
import { useMemo, useState } from 'react';
import { sourceInfo } from '../../data/catalog';
import { domains, type DomainId } from '../../domains';
import { formatCompact, formatCount, formatSpan } from '../../lib/format';
import { useTimeFormats } from '../../lib/timeZone';
import { outcomes } from '../../outcomes';
import { Segmented } from '../../ui/controls';
import { DataTable, Legend } from '../charts/parts';
import { TimeChart, type TimeSeries } from '../charts/TimeChart';
import { useHiddenSeries } from '../charts/useHiddenSeries';
import type { PanelInfo, PanelSize } from '../layout';
import styles from '../MetricsSection.module.css';
import { Panel } from '../Panel';
import type { MetricsData } from './common';

/** The records over time panel. */
export function VolumePanel({
  panel,
  size,
  data,
}: {
  panel: PanelInfo;
  size: PanelSize;
  data: MetricsData;
}) {
  const formats = useTimeFormats();
  const [by, setBy] = useState<'outcome' | 'domain'>('outcome');
  const [hidden, toggle] = useHiddenSeries();
  const buckets = useMemo(() => data.snapshot?.buckets ?? [], [data.snapshot]);
  const starts = useMemo(() => buckets.map((bucket) => bucket.start), [buckets]);

  const series = useMemo<TimeSeries[]>(() => {
    if (by === 'outcome') {
      return outcomes.map((item) => ({
        key: item.key,
        label: item.label,
        color: item.color,
        values: buckets.map((bucket) => bucket[item.key]),
      }));
    }
    const domainOf = (source: string): DomainId => sourceInfo(data.catalog, source).domain;
    return domains
      .map((domain) => ({
        key: domain.id,
        label: domain.name,
        color: domain.color,
        values: buckets.map((bucket) =>
          Object.entries(bucket.bySource).reduce(
            (sum, [source, received]) => sum + (domainOf(source) === domain.id ? received : 0),
            0,
          ),
        ),
      }))
      .filter((item) => item.values.some((value) => value > 0));
  }, [by, buckets, data.catalog]);

  const totals = (index: number) =>
    `Total ${formatCount(series.filter((item) => !hidden.has(item.key)).reduce((sum, item) => sum + (item.values[index] ?? 0), 0))}`;

  return (
    <Panel
      panel={panel}
      size={size}
      refreshing={data.refreshing}
      subtitle={`Records received in each ${formatSpan(data.intervalMs)}, ${by === 'outcome' ? 'by what became of them' : 'by domain'}.`}
      actions={
        <Segmented
          small
          label="Group by"
          value={by}
          onChange={setBy}
          options={[
            { value: 'outcome', label: 'Outcome' },
            { value: 'domain', label: 'Domain' },
          ]}
        />
      }
      table={
        <DataTable
          caption="Records received per interval"
          columns={[
            {
              key: 'time',
              header: 'Interval',
              sortValue: (row: number) => starts[row] ?? 0,
              render: (row: number) => (
                <span className={styles.timeCell}>{formats.dateMinute(starts[row] ?? 0)}</span>
              ),
            },
            ...series.map((item) => ({
              key: item.key,
              header: item.label,
              numeric: true,
              sortValue: (row: number) => item.values[row] ?? 0,
              render: (row: number) => formatCount(item.values[row] ?? 0),
            })),
          ]}
          rows={starts.map((_, index) => index).reverse()}
          rowKey={(row) => String(row)}
        />
      }
    >
      <TimeChart
        kind="stacked"
        starts={starts}
        intervalMs={data.intervalMs}
        range={data.range}
        series={series}
        hidden={hidden}
        formatValue={formatCount}
        formatAxis={formatCompact}
        total={totals}
        height={250}
      />
      <div className={styles.legendRow}>
        <Legend
          items={series.map((item) => ({
            key: item.key,
            label: item.label,
            color: item.color,
            shape: 'bar',
          }))}
          hidden={hidden}
          onToggle={toggle}
        />
      </div>
    </Panel>
  );
}
