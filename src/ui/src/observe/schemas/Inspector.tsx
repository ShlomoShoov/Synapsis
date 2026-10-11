/**
 * The inspector: everything about the selected package or definition, on the right of the map. Every package and
 * definition it names is a button that selects it (and brings it into view).
 */
import type { ReactNode } from 'react';
import { domainById, shortSourceName } from '../domains';
import { formatAge, formatCount, formatPercent, formatSpan } from '../lib/format';
import { Icon } from '../ui/Icon';
import type { Target } from './focus';
import {
  countOf,
  definitionKinds,
  kindInfo,
  neighboursOf,
  type Definition,
  type Reference,
  type SchemaGraph,
} from './graph';
import { KindGlyph } from './KindGlyph';
import type { SourceLive } from './PackageCard';
import styles from './SchemaSection.module.css';

/** How a reference reads from either end. */
function relation(reference: Reference, fromSide: boolean): string {
  switch (reference.kind) {
    case 'type':
      return fromSide ? `as ${reference.role}` : `${reference.role} of`;
    case 'role':
      return fromSide ? 'allows the role' : 'role in';
    case 'measure':
      return fromSide ? 'carries' : 'carried by';
    case 'from':
      return fromSide ? 'from' : 'starts';
    case 'to':
      return fromSide ? 'to' : 'ends';
    case 'namespace':
      return fromSide ? 'keys of' : 'keyed in';
  }
}

