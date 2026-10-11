/**
 * A heatmap of sources over time: one row per source, one cell per interval, its light the records received then
 * (one hue, on a log scale so a quiet source still shows beside a busy one). It shows each feeder's rhythm at a glance:
 * the hourly earthquakes, the daily catalogues, the weekly releases, and the gaps.
 */
import { scaleTime, scaleUtc } from 'd3-scale';
import { useMemo, useState, type PointerEvent, type ReactNode } from 'react';
import { DAY } from '../../lib/time';
import { useTimeFormats } from '../../lib/timeZone';
import { useElementSize } from '../../lib/useElementSize';
import { sequential } from '../../theme/palette';
import styles from './charts.module.css';
import { ChartTooltip } from './TimeChart';

/** One row of the heatmap. */
export interface HeatRow {
  key: string;
  label: string;
  /** The colour of the row's dot (its domain). */
  color: string;
  values: readonly number[];
}

const labelWidth = 178;
const rowHeight = 18;
const axisHeight = 24;

/** The heatmap; see the file comment. */
export function Heatmap({
  rows,
  starts,
  intervalMs,
  formatValue,
  legend,
}: {
  rows: readonly HeatRow[];
  starts: readonly number[];
  intervalMs: number;
  formatValue: (value: number) => string;
  /** Drawn under the grid: the scale's legend. */
  legend?: ReactNode;
}) {
  const [box, setBox] = useState<HTMLDivElement | null>(null);
  const { width } = useElementSize(box);
  const formats = useTimeFormats();
  const [hover, setHover] = useState<{ row: number; column: number } | null>(null);
  const height = rows.length * rowHeight + axisHeight;
  const max = useMemo(() => Math.max(1, ...rows.flatMap((row) => row.values)), [rows]);
  const cellWidth = starts.length > 0 ? Math.max(1, (width - labelWidth) / starts.length) : 0;

  const ticks = useMemo(() => {
    if (width <= 0 || starts.length === 0) {
      return [];
    }
    const first = starts[0] as number;
    const last = (starts[starts.length - 1] as number) + intervalMs;
    const scale = (formats.utc ? scaleUtc : scaleTime)()
      .domain([new Date(first), new Date(last)])
      .range([labelWidth, width]);
    return scale.ticks(Math.max(2, Math.floor((width - labelWidth) / 120))).map((date) => ({
      x: scale(date),
      text: last - first > 2 * DAY ? formats.day(date.getTime()) : formats.minute(date.getTime()),
    }));
  }, [width, starts, intervalMs, formats]);

  const onPointerMove = (event: PointerEvent<SVGSVGElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    const x = event.clientX - rect.left - labelWidth;
    const row = Math.floor((event.clientY - rect.top) / rowHeight);
    const column = Math.floor(x / cellWidth);
    setHover(
      x >= 0 && row >= 0 && row < rows.length && column >= 0 && column < starts.length
        ? { row, column }
        : null,
    );
  };

  const hovered = hover ? rows[hover.row] : undefined;
  const hoveredStart = hover ? (starts[hover.column] ?? 0) : 0;

  return (
    <div ref={setBox} className={styles.chart}>
      {width > 0 ? (
        <svg
          className={styles.svg}
          width={width}
          height={height}
          onPointerMove={onPointerMove}
          onPointerLeave={() => setHover(null)}
          aria-hidden="true"
        >
          {rows.map((row, r) => {
            const quiet = row.values.every((value) => value === 0);
            return (
              <g key={row.key} transform={`translate(0, ${r * rowHeight})`}>
                <circle cx={6} cy={rowHeight / 2} r={3.5} fill={row.color} />
                <text
                  className={styles.rowLabel}
                  data-quiet={quiet || undefined}
                  x={18}
                  y={rowHeight / 2 + 3.8}
                >
                  {row.label}
                </text>
                {row.values.map((value, c) =>
                  value > 0 ? (
                    <rect
                      key={c}
                      className={styles.cell}
                      x={labelWidth + c * cellWidth}
                      y={1}
                      width={Math.max(1, cellWidth - 1)}
                      height={rowHeight - 2}
                      fill={sequential(0.18 + 0.82 * (Math.log1p(value) / Math.log1p(max)))}
                    />
                  ) : (
                    <rect
                      key={c}
                      className={styles.cellEmpty}
                      x={labelWidth + c * cellWidth}
                      y={1}
                      width={Math.max(1, cellWidth - 1)}
                      height={rowHeight - 2}
                    />
                  ),
                )}
              </g>
            );
          })}
          {hover ? (
            <rect
              className={styles.cellFocus}
              x={labelWidth + hover.column * cellWidth - 0.5}
              y={hover.row * rowHeight + 0.5}
              width={Math.max(2, cellWidth)}
              height={rowHeight - 1}
            />
          ) : null}
          {ticks.map((tick) => (
            <text
              key={tick.x}
              className={styles.axisLabel}
              x={tick.x}
              y={rows.length * rowHeight + 17}
              textAnchor="middle"
            >
              {tick.text}
            </text>
          ))}
        </svg>
      ) : (
        <div style={{ height }} />
      )}
      {hover && hovered ? (
        <ChartTooltip
          left={labelWidth + (hover.column + 0.5) * cellWidth}
          top={hover.row * rowHeight + rowHeight}
          width={width}
          title={`${formats.dateMinute(hoveredStart)} – ${formats.minute(hoveredStart + intervalMs)}`}
          rows={[
            {
              key: hovered.key,
              color: hovered.color,
              shape: 'bar',
              value: formatValue(hovered.values[hover.column] ?? 0),
              label: hovered.label,
            },
          ]}
        />
      ) : null}
      {legend}
    </div>
  );
}
