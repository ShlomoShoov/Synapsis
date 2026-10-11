/**
 * The batch sheet: everything the log knows about one batch from one source, opened from the arrivals feed or the
 * batch log. Its outcome in numbers, the error codes that refused records (each with what it means), and the
 * refused records the log kept (the first 50), so a developer can go straight to the record and the field.
 *
 * A native modal dialog: focus stays inside, Escape and a click outside close it.
 */
import { useEffect, useRef } from 'react';
import { sourceInfo } from '../data/catalog';
import { errorCodeInfo } from '../data/errorCodes';
import type { LogEntry } from '../data/model';
import { useObserveData } from '../data/observeContext';
import { domainById } from '../domains';
import { formatCount, formatDuration, formatPercent } from '../lib/format';
import { useTimeFormats } from '../lib/timeZone';
import { outcomes } from '../outcomes';
import { Icon } from '../ui/Icon';
import { useLightDismiss } from '../ui/useLightDismiss';
import { DomainDot, OutcomeBar } from './OutcomeBar';
import styles from './shared.module.css';

/** The sheet of one log entry; open while `entry` is set. */
export function BatchSheet({ entry, onClose }: { entry: LogEntry | null; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useLightDismiss(dialog, onClose);
  const { catalog } = useObserveData();
  const formats = useTimeFormats();

  useEffect(() => {
    const element = dialog.current;
    if (!element) {
      return;
    }
    if (entry && !element.open) {
      element.showModal();
    } else if (!entry && element.open) {
      element.close();
    }
  }, [entry]);

  const info = entry ? sourceInfo(catalog, entry.source) : null;

  return (
    <dialog ref={dialog} className={styles.sheet} aria-label="Batch" onClose={onClose}>
      {entry && info ? (
        <div className={styles.sheetBody}>
          <header className={styles.sheetHeader}>
            <div>
              <p className={styles.sheetEyebrow}>Batch · {formats.dateTime(entry.time)}</p>
              <h2 className={styles.sheetTitle}>
                <DomainDot domain={info.domain} size={9} />
                {info.short}
              </h2>
              <p className={styles.sheetSubtitle}>{info.title ?? domainById(info.domain).name}</p>
            </div>
            <button
              type="button"
              className={styles.sheetClose}
              onClick={onClose}
              aria-label="Close"
            >
              <Icon name="close" />
            </button>
          </header>

          <dl className={styles.facts}>
            <div>
              <dt>Batch id</dt>
              <dd className={styles.mono}>{entry.batchId}</dd>
            </div>
            <div>
              <dt>Producer</dt>
              <dd className={styles.mono}>{entry.producer}</dd>
            </div>
            <div>
              <dt>Received</dt>
              <dd>
                {formats.longDay(entry.time)}, {formats.clock(entry.time)}
              </dd>
            </div>
            <div>
              <dt>Handled in</dt>
              <dd>{formatDuration(entry.durationMs)} (the whole batch)</dd>
            </div>
          </dl>

          <section className={styles.sheetSection}>
            <h3 className={styles.sheetHeading}>
              {formatCount(entry.received)} records from this source
            </h3>
            <OutcomeBar counts={entry} height={8} />
            <ul className={styles.outcomeList}>
              {outcomes.map((item) => (
                <li key={item.key} data-zero={entry[item.key] === 0 || undefined}>
                  <span
                    className={styles.swatch}
                    style={{ background: item.color }}
                    aria-hidden="true"
                  />
                  <span className={styles.outcomeName}>{item.label}</span>
                  <span className={styles.outcomeValue}>{formatCount(entry[item.key])}</span>
                  <span className={styles.outcomeShare}>
                    {entry.received ? formatPercent(entry[item.key] / entry.received) : '—'}
                  </span>
                </li>
              ))}
            </ul>
          </section>

          {entry.rejectionCounts.length > 0 ? (
            <section className={styles.sheetSection}>
              <h3 className={styles.sheetHeading}>Why records were refused</h3>
              <ul className={styles.codes}>
                {entry.rejectionCounts.map((item) => {
                  const meaning = errorCodeInfo(item.code);
                  return (
                    <li key={item.code}>
                      <span className={styles.code}>{item.code}</span>
                      <span className={styles.codeCount}>{formatCount(item.count)}</span>
                      <span className={styles.codeText}>
                        {meaning.rule ? `${meaning.rule}. ` : ''}
                        {meaning.text}
                      </span>
                    </li>
                  );
                })}
              </ul>
            </section>
          ) : null}

          {entry.rejections.length > 0 ? (
            <section className={styles.sheetSection}>
              <h3 className={styles.sheetHeading}>
                Refused records
                <span className={styles.headingNote}>
                  {entry.rejected > entry.rejections.length
                    ? `the first ${entry.rejections.length} of ${formatCount(entry.rejected)}`
                    : `all ${entry.rejections.length}`}
                </span>
              </h3>
              <table className={styles.rejections}>
                <thead>
                  <tr>
                    <th scope="col">#</th>
                    <th scope="col">Record</th>
                    <th scope="col">Code</th>
                    <th scope="col">Reason</th>
                  </tr>
                </thead>
                <tbody>
                  {entry.rejections.map((item) => (
                    <tr key={`${item.index}-${item.code}`}>
                      <td>{item.index}</td>
                      <td>{item.id ?? <span className={styles.none}>no readable id</span>}</td>
                      <td>{item.code}</td>
                      <td>{item.reason || '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          ) : null}
        </div>
      ) : null}
    </dialog>
  );
}
