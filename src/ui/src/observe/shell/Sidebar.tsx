/**
 * The sidebar: Observe's table of contents. It lists the three sections with their numbers, follows the reader
 * down the page (a light marker slides to the current section), opens the current section's own anchors, and jumps
 * to any of them. Below: the way back home and to the other areas, then the instruments' state: where the data
 * comes from, whether each service answers, and the time zone.
 *
 * It folds to a narrow rail ([ or the button at the top), and remembers it.
 */
import { useLayoutEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import { useObserveData } from '../data/observeContext';
import type { Ping } from '../data/model';
import { formatDuration } from '../lib/format';
import { useSetUtc, useTimeFormats } from '../lib/timeZone';
import { Hint, Segmented } from '../ui/controls';
import { Icon, type IconName } from '../ui/Icon';
import { sections, type SectionId } from './sections';
import styles from './Sidebar.module.css';
import { scrollToId, type ScrollPosition } from './useScrollSpy';

/** The other areas of Synapsis. */
const areas: { name: string; icon: IconName; to?: string }[] = [
  { name: 'Home', icon: 'home', to: '/home' },
  { name: 'Analyze', icon: 'analyze', to: '/analyze' },
];

/** The sidebar. */
export function Sidebar({
  position,
  collapsed,
  onToggle,
  onShowShortcuts,
}: {
  position: ScrollPosition;
  collapsed: boolean;
  onToggle: () => void;
  onShowShortcuts: () => void;
}) {
  const current: SectionId = position.section ?? 'flow';
  const list = useRef<HTMLOListElement>(null);
  const [marker, setMarker] = useState<{ top: number; height: number } | null>(null);

  // The marker follows the current section's link, or the current anchor's when there is one.
  useLayoutEffect(() => {
    const target =
      (position.anchor &&
        list.current?.querySelector<HTMLElement>(
          `[data-anchor="${CSS.escape(position.anchor)}"]`,
        )) ||
      list.current?.querySelector<HTMLElement>(`[data-section="${current}"]`);
    if (target && list.current) {
      const box = target.getBoundingClientRect();
      const parent = list.current.getBoundingClientRect();
      setMarker({ top: box.top - parent.top, height: box.height });
    }
  }, [current, position.anchor, position.anchors, collapsed]);

  return (
    <aside
      className={styles.sidebar}
      data-collapsed={collapsed || undefined}
      aria-label="Observe navigation"
    >
      <div className={styles.brandRow}>
        <Link to="/home" className={styles.brand} aria-label="Synapsis home">
          <span className={styles.monogram} aria-hidden="true">
            S
          </span>
          <span className={styles.wordmark}>Synapsis</span>
        </Link>
        <button
          type="button"
          className={styles.fold}
          onClick={onToggle}
          aria-label={collapsed ? 'Unfold the sidebar' : 'Fold the sidebar'}
          aria-expanded={!collapsed}
        >
          <Icon name="sidebar" />
        </button>
      </div>

      <p className={styles.areaLabel}>Observe</p>
      <nav aria-label="Sections">
        <ol className={styles.sections} ref={list}>
          {marker ? (
            <span
              className={styles.marker}
              style={{ transform: `translateY(${marker.top}px)`, height: marker.height }}
              aria-hidden="true"
            />
          ) : null}
          {sections.map((section) => {
            const active = section.id === current;
            const anchors = position.anchors.filter((anchor) => anchor.section === section.id);
            return (
              <li key={section.id}>
                <Hint text={collapsed ? section.title : ''} side="right">
                  <a
                    href={`#${section.id}`}
                    data-section={section.id}
                    className={styles.sectionLink}
                    aria-current={active && !position.anchor ? 'location' : undefined}
                    data-active={active || undefined}
                    onClick={(event) => {
                      event.preventDefault();
                      scrollToId(section.id);
                    }}
                  >
                    <span className={styles.number}>{section.number}</span>
                    <span className={styles.sectionTitle}>{section.title}</span>
                    <kbd className={styles.key}>{section.key}</kbd>
                  </a>
                </Hint>
                {anchors.length > 0 ? (
                  <div
                    className={styles.anchorsWrap}
                    data-open={(active && !collapsed) || undefined}
                  >
                    <ol className={styles.anchors}>
                      {anchors.map((anchor) => (
                        <li key={anchor.id}>
                          <a
                            href={`#${anchor.id}`}
                            data-anchor={anchor.id}
                            className={styles.anchorLink}
                            aria-current={position.anchor === anchor.id ? 'location' : undefined}
                            tabIndex={active ? 0 : -1}
                            onClick={(event) => {
                              event.preventDefault();
                              scrollToId(anchor.id);
                            }}
                          >
                            {anchor.label}
                          </a>
                        </li>
                      ))}
                    </ol>
                  </div>
                ) : null}
              </li>
            );
          })}
        </ol>
      </nav>

      <div className={styles.spacer} />

      <nav aria-label="Synapsis" className={styles.areas}>
        {areas.map((area) =>
          area.to ? (
            <Hint key={area.name} text={collapsed ? area.name : ''} side="right">
              <Link to={area.to} className={styles.areaLink}>
                <Icon name={area.icon} />
                <span className={styles.areaName}>{area.name}</span>
              </Link>
            </Hint>
          ) : (
            <Hint key={area.name} text={collapsed ? `${area.name}, coming soon` : ''} side="right">
              <span className={styles.areaLink} aria-disabled="true">
                <Icon name={area.icon} />
                <span className={styles.areaName}>{area.name}</span>
                <span className={styles.soon}>Soon</span>
              </span>
            </Hint>
          ),
        )}
      </nav>

      <Instruments collapsed={collapsed} onShowShortcuts={onShowShortcuts} />
    </aside>
  );
}

/** The instruments' state, at the foot of the sidebar. */
function Instruments({
  collapsed,
  onShowShortcuts,
}: {
  collapsed: boolean;
  onShowShortcuts: () => void;
}) {
  const { mode, health, log, api } = useObserveData();
  const formats = useTimeFormats();
  const setUtc = useSetUtc();
  const overall: Ping | null =
    health.log && health.api
      ? { ok: health.log.ok && health.api.ok, latencyMs: 0, detail: '' }
      : null;

  if (collapsed) {
    return (
      <div className={styles.instruments}>
        <Hint
          side="right"
          text={
            overall === null
              ? 'Checking the services'
              : overall.ok
                ? `Both services answer${mode === 'simulated' ? ' (simulated)' : ''}`
                : 'A service does not answer'
          }
        >
          <span className={styles.collapsedStatus}>
            <StatusDot ping={overall} />
          </span>
        </Hint>
      </div>
    );
  }

  return (
    <div className={styles.instruments}>
      <ul className={styles.services}>
        <ServiceRow name="Elasticsearch" ping={health.log} where={log.location} />
        <ServiceRow name="API" ping={health.api} where={api.location} />
      </ul>
      <div className={styles.row}>
        <span className={styles.rowLabel}>Time</span>
        <Segmented<'local' | 'utc'>
          small
          label="Time zone"
          value={formats.utc ? 'utc' : 'local'}
          onChange={(value) => setUtc(value === 'utc')}
          options={[
            { value: 'local', label: 'Local' },
            { value: 'utc', label: 'UTC' },
          ]}
        />
      </div>
      <button type="button" className={styles.shortcuts} onClick={onShowShortcuts}>
        <Icon name="keyboard" />
        Keyboard shortcuts
        <kbd className={styles.key}>?</kbd>
      </button>
    </div>
  );
}

/** One service: its state, name and answer time. */
function ServiceRow({ name, ping, where }: { name: string; ping: Ping | null; where: string }) {
  const state = ping === null ? 'Checking' : ping.ok ? formatDuration(ping.latencyMs) : 'No answer';
  return (
    <li className={styles.service} title={ping ? `${where}: ${ping.detail}` : where}>
      <StatusDot ping={ping} />
      <span className={styles.serviceName}>{name}</span>
      <span className={styles.serviceState} data-failed={(ping && !ping.ok) || undefined}>
        {state}
      </span>
    </li>
  );
}

/** A coloured dot for a service's state; the text beside it always says the same in words. */
function StatusDot({ ping }: { ping: Ping | null }) {
  return (
    <span
      className={styles.dot}
      data-state={ping === null ? 'pending' : ping.ok ? 'good' : 'critical'}
      aria-hidden="true"
    />
  );
}
