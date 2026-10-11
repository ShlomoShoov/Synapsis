/**
 * One package on the map: its name and title, then its definitions by kind, one row each (a source shows how much
 * it sent in the last 24 hours and whether its feeder runs on time). Kinds that are rarely looked at are summed up in
 * a last line that opens the card.
 *
 * Every row stands where layout.ts put it (absolute, fixed heights), so the threads always meet their row. Rows are
 * buttons: a click inspects, the pointer or focus lights the connections, and the up and down arrows move between the
 * rows of a card (Tab moves between cards).
 */
import { memo, type CSSProperties, type KeyboardEvent } from 'react';
import { shortSourceName, domainById } from '../domains';
import { formatCompact, formatSpan } from '../lib/format';
import type { Freshness } from '../metrics/freshness';
import controls from '../ui/controls.module.css';
import type { RowLight, Target } from './focus';
import { countOf, kindInfo, type Definition, type PackageNode, type SchemaGraph } from './graph';
import { KindGlyph } from './KindGlyph';
import type { CardLayout, RowLayout } from './layout';
import styles from './SchemaMap.module.css';

/** What the log says of one source lately. */
export interface SourceLive {
  /** Records received in the last 24 hours. */
  received: number;
  rejected: number;
  /** When it last sent a batch, if ever (over the last 30 days). */
  last: number | null;
  freshness: Freshness;
}

/** The short line at the end of a row. */
function metaOf(
  definition: Definition,
  graph: SchemaGraph,
  hub: boolean,
  live: SourceLive | undefined,
): string {
  switch (definition.kind) {
    case 'source':
      return live ? (live.received ? formatCompact(live.received) : '0') : '';
    case 'measure':
      return definition.declaration.unit;
    case 'space':
      return definition.declaration.closed
        ? 'closed'
        : `${definition.declaration.max_depth} levels`;
    case 'vocabulary':
      return String(definition.declaration.values.length);
    case 'namespace':
      return definition.declaration.entity_type;
    case 'entity':
    case 'role': {
      if (!hub) {
        const attributes =
          definition.kind === 'entity'
            ? Object.keys(definition.declaration.attributes ?? {}).length
            : 0;
        return attributes ? `${attributes} attr.` : '';
      }
      // On the hub: how many packages build on it.
      const users = new Set(
        (graph.usedBy.get(definition.id) ?? [])
          .filter((reference) => reference.crossPackage)
          .map((reference) => graph.definitions.get(reference.from)?.packageName),
      );
      return users.size ? `${users.size} pkg` : '';
    }
    default:
      return '';
  }
}

/** Moves the focus to the next or previous button of the card on the up and down arrows. */
function moveFocus(event: KeyboardEvent<HTMLButtonElement>) {
  if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') {
    return;
  }
  const card = event.currentTarget.closest('article');
  const buttons = card ? [...card.querySelectorAll<HTMLButtonElement>('button')] : [];
  const index = buttons.indexOf(event.currentTarget);
  const next = buttons[index + (event.key === 'ArrowDown' ? 1 : -1)];
  if (next) {
    event.preventDefault();
    next.focus();
  }
}

/** The props of a card. */
export interface PackageCardProps {
  card: CardLayout;
  pkg: PackageNode;
  graph: SchemaGraph;
  hub: boolean;
  dim: boolean;
  /** The rows lit, by definition id. */
  rows: ReadonlyMap<string, RowLight>;
  selected: Target | null;
  live: ReadonlyMap<string, SourceLive>;
  /** "new" or "changed" since the map was opened. */
  badge: 'new' | 'changed' | null;
  onSelect: (target: Target) => void;
  onHover: (target: Target | null) => void;
  onToggle: (name: string) => void;
}

