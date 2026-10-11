/**
 * Key figures: the six numbers to read first. Records received, records written, the share refused, requests
 * (batches), the busiest second, and the slowest batches; each with its change from the period before and a small
 * line of its course over the period.
 */
import type { ReactNode } from 'react';
import { writtenOf } from '../../data/model';
import {
  formatCompact,
  formatCount,
  formatDuration,
  formatPercent,
  formatRate,
  formatSpan,
  formatSpanWords,
} from '../../lib/format';
import { useTimeFormats } from '../../lib/timeZone';
import type { Status } from '../../outcomes';
import { Icon } from '../../ui/Icon';
import { Sparkline } from '../charts/parts';
import type { PanelInfo, PanelSize } from '../layout';
import styles from '../MetricsSection.module.css';
import { Panel } from '../Panel';
import {
  pointChange,
  ratePerSecond,
  relativeChange,
  secondsOf,
  type Change,
  type MetricsData,
} from './common';

/** One tile. */
interface Figure {
  label: string;
  value: ReactNode;
  /** A unit after the value, smaller: "/s". */
  unit?: string;
  /** A state worth a mark beside the value, with the words that say why. */
  status?: { level: Status; text: string };
  change: Change | null;
  /** The line under the value when there is nothing to compare. */
  aside?: string;
  /** A smaller line under that. */
  note: string;
  spark: readonly number[];
}

/** The refusal share from which the figure is marked. */
const refusalWarning = 0.01;
const refusalCritical = 0.05;

/** A count in full up to a hundred thousand, in short beyond. */
const count = (value: number) => (value < 100_000 ? formatCount(value) : formatCompact(value));

/** The key figures panel. */
export function FiguresPanel({
  panel,
  size,
  data,
}: {
  panel: PanelInfo;
  size: PanelSize;
  data: MetricsData;
}) {
  const { snapshot, previous, range, intervalMs } = data;
  const formats = useTimeFormats();
  const bounds = `${formats.dateMinute(range.from)} – ${formats.dateMinute(range.to)}`;
  if (!snapshot) {
    return (
      <Panel panel={panel} size={size} refreshing>
        <div className={styles.figures} style={{ minHeight: 132 }} />
      </Panel>
    );
  }
  const totals = snapshot.totals;
  const buckets = snapshot.buckets;
  const written = writtenOf(totals);
  const refusedShare = totals.received ? totals.rejected / totals.received : 0;
  const previousShare =
    previous && previous.received ? previous.rejected / previous.received : undefined;
  const seconds = Math.max(1, (range.to - range.from) / 1000);
  const rates = ratePerSecond(buckets, intervalMs, range, (bucket) => bucket.received).map(
    (rate) => rate ?? 0,
  );
  const peak = Math.max(0, ...rates);
  const requestRates = buckets.map((bucket) => {
    const span = secondsOf(bucket, intervalMs, range);
    return span > 0 ? bucket.batches / span : 0;
  });

  const figures: Figure[] = [
    {
      label: 'Records received',
      value: count(totals.received),
      change: relativeChange(totals.received, previous?.received),
      note: `${formatCount(totals.entries)} log entries`,
      spark: buckets.map((bucket) => bucket.received),
    },
    {
      label: 'Written',
      value: count(written),
      change: relativeChange(written, previous ? writtenOf(previous) : undefined),
      note: totals.received
        ? `${formatPercent(written / totals.received)} of received`
        : 'nothing received',
      spark: buckets.map((bucket) => bucket.new + bucket.changed),
    },
    {
      label: 'Refused',
      value: totals.received ? formatPercent(refusedShare) : '—',
      status:
        refusedShare >= refusalCritical
          ? { level: 'critical', text: `over ${formatPercent(refusalCritical)}` }
          : refusedShare >= refusalWarning
            ? { level: 'warning', text: `over ${formatPercent(refusalWarning)}` }
            : undefined,
      change: pointChange(refusedShare, previousShare),
      note: `${formatCount(totals.rejected)} records`,
      spark: buckets.map((bucket) => (bucket.received ? bucket.rejected / bucket.received : 0)),
    },
    {
      label: 'Requests',
      value: count(totals.batches),
      change: relativeChange(totals.batches, previous?.batches),
      note: `${formatRate(totals.batches / seconds)} on average`,
      spark: requestRates,
    },
    {
      label: 'Peak throughput',
      value: formatRate(peak).replace('/s', ''),
      unit: 'records/s',
      change: null,
      aside: `in the busiest ${formatSpan(intervalMs)}`,
      note: `${formatRate(totals.received / seconds)} on average`,
      spark: rates,
    },
    {
      label: 'Batch duration p95',
      value: formatDuration(totals.duration.p95),
      change: relativeChange(
        totals.duration.p95 ?? 0,
        previous ? previous.duration.p95 : undefined,
        true,
      ),
      note: `median ${formatDuration(totals.duration.p50)}`,
      spark: buckets.map((bucket) => bucket.durationP95 ?? 0),
    },
  ];

  return (
    <Panel
      panel={panel}
      size={size}
      refreshing={data.refreshing}
      subtitle={
        previous
          ? `${bounds}, against the ${formatSpanWords(range.to - range.from)} before.`
          : `${bounds}.`
      }
    >
      <dl className={styles.figures}>
        {figures.map((figure) => (
          <div key={figure.label} className={styles.figure}>
            <dt className={styles.figureLabel}>{figure.label}</dt>
            <dd className={styles.figureValue}>
              {figure.value}
              {figure.unit ? <span className={styles.figureUnit}>{figure.unit}</span> : null}
              {figure.status ? (
                <span className={styles.figureStatus} data-status={figure.status.level}>
                  <Icon name={figure.status.level === 'critical' ? 'alert' : 'warning'} size={13} />
                  {figure.status.text}
                </span>
              ) : null}
            </dd>
            <dd className={styles.delta} data-tone={figure.change?.tone}>
              {figure.change ? (
                <>
                  <strong>{figure.change.text}</strong> {figure.change.rest}
                </>
              ) : (
                (figure.aside ?? ' ')
              )}
            </dd>
            <dd className={styles.figureNote}>{figure.note}</dd>
            <dd className={styles.figureSpark}>
              <Sparkline values={figure.spark} />
            </dd>
          </div>
        ))}
      </dl>
    </Panel>
  );
}
