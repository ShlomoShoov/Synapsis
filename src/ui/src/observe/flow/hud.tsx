/**
 * What surrounds the 3D scene: the readouts at the top, the arrivals feed on the right, the legend at the bottom, and
 * the card of the source under the pointer.
 */
import type { SourceInfo } from '../data/catalog';
import type { LogEntry, SourceWindow } from '../data/model';
import { domainById, sourceDomains } from '../domains';
import { formatAge, formatCompact, formatCount, formatPercent, formatSpan } from '../lib/format';
import { useTimeFormats } from '../lib/timeZone';
import { DomainDot } from '../shared/OutcomeBar';
import type { FlowStats } from './director';
import styles from './FlowSection.module.css';

/** The readouts: rates over the last minutes of scene time. */
export function Readouts({ stats, totalSources }: { stats: FlowStats; totalSources: number }) {
  const minutes = stats.windowMs / 60_000;
  const items = [
    { label: 'Records / min', value: formatCompact(Math.round(stats.received / minutes)) },
    {
      label: 'Batches / min',
      value: formatCompact(Math.round((stats.batches / minutes) * 10) / 10),
    },
    {
      label: 'Refused',
      value: stats.received ? formatPercent(stats.rejected / stats.received) : '—',
      alert: stats.received > 0 && stats.rejected / stats.received >= 0.02,
    },
    { label: 'Sources sending', value: `${stats.sources}`, of: totalSources },
  ];
  return (
    <div className={styles.readouts}>
      <dl className={styles.readoutList}>
        {items.map((item) => (
          <div key={item.label} className={styles.readout}>
            <dt>{item.label}</dt>
            <dd data-alert={item.alert || undefined}>
              {item.value}
              {item.of ? <span className={styles.readoutOf}> of {item.of}</span> : null}
            </dd>
          </div>
        ))}
      </dl>
      <p className={styles.readoutNote}>over the last {formatSpan(stats.windowMs)} of the scene</p>
    </div>
  );
}

/** The latest batches, newest first; each opens its sheet. */
export function ArrivalsFeed({
  feed,
  sourceOf,
  onOpen,
  focus,
}: {
  feed: readonly LogEntry[];
  sourceOf: (id: string) => SourceInfo;
  onOpen: (entry: LogEntry) => void;
  focus: string | null;
}) {
  const formats = useTimeFormats();
  const shown = (focus ? feed.filter((entry) => entry.source === focus) : feed).slice(0, 9);
  return (
    <section className={styles.feed} aria-label="Arrivals">
      <h3 className={styles.feedTitle}>
        Arrivals
        <span className={styles.feedNote}>{focus ? sourceOf(focus).short : 'every source'}</span>
      </h3>
      {shown.length === 0 ? (
        <p className={styles.feedEmpty}>Nothing has arrived in this part of the timeline yet.</p>
      ) : (
        <ol className={styles.feedList}>
          {shown.map((entry) => {
            const info = sourceOf(entry.source);
            const written = entry.new + entry.changed;
            return (
              <li key={entry.id} className={styles.feedItem}>
                <button type="button" className={styles.feedButton} onClick={() => onOpen(entry)}>
                  <span className={styles.feedTime}>{formats.clock(entry.time)}</span>
                  <span className={styles.feedSource}>
                    <DomainDot domain={info.domain} size={6} />
                    {info.short}
                  </span>
                  <span className={styles.feedCounts}>
                    <span title="Records stored (new and new versions)">
                      {written > 0 ? `+${formatCompact(written)}` : '±0'}
                    </span>
                    {entry.rejected > 0 ? (
                      <span className={styles.feedRejected} title="Records refused">
                        ×{formatCompact(entry.rejected)}
                      </span>
                    ) : null}
                  </span>
                </button>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}

/** The legend: what the colours and shapes mean. */
export function Legend() {
  return (
    <div className={styles.legend}>
      <ul className={styles.legendDomains}>
        {sourceDomains.map((domain) => (
          <li key={domain.id} title={domain.name}>
            <span
              className={styles.legendSwatch}
              style={{ background: domain.color }}
              aria-hidden="true"
            />
            {domain.short}
          </li>
        ))}
      </ul>
      <p className={styles.legendKeys}>
        <span>
          <i className={styles.keyArc} aria-hidden="true" /> a batch, on its way
        </span>
        <span>
          <i className={styles.keyPylon} aria-hidden="true" /> height: records in 24 h
        </span>
        <span>
          <i className={styles.keySpark} aria-hidden="true" /> refused records
        </span>
      </p>
    </div>
  );
}

/** The card of a source, shown beside its light. */
export function SourceCard({
  source,
  window,
  now,
  focused,
}: {
  source: SourceInfo;
  window: SourceWindow | undefined;
  now: number;
  focused: boolean;
}) {
  return (
    <div className={styles.card} role="tooltip">
      <div className={styles.cardHead}>
        <span className={styles.cardName}>
          <DomainDot domain={source.domain} size={8} />
          {source.short}
        </span>
        <span className={styles.cardDomain}>{domainById(source.domain).name}</span>
      </div>
      {source.title ? <p className={styles.cardTitle}>{source.title}</p> : null}
      <dl className={styles.cardStats}>
        <div>
          <dt>Last 24 h</dt>
          <dd>{window ? `${formatCount(window.received)} records` : 'nothing'}</dd>
        </div>
        <div>
          <dt>Refused</dt>
          <dd data-alert={(window && window.rejected > 0) || undefined}>
            {window && window.received
              ? `${formatCount(window.rejected)} (${formatPercent(window.rejected / window.received)})`
              : '—'}
          </dd>
        </div>
        <div>
          <dt>Last batch</dt>
          <dd>{window ? formatAge(window.last, now) : 'more than a day ago'}</dd>
        </div>
      </dl>
      <p className={styles.cardHint}>
        {focused ? 'Click to follow every source again' : 'Click to follow this source'}
      </p>
    </div>
  );
}
