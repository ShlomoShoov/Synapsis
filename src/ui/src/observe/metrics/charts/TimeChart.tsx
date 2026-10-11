/**
 * A chart over time, in one of two forms:
 *
 * - "stacked": a column per interval, its parts stacked (outcomes, domains), a 2-pixel gap between parts and a
 *   rounded top on the highest part only;
 * - "lines": one 2-pixel line per series, with a faint wash under it when it is the only one.
 *
 * The pointer finds the interval (a crosshair snaps to it) and the tooltip lists every series there, value first.
 * The first and last intervals usually cover only part of the period; they are drawn lighter. One axis only.
 */
import { scaleLinear, scaleTime, scaleUtc } from 'd3-scale';
import { area as d3area, curveMonotoneX, line as d3line } from 'd3-shape';
import { useMemo, useState, type PointerEvent } from 'react';
import type { TimeRange } from '../../data/model';
import { DAY } from '../../lib/time';
import { useTimeFormats } from '../../lib/timeZone';
import { useElementSize } from '../../lib/useElementSize';
import styles from './charts.module.css';

/** One series of a time chart. */
export interface TimeSeries {
  key: string;
  label: string;
  color: string;
  /** One value per interval; null for "no value" (a gap in a line). */
  values: readonly (number | null)[];
  /** A lighter, thinner line beside the main one (p50 beside p95). */
  secondary?: boolean;
}

/** The settings of a time chart. */
export interface TimeChartProps {
  /** The start of each interval. */
  starts: readonly number[];
  intervalMs: number;
  /** The period the chart covers: intervals reaching outside it are partial. */
  range: TimeRange;
  series: readonly TimeSeries[];
  kind: 'stacked' | 'lines';
  /** Writes a value in the tooltip. */
  formatValue: (value: number) => string;
  /** Writes a value on the axis (shorter). */
  formatAxis: (value: number) => string;
  height?: number;
  /** The keys of series the reader turned off (from the legend). */
  hidden?: ReadonlySet<string>;
  /** A wash under a lone line. */
  area?: boolean;
  /** A note under the tooltip's values, such as a total. */
  total?: (index: number) => string | null;
}

const margin = { top: 12, right: 14, bottom: 26, left: 50 };

/** A rectangle with only its top corners rounded: the data end of a column. */
function roundedTop(x: number, y: number, width: number, height: number, radius: number): string {
  const r = Math.max(0, Math.min(radius, width / 2, height));
  return `M${x},${y + height}V${y + r}Q${x},${y} ${x + r},${y}H${x + width - r}Q${x + width},${y} ${x + width},${y + r}V${y + height}Z`;
}

