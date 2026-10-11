/**
 * Two panels about pace: throughput (records or requests per second, each interval measured over the part of it
 * inside the period) and batch duration (the median and the slowest 5% of batches, per interval).
 */
import { useMemo, useState } from 'react';
import { formatCompact, formatDuration, formatRate, formatSpan } from '../../lib/format';
import { useTimeFormats } from '../../lib/timeZone';
import { outcome } from '../../outcomes';
import { Segmented } from '../../ui/controls';
import { DataTable, Legend } from '../charts/parts';
import { TimeChart, type TimeSeries } from '../charts/TimeChart';
import { useHiddenSeries } from '../charts/useHiddenSeries';
import type { PanelInfo, PanelSize } from '../layout';
import styles from '../MetricsSection.module.css';
import { Panel } from '../Panel';
import { ratePerSecond, type MetricsData } from './common';

/** A lone line's colour: the light of written records. */
const lightLine = outcome('new').color;

/** The throughput panel. */
export function ThroughputPanel({
  panel,
  size,
  data,
}: {
  panel: PanelInfo;
  size: PanelSize;
  data: MetricsData;
}) {
  const formats = useTimeFormats();
  const [of, setOf] = useState<'records' | 'requests'>('records');
  const buckets = useMemo(() => data.snapshot?.buckets ?? [], [data.snapshot]);
  const starts = useMemo(() => buckets.map((bucket) => bucket.start), [buckets]);
  const values = useMemo(
    () =>
      ratePerSecond(buckets, data.intervalMs, data.range, (bucket) =>
        of === 'records' ? bucket.received : bucket.batches,
      ),
    [buckets, data.intervalMs, data.range, of],
  );
  const peak = Math.max(0, ...values.map((value) => value ?? 0));
  const label = of === 'records' ? 'Records per second' : 'Requests per second';

  return (
    <Panel
      panel={panel}
      size={size}
      refreshing={data.refreshing}
      subtitle={`${label}, averaged over each ${formatSpan(data.intervalMs)}; the busiest: ${formatRate(peak)}.`}
      actions={
        <Segmented
          small
          label="Measure"
          value={of}
          onChange={setOf}
          options={[
            { value: 'records', label: 'Records' },
            { value: 'requests', label: 'Requests' },
          ]}
        />
      }
      table={
        <DataTable
          caption={label}
          columns={[
            {
              key: 'time',
              header: 'Interval',
              sortValue: (row: number) => starts[row] ?? 0,
              render: (row: number) => (
                <span className={styles.timeCell}>{formats.dateMinute(starts[row] ?? 0)}</span>
              ),
            },
            {
              key: 'rate',
              header: label,
              numeric: true,
              sortValue: (row: number) => values[row] ?? 0,
              render: (row: number) => (values[row] === null ? '—' : formatRate(values[row] ?? 0)),
            },
          ]}
          rows={starts.map((_, index) => index).reverse()}
          rowKey={(row) => String(row)}
        />
      }
    >
      <TimeChart
        kind="lines"
        area
        starts={starts}
        intervalMs={data.intervalMs}
        range={data.range}
        series={[{ key: of, label, color: lightLine, values }]}
        formatValue={formatRate}
        formatAxis={(value) =>
          value >= 1000 ? formatCompact(value) : String(Number(value.toPrecision(3)))
        }
        height={200}
      />
    </Panel>
  );
}

/** The batch duration panel. */
export function LatencyPanel({
  panel,
  size,
  data,
}: {
  panel: PanelInfo;
  size: PanelSize;
  data: MetricsData;
}) {
  const formats = useTimeFormats();
  const [hidden, toggle] = useHiddenSeries();
  const buckets = useMemo(() => data.snapshot?.buckets ?? [], [data.snapshot]);
  const starts = useMemo(() => buckets.map((bucket) => bucket.start), [buckets]);
  const series = useMemo<TimeSeries[]>(
    () => [
      {
        key: 'p95',
        label: 'Slowest 5% (p95)',
        color: lightLine,
        values: buckets.map((bucket) => bucket.durationP95),
      },
      {
        key: 'p50',
        label: 'Median (p50)',
        color: outcome('changed').color,
        values: buckets.map((bucket) => bucket.durationP50),
        secondary: true,
      },
    ],
    [buckets],
  );

  return (
    <Panel
      panel={panel}
      size={size}
      refreshing={data.refreshing}
      subtitle={`How long the API took per batch, in each ${formatSpan(data.intervalMs)}.`}
      table={
        <DataTable
          caption="Batch duration per interval"
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
              sortValue: (row: number) => item.values[row] ?? -1,
              render: (row: number) => formatDuration(item.values[row]),
            })),
          ]}
          rows={starts.map((_, index) => index).reverse()}
          rowKey={(row) => String(row)}
        />
      }
    >
      <TimeChart
        kind="lines"
        starts={starts}
        intervalMs={data.intervalMs}
        range={data.range}
        series={series}
        hidden={hidden}
        formatValue={formatDuration}
        formatAxis={(value) => formatDuration(value).replace(' ', '')}
        height={200}
      />
      <div className={styles.legendRow}>
        <Legend
          items={series.map((item) => ({
            key: item.key,
            label: item.label,
            color: item.color,
            shape: 'line',
          }))}
          hidden={hidden}
          onToggle={toggle}
        />
      </div>
    </Panel>
  );
}
