/**
 * Section 02, the metrics: the ingestion log's numbers over a chosen period, in panels the viewer can arrange.
 *
 * One question to the log answers every chart at once (the snapshot), so the numbers on the page always agree; a
 * second one, the same totals over the period before, gives the changes; a third, each source's last 30 days, gives
 * freshness. The filters scope everything and live in the address.
 */
import { useMemo, useState } from 'react';
import type { LogEntry } from '../data/model';
import { useObserveData } from '../data/observeContext';
import { useResource } from '../data/useResource';
import { useNow } from '../lib/clock';
import { DAY, HOUR, niceInterval } from '../lib/time';
import { BatchSheet } from '../shared/BatchSheet';
import { SectionHeader } from '../shell/SectionHeader';
import { sectionInfo } from '../shell/sections';
import controls from '../ui/controls.module.css';
import { Icon } from '../ui/Icon';
import { Customize } from './Customize';
import { FilterBar } from './FilterBar';
import {
  previousRange,
  rangeOf,
  readFilter,
  refreshMsOf,
  writeFilter,
  type MetricsFilter,
} from './filters';
import { panels, sizeOf, useLayout, type PanelInfo, type PanelSize } from './layout';
import styles from './MetricsSection.module.css';
import { BatchLogPanel } from './panels/BatchLogPanel';
import type { MetricsData } from './panels/common';
import { FiguresPanel } from './panels/FiguresPanel';
import { HeatmapPanel } from './panels/HeatmapPanel';
import { LatencyPanel, ThroughputPanel } from './panels/RatePanels';
import { RejectionsPanel } from './panels/RejectionsPanel';
import { SourcesPanel } from './panels/SourcesPanel';
import { VolumePanel } from './panels/VolumePanel';

const section = sectionInfo('metrics');

/** The metrics section. */
export function MetricsSection() {
  const { log, catalog, mode, setMode, health } = useObserveData();
  const [filter, setFilter] = useState<MetricsFilter>(() =>
    readFilter(new URLSearchParams(window.location.search)),
  );
  /** When the viewer last changed the filters or pressed refresh: a period ending now ends then. */
  const [pressedAt, setPressedAt] = useState(0);
  const refreshMs = refreshMsOf(filter);
  // With refresh on, the period ends at the latest tick; off, it moves only when asked (or once a day).
  const tick = useNow(refreshMs ?? 24 * HOUR);
  const now = Math.max(tick, pressedAt);
  const range = useMemo(() => rangeOf(filter, now), [filter, now]);
  const intervalMs = niceInterval(range.to - range.from, 90);
  const sourcesKey = filter.sources ? [...filter.sources].sort().join(',') : 'all';
  const scope = `${mode}:${range.from}:${range.to}:${sourcesKey}`;

  const snapshot = useResource(`snapshot:${scope}:${intervalMs}`, (signal) =>
    log.snapshot({ range, sources: filter.sources, intervalMs }, signal),
  );
  const previous = useResource(filter.compare ? `previous:${scope}` : null, (signal) =>
    log.totals({ range: previousRange(range), sources: filter.sources }, signal),
  );
  const historyAt = Math.floor(now / (5 * 60_000)) * 5 * 60_000;
  const history = useResource(`history:${mode}:${historyAt}`, (signal) =>
    log.sourceHistory({ to: historyAt, lookbackMs: 30 * DAY }, signal),
  );

  const [layout, setLayout, resetLayout] = useLayout();
  const [customizing, setCustomizing] = useState(false);
  const [openEntry, setOpenEntry] = useState<LogEntry | null>(null);

  const change = (next: MetricsFilter) => {
    setFilter(next);
    setPressedAt(Date.now());
    const url = new URL(window.location.href);
    writeFilter(next, url);
    window.history.replaceState(window.history.state, '', url);
  };

  const data: MetricsData = {
    range,
    intervalMs,
    sources: filter.sources,
    snapshot: snapshot.data,
    previous: filter.compare ? previous.data : undefined,
    history: history.data,
    refreshing: snapshot.refreshing,
    catalog,
    now,
    scope,
    openBatch: setOpenEntry,
    focusSource: (source) => change({ ...filter, sources: [source] }),
  };

  const shown = layout.order
    .filter((id) => !layout.hidden.includes(id))
    .map((id) => panels.find((panel) => panel.id === id))
    .filter((panel): panel is PanelInfo => !!panel);
  const unreachable = mode === 'live' && health.log !== null && !health.log.ok;

  return (
    <section
      id="metrics"
      data-nav-section="metrics"
      className={styles.section}
      aria-labelledby="metrics-title"
    >
      <SectionHeader
        section={section}
        aside={
          <button type="button" className={controls.button} onClick={() => setCustomizing(true)}>
            <Icon name="sliders" size={15} />
            Customize
          </button>
        }
      />

      <FilterBar
        filter={filter}
        onChange={change}
        sources={catalog.sources}
        updatedAt={snapshot.updatedAt}
        busy={snapshot.loading || snapshot.refreshing}
        onRefresh={() => {
          setPressedAt(Date.now());
          snapshot.reload();
          previous.reload();
          history.reload();
        }}
      />

      {snapshot.error && !snapshot.refreshing ? (
        <div className={styles.message} role="alert">
          <Icon name="alert" size={18} />
          <div>
            <strong>The numbers could not be read</strong>
            {snapshot.error.message}
            {unreachable ? (
              <>
                {' '}
                Start the core with <code>docker compose -f deploy/core/compose.yml up -d</code>.
              </>
            ) : null}
          </div>
          {mode === 'live' ? (
            <button type="button" className={controls.button} onClick={() => setMode('simulated')}>
              Use simulated data
            </button>
          ) : null}
        </div>
      ) : null}

      <div className={styles.grid}>
        {shown.map((panel) => (
          <PanelFor key={panel.id} panel={panel} size={sizeOf(layout, panel)} data={data} />
        ))}
      </div>

      <Customize
        open={customizing}
        layout={layout}
        onChange={setLayout}
        onReset={resetLayout}
        onClose={() => setCustomizing(false)}
      />
      <BatchSheet entry={openEntry} onClose={() => setOpenEntry(null)} />
    </section>
  );
}

/** The panel component of each panel. */
function PanelFor({ panel, size, data }: { panel: PanelInfo; size: PanelSize; data: MetricsData }) {
  switch (panel.id) {
    case 'figures':
      return <FiguresPanel panel={panel} size={size} data={data} />;
    case 'volume':
      return <VolumePanel panel={panel} size={size} data={data} />;
    case 'throughput':
      return <ThroughputPanel panel={panel} size={size} data={data} />;
    case 'latency':
      return <LatencyPanel panel={panel} size={size} data={data} />;
    case 'sources':
      return <SourcesPanel panel={panel} size={size} data={data} />;
    case 'rejections':
      return <RejectionsPanel panel={panel} size={size} data={data} />;
    case 'heatmap':
      return <HeatmapPanel panel={panel} size={size} data={data} />;
    case 'batches':
      return <BatchLogPanel panel={panel} size={size} data={data} />;
  }
}