/** A chart over time; see the file comment. */
export function TimeChart({
  starts,
  intervalMs,
  range,
  series,
  kind,
  formatValue,
  formatAxis,
  height = 230,
  hidden,
  area = false,
  total,
}: TimeChartProps) {
  const [box, setBox] = useState<HTMLDivElement | null>(null);
  const { width } = useElementSize(box);
  const formats = useTimeFormats();
  const [hover, setHover] = useState<number | null>(null);
  const shown = useMemo(() => series.filter((item) => !hidden?.has(item.key)), [series, hidden]);

  const geometry = useMemo(() => {
    if (width <= 0 || starts.length === 0) {
      return null;
    }
    const first = starts[0] as number;
    const last = (starts[starts.length - 1] as number) + intervalMs;
    const x = (formats.utc ? scaleUtc : scaleTime)()
      .domain([new Date(first), new Date(last)])
      .range([margin.left, width - margin.right]);
    const sums = starts.map((_, i) =>
      kind === 'stacked'
        ? shown.reduce((sum, item) => sum + (item.values[i] ?? 0), 0)
        : Math.max(0, ...shown.map((item) => item.values[i] ?? 0)),
    );
    const max = Math.max(1e-9, ...sums);
    const y = scaleLinear()
      .domain([0, max])
      .nice(4)
      .range([height - margin.bottom, margin.top]);
    const step = (width - margin.left - margin.right) / starts.length;
    const span = last - first;
    const xTicks = x.ticks(Math.max(2, Math.floor(width / 120))).map((date) => ({
      x: x(date),
      text: span > 2 * DAY ? formats.day(date.getTime()) : formats.minute(date.getTime()),
    }));
    return { x, y, step, xTicks, yTicks: y.ticks(4) };
  }, [width, starts, intervalMs, formats, shown, kind, height]);

  const onPointerMove = (event: PointerEvent<SVGSVGElement>) => {
    if (!geometry) {
      return;
    }
    const rect = event.currentTarget.getBoundingClientRect();
    const index = Math.floor((event.clientX - rect.left - margin.left) / geometry.step);
    setHover(index >= 0 && index < starts.length ? index : null);
  };

  const partial = (i: number) => {
    const start = starts[i] ?? 0;
    return start < range.from || start + intervalMs > range.to;
  };

  return (
    <div ref={setBox} className={styles.chart}>
      {geometry ? (
        <svg
          className={styles.svg}
          width={width}
          height={height}
          onPointerMove={onPointerMove}
          onPointerLeave={() => setHover(null)}
          aria-hidden="true"
        >
          {geometry.yTicks.map((tick) => (
            <g key={tick}>
              <line
                className={tick === 0 ? styles.baseline : styles.grid}
                x1={margin.left}
                x2={width - margin.right}
                y1={Math.round(geometry.y(tick)) + 0.5}
                y2={Math.round(geometry.y(tick)) + 0.5}
              />
              <text
                className={styles.axisLabel}
                x={margin.left - 10}
                y={geometry.y(tick) + 3.5}
                textAnchor="end"
              >
                {formatAxis(tick)}
              </text>
            </g>
          ))}
          {geometry.xTicks.map((tick) => (
            <text
              key={tick.x}
              className={styles.axisLabel}
              x={tick.x}
              y={height - 6}
              textAnchor="middle"
            >
              {tick.text}
            </text>
          ))}

          {hover !== null ? (
            <rect
              className={styles.hoverBand}
              x={margin.left + hover * geometry.step}
              y={margin.top}
              width={geometry.step}
              height={height - margin.top - margin.bottom}
            />
          ) : null}

          {kind === 'stacked' ? (
            <Columns starts={starts} series={shown} geometry={geometry} partial={partial} />
          ) : (
            <Lines
              starts={starts}
              intervalMs={intervalMs}
              series={shown}
              geometry={geometry}
              area={area}
            />
          )}

          {hover !== null && kind === 'lines' ? (
            <line
              className={styles.crosshair}
              x1={margin.left + (hover + 0.5) * geometry.step}
              x2={margin.left + (hover + 0.5) * geometry.step}
              y1={margin.top}
              y2={height - margin.bottom}
            />
          ) : null}
        </svg>
      ) : (
        <div style={{ height }} />
      )}

      {hover !== null && geometry ? (
        <ChartTooltip
          left={margin.left + (hover + 0.5) * geometry.step}
          width={width}
          title={`${formats.dateMinute(starts[hover] ?? 0)} – ${formats.minute((starts[hover] ?? 0) + intervalMs)}${partial(hover) ? ' (partial)' : ''}`}
          rows={[...shown].reverse().map((item) => ({
            key: item.key,
            color: item.color,
            shape: kind === 'stacked' ? ('bar' as const) : ('line' as const),
            value: item.values[hover] === null ? '—' : formatValue(item.values[hover] ?? 0),
            label: item.label,
            muted: !item.values[hover],
          }))}
          footer={total?.(hover) ?? null}
        />
      ) : null}
    </div>
  );
}

/** The geometry a chart's marks are drawn with. */
interface Geometry {
  x: (date: Date) => number;
  y: (value: number) => number;
  step: number;
}

function Columns({
  starts,
  series,
  geometry,
  partial,
}: {
  starts: readonly number[];
  series: readonly TimeSeries[];
  geometry: Geometry;
  partial: (index: number) => boolean;
}) {
  const barWidth = Math.max(1, Math.min(24, geometry.step - 2));
  return (
    <g>
      {starts.map((start, i) => {
        const x = margin.left + i * geometry.step + (geometry.step - barWidth) / 2;
        const parts: { key: string; color: string; top: number; bottom: number }[] = [];
        let total = 0;
        for (const item of series) {
          const value = item.values[i] ?? 0;
          if (value <= 0) {
            continue;
          }
          const bottom = geometry.y(total);
          total += value;
          const top = geometry.y(total);
          parts.push({
            key: item.key,
            color: item.color,
            top,
            bottom: parts.length > 0 ? bottom - 2 : bottom,
          });
        }
        return (
          <g key={start} className={partial(i) ? styles.partial : undefined}>
            {parts.map((part, index) => {
              const h = part.bottom - part.top;
              if (h < 0.75) {
                return null;
              }
              return index === parts.length - 1 ? (
                <path
                  key={part.key}
                  d={roundedTop(x, part.top, barWidth, h, 4)}
                  fill={part.color}
                />
              ) : (
                <rect
                  key={part.key}
                  x={x}
                  y={part.top}
                  width={barWidth}
                  height={h}
                  fill={part.color}
                />
              );
            })}
          </g>
        );
      })}
    </g>
  );
}

