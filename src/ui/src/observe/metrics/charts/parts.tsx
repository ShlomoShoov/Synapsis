/**
 * The smaller pieces of the dashboards: a legend whose items turn their series on and off, a list of horizontal
 * bars, a sparkline, and a sortable table (the table view of every chart, and the sources and batch tables).
 */
import { line as d3line, area as d3area, curveMonotoneX } from 'd3-shape';
import { useMemo, useState, type ReactNode } from 'react';
import { Icon } from '../../ui/Icon';
import styles from './charts.module.css';

/** One item of a legend. */
export interface LegendItem {
  key: string;
  label: string;
  color: string;
  /** A swatch for bars and areas, a short stroke for lines. */
  shape: 'bar' | 'line';
}

/** A legend: each item shows its colour and name, and turns its series on and off when pressed. */
export function Legend({
  items,
  hidden,
  onToggle,
}: {
  items: readonly LegendItem[];
  hidden: ReadonlySet<string>;
  onToggle: (key: string) => void;
}) {
  return (
    <ul className={styles.legend} aria-label="Series">
      {items.map((item) => (
        <li key={item.key}>
          <button
            type="button"
            className={styles.legendItem}
            aria-pressed={!hidden.has(item.key)}
            onClick={() => onToggle(item.key)}
            title={hidden.has(item.key) ? `Show ${item.label}` : `Hide ${item.label}`}
          >
            <span
              className={styles.legendSwatch}
              data-shape={item.shape}
              style={{ background: item.color }}
            />
            {item.label}
          </button>
        </li>
      ))}
    </ul>
  );
}

/** One row of a bar list. */
export interface BarItem {
  key: string;
  label: ReactNode;
  value: number;
  /** The value as written at the end of the row. */
  display: string;
  /** A smaller line under the value. */
  note?: string;
  color: string;
  /** A description for the row's title (shown on hover). */
  title?: string;
}

/** Horizontal bars, longest first, the value written at the end of each row. */
export function BarList({
  items,
  onSelect,
}: {
  items: readonly BarItem[];
  onSelect?: (key: string) => void;
}) {
  const max = Math.max(1, ...items.map((item) => item.value));
  return (
    <ul className={styles.barList}>
      {items.map((item) => (
        <li key={item.key} className={styles.barRow} title={item.title}>
          <span className={styles.barLabel}>
            {onSelect ? (
              <button type="button" className={styles.barButton} onClick={() => onSelect(item.key)}>
                {item.label}
              </button>
            ) : (
              item.label
            )}
          </span>
          <span className={styles.barTrack} aria-hidden="true">
            <span
              className={styles.barFill}
              style={{ width: `${(item.value / max) * 100}%`, background: item.color }}
            />
          </span>
          <span className={styles.barValue}>
            {item.display}
            {item.note ? <span className={styles.barNote}>{item.note}</span> : null}
          </span>
        </li>
      ))}
    </ul>
  );
}

/** A small line of recent values, the last one marked. */
export function Sparkline({
  values,
  width = 132,
  height = 30,
}: {
  values: readonly number[];
  width?: number;
  height?: number;
}) {
  const shape = useMemo(() => {
    if (values.length < 2) {
      return null;
    }
    const max = Math.max(1e-9, ...values);
    const x = (i: number) => (i / (values.length - 1)) * (width - 6) + 3;
    const y = (v: number) => height - 3 - (v / max) * (height - 6);
    const points = values.map((value, i) => [x(i), y(value)] as [number, number]);
    const line = d3line().curve(curveMonotoneX)(points) ?? '';
    const area =
      d3area()
        .y0(height - 3)
        .curve(curveMonotoneX)(points) ?? '';
    const last = points[points.length - 1] as [number, number];
    return { line, area, last };
  }, [values, width, height]);
  if (!shape) {
    return <svg className={styles.sparkline} width={width} height={height} aria-hidden="true" />;
  }
  return (
    <svg className={styles.sparkline} width={width} height={height} aria-hidden="true">
      <path className={styles.sparkArea} d={shape.area} />
      <path className={styles.sparkLine} d={shape.line} />
      <circle className={styles.sparkDot} cx={shape.last[0]} cy={shape.last[1]} r={3} />
    </svg>
  );
}

