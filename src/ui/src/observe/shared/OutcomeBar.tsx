/**
 * Small pieces that show a source or a set of records the same way everywhere: a domain's dot, and the outcome bar,
 * one thin bar split into what was written, left alone and refused (a 2-pixel gap between parts, as the data-viz
 * rules ask).
 */
import { domainById, type DomainId } from '../domains';
import type { OutcomeCounts } from '../data/model';
import { formatCount, formatPercent } from '../lib/format';
import { outcomes } from '../outcomes';
import controls from '../ui/controls.module.css';
import styles from './shared.module.css';

/** The dot of a domain's colour, beside a source's name. */
export function DomainDot({ domain, size = 8 }: { domain: DomainId; size?: number }) {
  return (
    <span
      className={styles.dot}
      style={{ width: size, height: size, background: domainById(domain).color }}
      title={domainById(domain).name}
      aria-hidden="true"
    />
  );
}

/** A thin bar split by outcome, with the total share of each in its label for screen readers. */
export function OutcomeBar({ counts, height = 6 }: { counts: OutcomeCounts; height?: number }) {
  const total = counts.received;
  const label = outcomes
    .filter((item) => counts[item.key] > 0)
    .map(
      (item) =>
        `${item.label} ${formatCount(counts[item.key])} (${formatPercent(counts[item.key] / total)})`,
    )
    .join(', ');
  return (
    <span className={styles.outcomeBar} style={{ height }}>
      <span className={controls.srOnly}>{label || 'No records'}</span>
      {total > 0
        ? outcomes.map((item) =>
            counts[item.key] > 0 ? (
              <span
                key={item.key}
                aria-hidden="true"
                className={styles.outcomePart}
                style={{ flexGrow: counts[item.key], background: item.color }}
              />
            ) : null,
          )
        : null}
    </span>
  );
}