function Lines({
  starts,
  intervalMs,
  series,
  geometry,
  area,
}: {
  starts: readonly number[];
  intervalMs: number;
  series: readonly TimeSeries[];
  geometry: Geometry;
  area: boolean;
}) {
  const xOf = (i: number) => geometry.x(new Date((starts[i] ?? 0) + intervalMs / 2));
  return (
    <g>
      {series.map((item) => {
        const points = item.values.map((value, i) => ({ value, i }));
        const line = d3line<{ value: number | null; i: number }>()
          .defined((point) => point.value !== null)
          .x((point) => xOf(point.i))
          .y((point) => geometry.y(point.value ?? 0))
          .curve(curveMonotoneX);
        const wash = d3area<{ value: number | null; i: number }>()
          .defined((point) => point.value !== null)
          .x((point) => xOf(point.i))
          .y0(geometry.y(0))
          .y1((point) => geometry.y(point.value ?? 0))
          .curve(curveMonotoneX);
        let lastIndex = -1;
        item.values.forEach((value, i) => {
          if (value !== null) {
            lastIndex = i;
          }
        });
        // A value with gaps on both sides draws no line: it gets a small dot of its own, so it is not lost.
        const isolated = item.values.flatMap((value, i) =>
          value !== null &&
          i !== lastIndex &&
          (item.values[i - 1] ?? null) === null &&
          (item.values[i + 1] ?? null) === null
            ? [i]
            : [],
        );
        return (
          <g key={item.key}>
            {area && series.length === 1 ? (
              <path className={styles.area} d={wash(points) ?? ''} fill={item.color} />
            ) : null}
            <path
              className={item.secondary ? styles.lineSecondary : styles.line}
              d={line(points) ?? ''}
              stroke={item.color}
            />
            {isolated.map((i) => (
              <circle
                key={i}
                cx={xOf(i)}
                cy={geometry.y(item.values[i] ?? 0)}
                r={item.secondary ? 2 : 2.5}
                fill={item.color}
                opacity={item.secondary ? 0.6 : 1}
              />
            ))}
            {lastIndex >= 0 && !item.secondary ? (
              <circle
                className={styles.dot}
                cx={xOf(lastIndex)}
                cy={geometry.y(item.values[lastIndex] ?? 0)}
                r={4}
                fill={item.color}
              />
            ) : null}
          </g>
        );
      })}
    </g>
  );
}

/** One row of a tooltip. */
export interface TooltipRow {
  key: string;
  color: string;
  shape: 'bar' | 'line';
  value: string;
  label: string;
  muted?: boolean;
}

/** The tooltip of a chart, beside the hovered position (and to its left near the right edge). */
export function ChartTooltip({
  left,
  top = 8,
  width,
  title,
  rows,
  footer,
}: {
  left: number;
  top?: number;
  width: number;
  title: string;
  rows: readonly TooltipRow[];
  footer?: string | null;
}) {
  const flip = left > width - 230;
  return (
    <div
      className={styles.tooltip}
      style={{
        transform: `translate(${flip ? left - 16 : left + 16}px, ${top}px) translateX(${flip ? '-100%' : '0'})`,
      }}
    >
      <p className={styles.tooltipTitle}>{title}</p>
      <ul className={styles.tooltipRows}>
        {rows.map((row) => (
          <li key={row.key} className={styles.tooltipRow} data-muted={row.muted || undefined}>
            <span
              className={styles.tooltipKey}
              data-shape={row.shape}
              style={{ background: row.color }}
            />
            <span className={styles.tooltipValue}>{row.value}</span>
            <span className={styles.tooltipLabel}>{row.label}</span>
          </li>
        ))}
      </ul>
      {footer ? <p className={styles.tooltipTotal}>{footer}</p> : null}
    </div>
  );
}
