/**
 * The object panel, on the right under the time card: one record, read in full.
 *
 * Its head says what the record is (its family's colour, its kind, its title, its type and id) and the trail of
 * records the investigation went through. Its body lays out the record as its source sent it (GET /records/{id}):
 * the facts, the text, the measures, the attributes and the evidence, then the record itself as JSON, once per source.
 * Between them, its connections (GET /graph?id=), divided by kind: a click on one moves the panel to it.
 */
import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import type { WireGraph, WireRecordAnswer } from '../api/contract';
import { ApiError } from '../api/http';
import type { LngLat } from '../api/model';
import { tokensOf } from '../query/jsonView';
import { groupsOf, type LinkGroup, type Neighbour } from './connections';
import {
  familyOf,
  formatTime,
  headingOfNode,
  headingOfRecord,
  kindOfId,
  sourceName,
  type Heading,
} from './describe';
import type { FilterField } from '../filter/viewFilter';
import { viewOf, type Named, type RecordView } from './record';
import { current, type Focus, type ObjectAnswers } from './useObject';
import styles from './ObjectPanel.module.css';

interface ObjectPanelProps {
  /** The records looked at, in order; the last is shown. */
  trail: Focus[];
  answers: ObjectAnswers;
  /** Move the panel to a connected or named record. */
  onOpen: (focus: Focus) => void;
  /** Go back to the record at this place in the trail. */
  onTrail: (index: number) => void;
  onClose: () => void;
  onShowOnMap: (point: LngLat) => void;
  /** Open the connections graph (given while the graph is not shown). */
  onOpenGraph?: () => void;
  /** Show the record's logical place in the tree. */
  onShowInTree: () => void;
  /** A field of the record becomes a filter of the view. */
  onFilter: OnFilter;
}

type OnFilter = (field: FilterField | 'time', value: string) => void;

/** The rows of a group shown before "All". */
const firstRows = 5;

const kindWords = { event: 'Event', entity: 'Entity', conclusion: 'Conclusion' };

export function ObjectPanel({
  trail,
  answers,
  onOpen,
  onTrail,
  onClose,
  onShowOnMap,
  onOpenGraph,
  onShowInTree,
  onFilter,
}: ObjectPanelProps) {
  const focus = trail.at(-1)!;
  const record = current(answers.records)?.[0]?.record;
  const centre = current(answers.graph)?.center;
  const heading: Heading = record
    ? headingOfRecord(record)
    : (focus.heading ??
      (centre ? headingOfNode(centre) : undefined) ?? {
        kind: kindOfId(focus.id),
        type: '',
        title: focus.id,
        ...familyOf(kindOfId(focus.id), ''),
      });

  return (
    <aside
      className={styles.panel}
      style={{ '--family': heading.color } as CSSProperties}
      aria-label={`Details: ${heading.title}`}
    >
      <header className={styles.head}>
        <nav className={styles.bar} aria-label="The records looked at">
          <button
            type="button"
            className={styles.icon}
            onClick={() => onTrail(trail.length - 2)}
            disabled={trail.length < 2}
            aria-label="Back"
            title="Back"
          >
            <svg viewBox="0 0 20 20" aria-hidden="true">
              <path d="M12.5 5 7.5 10l5 5" />
            </svg>
          </button>
          <ol className={styles.trail}>
            {trail.length < 2 ? (
              <li className={styles.trailHint}>The first record</li>
            ) : (
              trail.slice(0, -1).map((step, index) => (
                <li key={`${step.id}-${index}`}>
                  <button type="button" onClick={() => onTrail(index)} title={step.id}>
                    {step.heading?.title ?? step.id}
                  </button>
                </li>
              ))
            )}
          </ol>
          <button
            type="button"
            className={styles.icon}
            onClick={onClose}
            aria-label="Close and return to the map (Esc)"
            title="Close (Esc)"
          >
            <svg viewBox="0 0 20 20" aria-hidden="true">
              <path d="m6 6 8 8M14 6l-8 8" />
            </svg>
          </button>
        </nav>
        <p className={styles.kicker}>
          <i />
          {heading.family}
          <button
            type="button"
            className={styles.kind}
            title="Filter the view by this kind"
            onClick={() => onFilter('kind', heading.kind)}
          >
            {kindWords[heading.kind]}
          </button>
        </p>
        <h2 className={styles.title} key={focus.id}>
          {heading.title}
        </h2>
        <p className={styles.ids}>
          {heading.type && (
            <button
              type="button"
              className={styles.typeCode}
              title="Filter the view by this type"
              onClick={() => onFilter('type', heading.type)}
            >
              {heading.type}
            </button>
          )}
          <CopyId id={focus.id} />
        </p>
      </header>

      <ObjectBody
        key={focus.id}
        title={heading.title}
        answers={answers}
        onOpen={onOpen}
        onShowOnMap={onShowOnMap}
        onOpenGraph={onOpenGraph}
        onShowInTree={onShowInTree}
        onFilter={onFilter}
      />
    </aside>
  );
}

