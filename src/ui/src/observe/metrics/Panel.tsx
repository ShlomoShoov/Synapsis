/**
 * The frame of a dashboard panel: its title and one line on what it answers, its own controls on the right, and a
 * switch between the chart and its table (the same numbers, readable without the chart). While new numbers load,
 * the previous ones stay, dimmed, so nothing jumps.
 *
 * The frame carries data-nav-anchor, so the sidebar lists the panel under Metrics.
 */
import { useState, type ReactNode } from 'react';
import { Icon } from '../ui/Icon';
import type { PanelInfo, PanelSize } from './layout';
import styles from './MetricsSection.module.css';

/** A dashboard panel. */
export function Panel({
  panel,
  size,
  subtitle,
  actions,
  table,
  refreshing = false,
  children,
}: {
  panel: PanelInfo;
  size: PanelSize;
  /** One line under the title, specific to what is shown (for example the interval). */
  subtitle?: ReactNode;
  /** Controls on the right of the title. */
  actions?: ReactNode;
  /** The table view of the chart; without one, no switch is shown. */
  table?: ReactNode;
  refreshing?: boolean;
  children: ReactNode;
}) {
  const [asTable, setAsTable] = useState(false);
  return (
    <section
      id={`metrics-${panel.id}`}
      data-nav-anchor={panel.title}
      className={styles.panel}
      data-size={size}
      aria-labelledby={`metrics-${panel.id}-title`}
    >
      <header className={styles.panelHeader}>
        <div className={styles.panelText}>
          <h3 id={`metrics-${panel.id}-title`} className={styles.panelTitle}>
            {panel.title}
          </h3>
          <p className={styles.panelSubtitle}>{subtitle ?? panel.description}</p>
        </div>
        <div className={styles.panelActions}>
          {actions}
          {table ? (
            <button
              type="button"
              className={styles.tableToggle}
              aria-pressed={asTable}
              onClick={() => setAsTable((value) => !value)}
              title={asTable ? 'Show the chart' : 'Show the numbers as a table'}
            >
              <Icon name={asTable ? 'chart' : 'table'} size={14} />
              {asTable ? 'Chart' : 'Table'}
            </button>
          ) : null}
        </div>
      </header>
      <div className={styles.panelBody} data-refreshing={refreshing || undefined}>
        {asTable && table ? <div className={styles.tableView}>{table}</div> : children}
      </div>
    </section>
  );
}