/** A list of facts. */
function Facts({ items }: { items: [string, ReactNode][] }) {
  const shown = items.filter(([, value]) => value !== null && value !== undefined && value !== '');
  return shown.length ? (
    <dl className={styles.facts}>
      {shown.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  ) : null;
}

/** A button naming a definition. */
function DefinitionLink({
  definition,
  note,
  onSelect,
}: {
  definition: Definition;
  note?: string;
  onSelect: (target: Target) => void;
}) {
  return (
    <button
      type="button"
      className={styles.link}
      onClick={() => onSelect({ type: 'definition', id: definition.id })}
    >
      <KindGlyph kind={definition.kind} />
      <span className={styles.linkCode}>
        {definition.kind === 'source' ? shortSourceName(definition.code) : definition.code}
      </span>
      {note ? <span className={styles.linkNote}>{note}</span> : null}
      <span className={styles.linkPackage}>{definition.packageName}</span>
    </button>
  );
}

/** The details that depend on the kind. */
function details(
  definition: Definition,
  live: SourceLive | undefined,
  now: number,
): [string, ReactNode][] {
  switch (definition.kind) {
    case 'source': {
      const d = definition.declaration;
      return [
        ['Publisher', d.publisher],
        ['Reliability', `${d.reliability} (${d.reliability_basis})`],
        ['Publication lag', `${d.publication_lag_days} d`],
        ['Known at', d.known_at_rule],
        ['Licence', d.license],
        [
          'Address',
          d.url ? (
            <a key="url" href={d.url} target="_blank" rel="noreferrer">
              {d.url.replace(/^https?:\/\//, '')}
            </a>
          ) : null,
        ],
        [
          'Last 24 hours',
          live
            ? `${formatCount(live.received)} records${live.received ? `, ${formatPercent(live.rejected / live.received)} refused` : ''}`
            : null,
        ],
        ['Last batch', live?.last ? formatAge(live.last, now) : live ? 'none in 30 days' : null],
        [
          'Runs',
          live
            ? `${live.freshness.label}${live.freshness.cadenceMs ? `, every ${formatSpan(live.freshness.cadenceMs)}` : ''}`
            : null,
        ],
        ['Notes', d.notes],
      ];
    }
    case 'event':
      return [
        ['Place', definition.declaration.place.join(', ')],
        ['Text', definition.declaration.text ?? 'none'],
        ['Attributes', Object.keys(definition.declaration.attributes ?? {}).join(', ')],
      ];
    case 'entity':
      return [
        ['Attributes', Object.keys(definition.declaration.attributes ?? {}).join(', ') || 'none'],
      ];
    case 'relation':
      return [['Attributes', Object.keys(definition.declaration.attributes ?? {}).join(', ')]];
    case 'measure': {
      const d = definition.declaration;
      const range =
        d.min !== undefined || d.max !== undefined ? `${d.min ?? '−∞'} to ${d.max ?? '∞'}` : null;
      return [
        ['Unit', d.unit],
        ['Range', range],
        ['Signed', d.signed ? 'yes' : null],
      ];
    }
    case 'space':
      return [
        ['Depth', `${definition.declaration.max_depth} levels`],
        ['Closed', definition.declaration.closed ? 'yes: only listed values' : 'no'],
        ['Values', definition.declaration.values?.join(', ')],
      ];
    case 'namespace':
      return [['Keys', <code key="pattern">{definition.declaration.key_pattern}</code>]];
    case 'vocabulary':
      return [['Values', definition.declaration.values.join(', ')]];
    default:
      return [];
  }
}

/** The inspector of a target. */
export function Inspector({
  graph,
  target,
  live,
  now,
  onSelect,
  onClose,
}: {
  graph: SchemaGraph;
  target: Target;
  live: ReadonlyMap<string, SourceLive>;
  /** The moment ages are counted from. */
  now: number;
  onSelect: (target: Target) => void;
  onClose: () => void;
}) {
  const close = (
    <button type="button" className={styles.close} onClick={onClose} aria-label="Close the details">
      <Icon name="close" />
    </button>
  );

  if (target.type === 'package') {
    const pkg = graph.packageByName.get(target.name);
    if (!pkg) {
      return null;
    }
    const neighbours = neighboursOf(graph, pkg.name);
    const counts = definitionKinds
      .map(
        (kind) =>
          [kind, pkg.definitions.filter((definition) => definition.kind === kind).length] as const,
      )
      .filter(([, count]) => count > 0);
    return (
      <aside className={styles.inspector} aria-label={`Package ${pkg.name}`}>
        <header className={styles.inspectorHead}>
          <p className={styles.kind}>
            <span
              className={styles.domainDot}
              style={{ background: domainById(pkg.domain).color }}
            />
            Package · {domainById(pkg.domain).name}
          </p>
          {close}
        </header>
        <h3 className={styles.code}>{pkg.name}</h3>
        <p className={styles.title}>{pkg.title}</p>
        {pkg.description ? <p className={styles.description}>{pkg.description}</p> : null}
        <Facts
          items={[
            ['Version', `${pkg.version} · ${pkg.fingerprint.slice(0, 12)}`],
            ['Maintainer', pkg.maintainer],
            ['Record ids', pkg.recordNamespaces.join(', ')],
            ['Entity ids', pkg.entityNamespaces.join(', ')],
            [
              'Defines',
              counts.map(([kind, count]) => countOf(kind, count)).join(', ') || 'nothing',
            ],
          ]}
        />
        {[
          ['Builds on', neighbours.uses],
          ['Used by', neighbours.usedBy],
        ].map(([label, names]) =>
          (names as string[]).length ? (
            <section key={label as string} className={styles.block}>
              <h4 className={styles.blockTitle}>{label as string}</h4>
              <div className={styles.chips}>
                {(names as string[]).map((name) => (
                  <button
                    key={name}
                    type="button"
                    className={styles.chip}
                    onClick={() => onSelect({ type: 'package', name })}
                  >
                    {name}
                  </button>
                ))}
              </div>
            </section>
          ) : null,
        )}
        <section className={styles.block}>
          <h4 className={styles.blockTitle}>Contents</h4>
          <div className={styles.links}>
            {pkg.definitions.map((definition) => (
              <DefinitionLink
                key={definition.id}
                definition={definition}
                note={kindInfo[definition.kind].one}
                onSelect={onSelect}
              />
            ))}
          </div>
        </section>
      </aside>
    );
  }

  const definition = graph.definitions.get(target.id);
  if (!definition) {
    return null;
  }
  const uses = graph.uses.get(definition.id) ?? [];
  const usedBy = graph.usedBy.get(definition.id) ?? [];
  const unresolved = graph.unresolved.filter((item) => item.from === definition.id);
  return (
    <aside
      className={styles.inspector}
      aria-label={`${kindInfo[definition.kind].one} ${definition.code}`}
    >
      <header className={styles.inspectorHead}>
        <p className={styles.kind}>
          <KindGlyph kind={definition.kind} />
          {kindInfo[definition.kind].one}
        </p>
        {close}
      </header>
      <h3 className={styles.code}>
        {definition.kind === 'source' ? shortSourceName(definition.code) : definition.code}
      </h3>
      <p className={styles.title}>{definition.title}</p>
      <button
        type="button"
        className={styles.chip}
        onClick={() => onSelect({ type: 'package', name: definition.packageName })}
      >
        in {definition.packageName}
      </button>
      <Facts
        items={details(
          definition,
          definition.kind === 'source' ? live.get(definition.code) : undefined,
          now,
        )}
      />
      {uses.length ? (
        <section className={styles.block}>
          <h4 className={styles.blockTitle}>Uses</h4>
          <div className={styles.links}>
            {uses.map((reference) => {
              const other = graph.definitions.get(reference.to);
              return other ? (
                <DefinitionLink
                  key={`${reference.kind}${reference.to}${reference.role ?? ''}`}
                  definition={other}
                  note={relation(reference, true)}
                  onSelect={onSelect}
                />
              ) : null;
            })}
          </div>
        </section>
      ) : null}
      {usedBy.length ? (
        <section className={styles.block}>
          <h4 className={styles.blockTitle}>Used by</h4>
          <div className={styles.links}>
            {usedBy.map((reference) => {
              const other = graph.definitions.get(reference.from);
              return other ? (
                <DefinitionLink
                  key={`${reference.kind}${reference.from}${reference.role ?? ''}`}
                  definition={other}
                  note={relation(reference, false)}
                  onSelect={onSelect}
                />
              ) : null;
            })}
          </div>
        </section>
      ) : null}
      {unresolved.length ? (
        <p className={styles.warning}>
          <Icon name="warning" size={14} />
          Uses codes no package defines: {unresolved.map((item) => item.code).join(', ')}.
        </p>
      ) : null}
    </aside>
  );
}