function ObjectBody({
  title,
  answers,
  onOpen,
  onShowOnMap,
  onOpenGraph,
  onShowInTree,
  onFilter,
}: {
  title: string;
  answers: ObjectAnswers;
  onOpen: (focus: Focus) => void;
  onShowOnMap: (point: LngLat) => void;
  onOpenGraph?: () => void;
  onShowInTree: () => void;
  onFilter: OnFilter;
}) {
  const statements = current(answers.records);
  const [chosen, setChosen] = useState(0);
  const answer = statements?.[Math.min(chosen, statements.length - 1)];
  const view = answer ? viewOf(answer.record) : undefined;

  return (
    <div className={styles.body}>
      {answers.records.pending && <Loading lines={5} />}
      {answers.records.error && <Failure error={answers.records.error} />}
      {statements && statements.length > 1 && (
        <div className={styles.sources} role="tablist" aria-label="Sources">
          {statements.map((statement, index) => (
            <button
              key={statement.source}
              type="button"
              role="tab"
              aria-selected={index === chosen}
              onClick={() => setChosen(index)}
            >
              {sourceName(statement.source)}
            </button>
          ))}
        </div>
      )}
      {view && (
        <Facts
          view={view}
          onShowOnMap={onShowOnMap}
          onShowInTree={onShowInTree}
          onFilter={onFilter}
        />
      )}
      {view && (view.body || (view.title && view.title !== title)) && (
        <section className={styles.section}>
          <h3 className={styles.heading}>Text</h3>
          {view.title && view.title !== title && <p className={styles.textTitle}>{view.title}</p>}
          {view.body && <p className={styles.text}>{view.body}</p>}
        </section>
      )}
      <Connections
        graph={answers.graph}
        named={view?.named ?? []}
        onOpen={onOpen}
        onOpenGraph={onOpenGraph}
      />
      {view && view.measures.length > 0 && (
        <section className={styles.section}>
          <h3 className={styles.heading}>
            Measures <span>{view.measures.length}</span>
          </h3>
          <ul className={styles.measures}>
            {view.measures.map((measure) => (
              <li key={measure.label}>
                <span>{measure.label}</span>
                <b>
                  {measure.value}
                  {measure.unit && <small> {measure.unit}</small>}
                </b>
              </li>
            ))}
          </ul>
        </section>
      )}
      {view && view.attributes.length > 0 && (
        <section className={styles.section}>
          <h3 className={styles.heading}>
            Attributes <span>{view.attributes.length}</span>
          </h3>
          <dl className={styles.attributes}>
            {view.attributes.map((attribute) => (
              <div key={attribute.label}>
                <dt>{attribute.label}</dt>
                <dd>
                  {attribute.link ? (
                    <a href={attribute.value} target="_blank" rel="noreferrer">
                      {attribute.value}
                    </a>
                  ) : (
                    attribute.value
                  )}
                </dd>
              </div>
            ))}
          </dl>
        </section>
      )}
      {view?.evidence && (
        <section className={styles.section}>
          <h3 className={styles.heading}>Evidence</h3>
          {view.evidence.url && (
            <a
              className={styles.evidence}
              href={view.evidence.url}
              target="_blank"
              rel="noreferrer"
            >
              <span>{view.evidence.url.replace(/^https?:\/\//, '')}</span>
              <svg viewBox="0 0 20 20" aria-hidden="true">
                <path d="M8 5h7v7M15 5l-9 9" />
              </svg>
            </a>
          )}
          {view.evidence.ref && <p className={styles.ref}>ref {view.evidence.ref}</p>}
        </section>
      )}
      {statements && <AsSent statements={statements} />}
    </div>
  );
}

function Facts({
  view,
  onShowOnMap,
  onShowInTree,
  onFilter,
}: {
  view: RecordView;
  onShowOnMap: (point: LngLat) => void;
  onShowInTree: () => void;
  onFilter: OnFilter;
}) {
  return (
    <dl className={styles.facts}>
      {view.facts.map((fact, index) => (
        <div key={`${fact.label}-${index}`}>
          <dt>{fact.label}</dt>
          <dd>
            {fact.filter ? (
              <button
                type="button"
                className={styles.filterable}
                title={
                  fact.filter.field === 'time'
                    ? 'Set the time window to this date'
                    : `Filter the view by this ${fact.filter.field === 'logical' ? 'place' : fact.filter.field}`
                }
                onClick={() => onFilter(fact.filter!.field, fact.filter!.value)}
              >
                {fact.value}
                <svg viewBox="0 0 20 20" aria-hidden="true">
                  <path d="M3.5 4.5h13l-5 6v5l-3-1.5v-3.5z" />
                </svg>
              </button>
            ) : (
              <span>{fact.value}</span>
            )}
            {fact.code && <code>{fact.code}</code>}
            {fact.label === 'Point' && view.point && (
              <button
                type="button"
                className={styles.pill}
                onClick={() => onShowOnMap(view.point!)}
              >
                <svg viewBox="0 0 20 20" aria-hidden="true">
                  <circle cx="10" cy="10" r="4.5" />
                  <path d="M10 2.5v3M10 14.5v3M2.5 10h3M14.5 10h3" />
                </svg>
                Show on map
              </button>
            )}
            {fact.label === 'Filed under' && view.logical && (
              <button type="button" className={styles.pill} onClick={onShowInTree}>
                <svg viewBox="0 0 20 20" aria-hidden="true">
                  <rect x="3" y="3.5" width="5" height="3.5" rx="1" />
                  <rect x="10" y="9" width="6.5" height="3" rx="1" />
                  <rect x="10" y="14.5" width="6.5" height="3" rx="1" />
                  <path d="M5.5 7v8.5H10M5.5 10.5H10" />
                </svg>
                Show in the tree
              </button>
            )}
          </dd>
        </div>
      ))}
    </dl>
  );
}

function Connections({
  graph,
  named,
  onOpen,
  onOpenGraph,
}: {
  graph: ObjectAnswers['graph'];
  named: Named[];
  onOpen: (focus: Focus) => void;
  onOpenGraph?: () => void;
}) {
  const answer: WireGraph | undefined = current(graph);
  const groups = useMemo(() => (answer ? groupsOf(answer) : []), [answer]);
  const notInGraph = graph.error instanceof ApiError && graph.error.status === 404;
  const cut = answer !== undefined && answer.total > answer.links.length;

  return (
    <section className={styles.section}>
      <h3 className={styles.heading}>
        Connections
        {answer && (
          <span>
            {answer.links.length.toLocaleString('en')} of {answer.total.toLocaleString('en')}
          </span>
        )}
      </h3>
      {graph.pending && <Loading lines={3} />}
      {notInGraph && (
        <p className={styles.quiet}>
          <b>Not in the graph.</b> No connection is known for this record.
        </p>
      )}
      {graph.error && !notInGraph && <Failure error={graph.error} />}
      {answer && answer.total === 0 && <p className={styles.quiet}>No connection.</p>}
      {cut && (
        <p className={styles.notice}>
          The API sent {answer.links.length.toLocaleString('en')} of its{' '}
          {answer.total.toLocaleString('en')} connections; the groups below count only those.
        </p>
      )}
      {onOpenGraph && answer && answer.total > 0 && (
        <button type="button" className={styles.openGraph} onClick={onOpenGraph}>
          <svg viewBox="0 0 20 20" aria-hidden="true">
            <circle cx="10" cy="10" r="2.2" />
            <circle cx="4" cy="5" r="1.5" />
            <circle cx="16" cy="6" r="1.5" />
            <circle cx="6" cy="16" r="1.5" />
            <circle cx="15.5" cy="15" r="1.5" />
            <path d="M8.3 8.6 5.2 6M11.9 9.1l2.8-2.2M8.6 11.7l-1.6 3M11.7 11.5l2.6 2.4" />
          </svg>
          Open the connections graph
          <svg viewBox="0 0 20 20" aria-hidden="true">
            <path d="m8 5 5 5-5 5" />
          </svg>
        </button>
      )}
      {groups.map((group) => (
        <Group key={group.key} group={group} onOpen={onOpen} />
      ))}
      {!answer && !graph.pending && named.length > 0 && (
        <div className={styles.group}>
          <h4 className={styles.groupHead}>
            <span>Named in the record</span>
            <b>{named.length}</b>
          </h4>
          <ul className={styles.links}>
            {named.map((entry) => (
              <li key={`${entry.role}-${entry.id}`}>
                <LinkRow
                  label={entry.id}
                  detail={entry.role}
                  color="var(--text-3)"
                  onClick={() => onOpen({ id: entry.id })}
                />
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

function Group({ group, onOpen }: { group: LinkGroup; onOpen: (focus: Focus) => void }) {
  const [all, setAll] = useState(false);
  const rows = all ? group.neighbours : group.neighbours.slice(0, firstRows);
  return (
    <div className={styles.group}>
      <h4 className={styles.groupHead}>
        <span>{group.title}</span>
        <b>{group.neighbours.length.toLocaleString('en')}</b>
        <code>{group.linkType}</code>
      </h4>
      <ul className={styles.links}>
        {rows.map((neighbour, index) => (
          <li key={`${neighbour.id}-${index}`}>
            <LinkRow
              label={neighbour.label}
              detail={neighbour.detail}
              color={neighbour.color}
              onClick={() => onOpen(focusOf(neighbour))}
            />
          </li>
        ))}
      </ul>
      {group.neighbours.length > firstRows && (
        <button type="button" className={styles.more} onClick={() => setAll(!all)}>
          {all ? 'Fewer' : `All ${group.neighbours.length.toLocaleString('en')}`}
        </button>
      )}
    </div>
  );
}

function LinkRow({
  label,
  detail,
  color,
  onClick,
}: {
  label: string;
  detail: string;
  color: string;
  onClick: () => void;
}) {
  return (
    <button type="button" className={styles.link} onClick={onClick}>
      <i style={{ background: color, boxShadow: `0 0 8px ${color}` }} />
      <span className={styles.linkLabel}>{label}</span>
      {detail && <span className={styles.linkDetail}>{detail}</span>}
      <svg viewBox="0 0 20 20" aria-hidden="true">
        <path d="m8 5 5 5-5 5" />
      </svg>
    </button>
  );
}

function AsSent({ statements }: { statements: WireRecordAnswer[] }) {
  return (
    <section className={styles.section}>
      <h3 className={styles.heading}>
        As each source sent it
        <span>
          {statements.length} {statements.length === 1 ? 'source' : 'sources'}
        </span>
      </h3>
      {statements.map((statement) => (
        <Statement key={statement.source} statement={statement} />
      ))}
    </section>
  );
}

function Statement({ statement }: { statement: WireRecordAnswer }) {
  const [open, setOpen] = useState(false);
  const tokens = useMemo(
    () => (open ? tokensOf(JSON.stringify(statement.record, null, 2)) : []),
    [open, statement.record],
  );
  return (
    <div className={styles.sent}>
      <button
        type="button"
        className={styles.sentHead}
        aria-expanded={open}
        onClick={() => setOpen(!open)}
      >
        <span className={styles.sentSource}>{sourceName(statement.source)}</span>
        <code>state {statement.state}</code>
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path d="m6 8 4 4 4-4" />
        </svg>
        <span className={styles.sentMeta}>
          received {formatTime(statement.received_at, 'minute')} · {statement.producer}
        </span>
      </button>
      {open && (
        <pre className={styles.json}>
          {tokens.map((token, index) => (
            <span key={index} data-kind={token.kind}>
              {token.text}
            </span>
          ))}
        </pre>
      )}
    </div>
  );
}

function CopyId({ id }: { id: string }) {
  const [copied, setCopied] = useState(false);
  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(false), 1400);
    return () => clearTimeout(timer);
  }, [copied]);
  return (
    <button
      type="button"
      className={styles.copy}
      title="Copy the id"
      onClick={() => {
        void navigator.clipboard?.writeText(id).then(() => setCopied(true));
      }}
    >
      {id}
      <span>{copied ? 'copied' : 'copy'}</span>
    </button>
  );
}

/** A failed answer; in the development mode, a request with no saved answer is said quietly. */
function Failure({ error }: { error: Error }) {
  if (error instanceof ApiError && error.status === 501) {
    return (
      <p className={styles.quiet}>
        <b>Not in the fixtures.</b> The development mode has no saved answer for this request.
      </p>
    );
  }
  return (
    <p className={styles.failed} role="alert">
      {error.message}
    </p>
  );
}

function Loading({ lines }: { lines: number }) {
  return (
    <div className={styles.loading} aria-label="Reading…">
      {Array.from({ length: lines }, (_, index) => (
        <i key={index} style={{ width: `${92 - ((index * 17) % 40)}%` }} />
      ))}
    </div>
  );
}

/** A connected record as the next place in the trail, named as the graph names it. */
function focusOf(neighbour: Neighbour): Focus {
  const type = typeof neighbour.node.type === 'string' ? neighbour.node.type : '';
  return {
    id: neighbour.id,
    heading: {
      kind: neighbour.kind,
      type,
      title: neighbour.label,
      ...familyOf(neighbour.kind, type),
    },
  };
}