/** One package's card. */
export const PackageCard = memo(function PackageCard({
  card,
  pkg,
  graph,
  hub,
  dim,
  rows,
  selected,
  live,
  badge,
  onSelect,
  onHover,
  onToggle,
}: PackageCardProps) {
  const domain = domainById(pkg.domain);
  const packageTarget: Target = { type: 'package', name: pkg.name };
  const style = {
    left: card.x,
    top: card.y,
    width: card.width,
    height: card.height,
    '--domain': domain.color,
  } as CSSProperties;

  const row = (item: RowLayout) => {
    if (item.type !== 'definition' || !item.definition) {
      const hidden = item.hidden;
      const text =
        item.type === 'less'
          ? 'Show less'
          : item.type === 'more'
            ? `${hidden.length} more`
            : [...new Set(hidden.map((definition) => definition.kind))]
                .map((kind) =>
                  countOf(kind, hidden.filter((definition) => definition.kind === kind).length),
                )
                .join(' · ');
      return (
        <button
          key={item.key}
          type="button"
          className={styles.rowMore}
          style={{ top: item.y }}
          tabIndex={-1}
          aria-expanded={card.expanded}
          onClick={() => onToggle(pkg.name)}
          onKeyDown={moveFocus}
          title={item.type === 'less' ? 'Sum up the rest again' : 'Show every row of this package'}
        >
          <span className={styles.rowText}>{text}</span>
        </button>
      );
    }
    const definition = item.definition;
    const light = rows.get(definition.id);
    const isSelected = selected?.type === 'definition' && selected.id === definition.id;
    const sourceLive = definition.kind === 'source' ? live.get(definition.code) : undefined;
    const target: Target = { type: 'definition', id: definition.id };
    return (
      <button
        key={item.key}
        type="button"
        className={styles.row}
        style={{ top: item.y }}
        tabIndex={-1}
        data-light={light}
        data-selected={isSelected || undefined}
        aria-pressed={isSelected}
        onClick={() => onSelect(target)}
        onPointerEnter={() => onHover(target)}
        onPointerLeave={() => onHover(null)}
        onFocus={() => onHover(target)}
        onBlur={() => onHover(null)}
        onKeyDown={moveFocus}
      >
        <KindGlyph kind={definition.kind} />
        <span className={controls.srOnly}>{kindInfo[definition.kind].one}: </span>
        <span className={styles.rowText}>
          {definition.kind === 'source' ? shortSourceName(definition.code) : definition.code}
        </span>
        {sourceLive ? (
          <span
            className={styles.liveDot}
            data-status={sourceLive.freshness.status}
            title={`${sourceLive.freshness.label}${sourceLive.freshness.cadenceMs ? `, every ${formatSpan(sourceLive.freshness.cadenceMs)}` : ''}`}
          />
        ) : null}
        <span className={styles.rowMeta}>
          {metaOf(definition, graph, hub, sourceLive)}
          {sourceLive ? (
            <span className={controls.srOnly}> records in the last 24 hours</span>
          ) : null}
        </span>
      </button>
    );
  };

  return (
    <article
      className={styles.card}
      style={style}
      data-hub={hub || undefined}
      data-dim={dim || undefined}
      data-selected={(selected?.type === 'package' && selected.name === pkg.name) || undefined}
      aria-label={`Package ${pkg.name}`}
      data-card={pkg.name}
    >
      <button
        type="button"
        className={styles.cardHead}
        onClick={() => onSelect(packageTarget)}
        onPointerEnter={() => onHover(packageTarget)}
        onPointerLeave={() => onHover(null)}
        onFocus={() => onHover(packageTarget)}
        onBlur={() => onHover(null)}
        onKeyDown={moveFocus}
      >
        <span className={styles.cardNameLine}>
          <span className={styles.cardDot} aria-hidden="true" />
          <span className={styles.cardName}>{pkg.name}</span>
          {badge ? (
            <span className={styles.badge}>{badge === 'new' ? 'New' : 'Changed'}</span>
          ) : null}
          <span className={styles.version}>v{pkg.version}</span>
        </span>
        <span className={styles.cardTitle}>
          {hub ? 'Shared vocabulary · ' : ''}
          {pkg.title}
        </span>
      </button>
      {pkg.readable ? null : (
        <p className={styles.unreadable}>The API gave no content for this package.</p>
      )}
      {card.sections.map((section) => (
        <div key={section.kind} className={styles.section}>
          <p className={styles.sectionLabel} style={{ top: section.y }}>
            <span>{kindInfo[section.kind].many}</span>
            <span className={styles.sectionCount}>{section.count}</span>
          </p>
          {section.rows.map(row)}
        </div>
      ))}
      {card.footer ? row(card.footer) : null}
    </article>
  );
});