/** One column of a data table. */
export interface Column<Row> {
  key: string;
  header: string;
  /** Right-aligned, for numbers. */
  numeric?: boolean;
  /** The value rows are sorted by; a column without one cannot be sorted. */
  sortValue?: (row: Row) => number | string;
  render: (row: Row) => ReactNode;
  width?: number | string;
}

/**
 * A table whose columns sort when their header is pressed (press again to reverse). A row that opens something does
 * it through a button in one of its cells, so the keyboard reaches it too. With a `limit`, only the first rows of the
 * current order show until the rest are asked for.
 */
export function DataTable<Row>({
  columns,
  rows,
  rowKey,
  initialSort,
  caption,
  limit,
  noun = 'rows',
}: {
  columns: readonly Column<Row>[];
  rows: readonly Row[];
  rowKey: (row: Row) => string;
  initialSort?: { key: string; descending: boolean };
  /** Read by screen readers: what the table holds. */
  caption: string;
  /** How many rows show before "Show all". */
  limit?: number;
  /** What the rows are, for the "Show all" button: "sources". */
  noun?: string;
}) {
  const [sort, setSort] = useState(initialSort ?? null);
  const [expanded, setExpanded] = useState(false);
  const sorted = useMemo(() => {
    const column = columns.find((item) => item.key === sort?.key);
    if (!sort || !column?.sortValue) {
      return rows;
    }
    const value = column.sortValue;
    return [...rows].sort((a, b) => {
      const left = value(a);
      const right = value(b);
      const order =
        typeof left === 'number' && typeof right === 'number'
          ? left - right
          : String(left).localeCompare(String(right));
      return sort.descending ? -order : order;
    });
  }, [rows, columns, sort]);
  const cut = limit !== undefined && rows.length > limit + 2;
  const shown = cut && !expanded ? sorted.slice(0, limit) : sorted;

  return (
    <>
      <table className={styles.table}>
        <caption
          style={{
            position: 'absolute',
            width: 1,
            height: 1,
            overflow: 'hidden',
            clipPath: 'inset(50%)',
          }}
        >
          {caption}
        </caption>
        <thead>
          <tr>
            {columns.map((column) => {
              const active = sort?.key === column.key;
              return (
                <th
                  key={column.key}
                  scope="col"
                  data-numeric={column.numeric || undefined}
                  style={{ width: column.width }}
                  aria-sort={active ? (sort?.descending ? 'descending' : 'ascending') : undefined}
                >
                  {column.sortValue ? (
                    <button
                      type="button"
                      className={styles.sortButton}
                      onClick={() =>
                        setSort({
                          key: column.key,
                          descending: active ? !sort?.descending : column.numeric === true,
                        })
                      }
                    >
                      {column.header}
                      {active ? (
                        <Icon
                          name="chevronDown"
                          size={11}
                          style={{ transform: sort?.descending ? undefined : 'rotate(180deg)' }}
                        />
                      ) : null}
                    </button>
                  ) : (
                    column.header
                  )}
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {shown.map((row) => (
            <tr key={rowKey(row)}>
              {columns.map((column) => (
                <td key={column.key} data-numeric={column.numeric || undefined}>
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {cut ? (
        <button
          type="button"
          className={styles.more}
          aria-expanded={expanded}
          onClick={() => setExpanded(!expanded)}
        >
          {expanded ? `Show the first ${limit}` : `Show all ${rows.length} ${noun}`}
          <Icon
            name="chevronDown"
            size={13}
            style={{ transform: expanded ? 'rotate(180deg)' : undefined }}
          />
        </button>
      ) : null}
    </>
  );
}
