/**
 * GRAPH-D, the connections graph: the object at the centre, its connections coming out of it in 3D, one direction
 * per kind. A click on a node opens it (the object panel moves to it) and grows its own connections out of it, so the
 * investigation goes on from any node. The key, bottom left, counts what is drawn and hides a kind with a click.
 *
 * The connections are GET /graph?id=: the panel's answer for the object looked at, and up to 1,000 on request when
 * the API sent fewer than the node has. A 404 is a node with no connection in the graph.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { fetchGraph } from '../api/client';
import type { WireGraph, WireGraphNode } from '../api/contract';
import { ApiError } from '../api/http';
import type { Latest } from '../lib/useLatest';
import { useReducedMotion } from '../lib/useReducedMotion';
import controlStyles from '../map/MapControls.module.css';
import { familyOf, kindColors, kindOfId, words } from '../object/describe';
import { current, type Focus } from '../object/useObject';
import { matches, type ViewFilter } from '../filter/viewFilter';
import { GraphScene } from './GraphScene';
import { emptyGraph, expand, nodeOf, type GraphNode, type GraphState } from './layout';
import styles from './GraphView.module.css';

interface GraphViewProps {
  trail: Focus[];
  /** The panel's answer of GET /graph for the object looked at. */
  graph: Latest<WireGraph>;
  /** The width kept clear on the right for the object panel. */
  inset: number;
  onOpen: (focus: Focus) => void;
  /** Where the key goes (the left column) and where the view buttons go. */
  keySlot: HTMLElement | null;
  /** The view's filter: the drawn nodes that do not match disappear (the object and the first node stay). */
  filter: ViewFilter;
  controlsSlot: HTMLElement | null;
}

/** Up to this many nodes drawn, every node has its name written; beyond, only the ones opened. */
const namedUpTo = 28;
/** The most links /graph sends. */
const mostLinks = 1000;

const kindNames = { event: 'Events', entity: 'Entities', conclusion: 'Conclusions' };

export function GraphView({
  trail,
  graph,
  filter,
  inset,
  onOpen,
  keySlot,
  controlsSlot,
}: GraphViewProps) {
  const focus = trail.at(-1)!;
  const [scene, setScene] = useState<GraphScene | null>(null);
  const [state, setState] = useState<GraphState>(emptyGraph);
  const [hidden, setHidden] = useState<Set<string>>(() => new Set());
  const [hover, setHover] = useState<{ id: string; x: number; y: number } | null>(null);
  // Reading more links: for which object, and how it went.
  const [moreOf, setMoreOf] = useState<{ id: string; state: 'reading' | Error } | null>(null);
  const more = moreOf?.id === focus.id ? moreOf.state : null;
  const still = useReducedMotion();
  const answer = current(graph);
  const notInGraph = graph.error instanceof ApiError && graph.error.status === 404;

  // The newest callbacks, for the scene's listeners.
  const latest = useRef({ onOpen, state });
  useEffect(() => {
    latest.current = { onOpen, state };
  });

  // The scene lives as long as its container (a ref callback, so it is made once the element is there).
  const containerRef = useCallback(
    (container: HTMLDivElement | null) => {
      if (!container) return;
      const made = new GraphScene(
        container,
        {
          onHover: (id, x, y) => setHover(id ? { id, x, y } : null),
          onPick: (id) => {
            const node = latest.current.state.nodes.get(id);
            if (node) latest.current.onOpen(focusOf(node));
          },
        },
        still,
      );
      setScene(made);
      return () => {
        made.dispose();
        setScene(null);
      };
    },
    [still],
  );

  // The object's connections grow out of it (once per answer); an object not in the graph yet starts a new one.
  const [grownFrom, setGrownFrom] = useState<WireGraph | undefined>(undefined);
  if (answer && answer !== grownFrom) {
    setGrownFrom(answer);
    setState((previous) =>
      expand(
        previous.nodes.has(answer.center.id) ? previous : emptyGraph(),
        answer,
        performance.now(),
      ),
    );
  }

  // Not in the graph: the object alone, unless it is already a node.
  if (notInGraph && !state.nodes.has(focus.id)) {
    setState(() => {
      const heading = focus.heading;
      const node: WireGraphNode = {
        id: focus.id,
        labels: [words(heading?.kind ?? kindOfId(focus.id))],
        name: heading?.title ?? focus.id,
        type: heading?.type ?? '',
      };
      return {
        ...emptyGraph(),
        root: focus.id,
        nodes: new Map([[focus.id, nodeOf(node, [0, 0, 0], performance.now())]]),
      };
    });
  }

  const drawn = useMemo(
    () => shownOf(state, hidden, filter, focus.id),
    [state, hidden, filter, focus.id],
  );
  const trailIds = useMemo(() => trail.map((step) => step.id), [trail]);

  useEffect(() => {
    scene?.set({ state, nodes: drawn.nodes, edges: drawn.edges, focus: focus.id, trail: trailIds });
  }, [scene, state, drawn, focus.id, trailIds]);

  useEffect(() => {
    scene?.setInset(inset);
  }, [scene, inset]);

  // The view glides to the object when it changes (once it is a node).
  const glidedTo = useRef<string | null>(null);
  const focusPlaced = state.nodes.has(focus.id);
  useEffect(() => {
    if (!scene || !focusPlaced || glidedTo.current === focus.id) return;
    glidedTo.current = focus.id;
    scene.focusOn(focus.id);
  }, [scene, focusPlaced, focus.id]);

  const centre = state.nodes.get(focus.id);
  const named = drawn.nodes.size <= namedUpTo;
  const labelled = [...drawn.nodes].filter(
    (id) =>
      named ||
      id === focus.id ||
      id === state.root ||
      trailIds.includes(id) ||
      state.nodes.get(id)?.total !== undefined,
  );
  const groups = [...state.groups.values()].filter(
    (group) =>
      group.owner === focus.id &&
      [...drawn.edges].some((id) => state.edges.get(id)?.group === group.key),
  );
  const hovered = hover ? state.nodes.get(hover.id) : undefined;

  const loadMore = () => {
    const id = focus.id;
    setMoreOf({ id, state: 'reading' });
    fetchGraph(id, mostLinks).then(
      (wire) => {
        setState((previous) => expand(previous, wire, performance.now()));
        setMoreOf(null);
      },
      (error: unknown) =>
        setMoreOf({ id, state: error instanceof Error ? error : new Error(String(error)) }),
    );
  };

  return (
    <div className={styles.stage}>
      <div ref={containerRef} className={styles.canvas} />

      {scene &&
        labelled.map((id) => {
          const node = state.nodes.get(id)!;
          return (
            <div
              key={id}
              ref={(element) => scene.bindLabel(`n:${id}`, element, () => scene.positionOf(id))}
              className={styles.label}
              data-focus={id === focus.id || undefined}
              data-opened={node.total !== undefined || undefined}
              data-conclusion={node.kind === 'conclusion' || undefined}
            >
              <span>{node.label}</span>
            </div>
          );
        })}
      {scene &&
        groups.map((group) => (
          <div
            key={group.key}
            ref={(element) => scene.bindLabel(`g:${group.key}`, element, () => group.anchor)}
            className={styles.group}
            style={{ color: group.color }}
          >
            {group.title}
            {(group.linkType === 'ABOUT' || group.linkType === 'PREMISE') && (
              <code>{group.linkType}</code>
            )}
            <b>{group.count.toLocaleString('en')}</b>
          </div>
        ))}

      <output className={styles.status}>
        {graph.pending && !centre?.total && (
          <span className={styles.reading}>Reading connections</span>
        )}
        {notInGraph && (
          <span>
            <b>Not in the graph.</b> No connection is known for {focus.heading?.title ?? focus.id}.
          </span>
        )}
        {centre?.total !== undefined && (
          <span>
            <b>{centre.shown?.toLocaleString('en')}</b> of {centre.total.toLocaleString('en')}{' '}
            {centre.total === 1 ? 'connection' : 'connections'} of <em>{centre.label}</em>
          </span>
        )}
        {centre?.total !== undefined &&
          centre.shown !== undefined &&
          centre.total > centre.shown &&
          centre.shown < mostLinks &&
          more === null && (
            <button type="button" onClick={loadMore}>
              Load up to {Math.min(centre.total, mostLinks).toLocaleString('en')}
            </button>
          )}
        {more === 'reading' && <span className={styles.reading}>Reading</span>}
        {more instanceof Error && <span className={styles.failed}>{more.message}</span>}
      </output>

      {hover && hovered && (
        <div
          className={styles.hover}
          style={{
            transform: `translate(${hover.x + 16}px, ${hover.y + 16}px)`,
            // Under the panel it would be hidden: it opens to the left of the pointer instead.
            translate:
              hover.x > window.innerWidth - inset - 380 ? 'calc(-100% - 32px) 0' : undefined,
          }}
        >
          <span className={styles.hoverTitle}>
            <i style={{ background: hovered.color, boxShadow: `0 0 10px ${hovered.color}` }} />
            {hovered.label}
          </span>
          <span className={styles.hoverLine}>
            {[words(hovered.kind), hovered.type].filter(Boolean).join(' · ')}
          </span>
          <span className={styles.hoverLine}>
            {hovered.total !== undefined
              ? `${hovered.shown?.toLocaleString('en')} of ${hovered.total.toLocaleString('en')} connections`
              : hovered.id === focus.id
                ? 'Open'
                : 'Click to open it and its connections'}
          </span>
        </div>
      )}

      {keySlot &&
        createPortal(
          <GraphKey
            state={state}
            drawn={drawn}
            hidden={hidden}
            onToggle={(key) => setHidden(toggled(hidden, key))}
          />,
          keySlot,
        )}
      {controlsSlot &&
        createPortal(
          <fieldset className={controlStyles.controls}>
            <legend className={controlStyles.hidden}>Graph view</legend>
            <div className={controlStyles.group}>
              <button
                type="button"
                aria-label="Closer"
                title="Closer"
                onClick={() => scene?.zoomBy(0.7)}
              >
                <svg viewBox="0 0 20 20" aria-hidden="true">
                  <path d="M10 5v10M5 10h10" />
                </svg>
              </button>
              <span className={controlStyles.rule} />
              <button
                type="button"
                aria-label="Farther"
                title="Farther"
                onClick={() => scene?.zoomBy(1.4)}
              >
                <svg viewBox="0 0 20 20" aria-hidden="true">
                  <path d="M5 10h10" />
                </svg>
              </button>
            </div>
            <button
              type="button"
              className={controlStyles.single}
              aria-label="Back to the object"
              title="Back to the object"
              onClick={() => scene?.focusOn(focus.id)}
            >
              <svg viewBox="0 0 20 20" aria-hidden="true">
                <circle cx="10" cy="10" r="4.5" />
                <path d="M10 2.5v3M10 14.5v3M2.5 10h3M14.5 10h3" />
              </svg>
            </button>
            <button
              type="button"
              className={controlStyles.single}
              aria-label="The whole graph"
              title="The whole graph"
              onClick={() => scene?.showAll()}
            >
              <svg viewBox="0 0 20 20" aria-hidden="true">
                <circle cx="10" cy="10" r="2" />
                <circle cx="4" cy="5" r="1.5" />
                <circle cx="16" cy="6" r="1.5" />
                <circle cx="6" cy="16" r="1.5" />
                <path d="M8.4 8.8 5.2 6M11.8 9.2 14.6 6.8M8.8 11.6 6.9 14.6" />
              </svg>
            </button>
          </fieldset>,
          controlsSlot,
        )}
    </div>
  );
}

function GraphKey({
  state,
  drawn,
  hidden,
  onToggle,
}: {
  state: GraphState;
  drawn: { nodes: Set<string>; edges: Set<string> };
  hidden: Set<string>;
  onToggle: (key: string) => void;
}) {
  const kinds = new Map<string, number>();
  for (const node of state.nodes.values()) {
    kinds.set(node.kind, (kinds.get(node.kind) ?? 0) + (drawn.nodes.has(node.id) ? 1 : 0));
  }
  const links = new Map<string, number>();
  for (const edge of state.edges.values()) {
    links.set(edge.linkType, (links.get(edge.linkType) ?? 0) + (drawn.edges.has(edge.id) ? 1 : 0));
  }
  return (
    <aside className={styles.key} aria-label="Key and filters">
      <p className={styles.total}>
        <strong>{drawn.nodes.size.toLocaleString('en')}</strong>
        {drawn.nodes.size === 1 ? 'node' : 'nodes'} · {drawn.edges.size.toLocaleString('en')}{' '}
        {drawn.edges.size === 1 ? 'link' : 'links'} drawn
      </p>
      <div className={styles.chips} aria-label="Kinds of record">
        {[...kinds].map(([kind, count]) => (
          <button
            key={kind}
            type="button"
            aria-pressed={!hidden.has(`kind:${kind}`)}
            onClick={() => onToggle(`kind:${kind}`)}
          >
            <i
              style={{
                background: kind === 'event' ? eventsColor : kindColors[kind as 'entity'],
              }}
            />
            {kindNames[kind as 'event']}
            <b>{count.toLocaleString('en')}</b>
          </button>
        ))}
      </div>
      <div className={styles.chips} aria-label="Kinds of connection">
        {[...links].map(([linkType, count]) => (
          <button
            key={linkType}
            type="button"
            data-link=""
            aria-pressed={!hidden.has(`link:${linkType}`)}
            onClick={() => onToggle(`link:${linkType}`)}
          >
            {words(linkType)}
            <b>{count.toLocaleString('en')}</b>
          </button>
        ))}
      </div>
      <p className={styles.hint}>
        A click on a node opens it and grows its connections; a click on a kind hides it. Drag to
        turn, right-drag to move, scroll to zoom.
      </p>
    </aside>
  );
}

/** The key's colour for events (each event shines in its category's colour). */
const eventsColor = 'linear-gradient(135deg, #ff7a45, #a98bff 55%, #3ddcff)';

/** The nodes and links drawn: those of the kinds not hidden; the object and the first node always. */
function shownOf(state: GraphState, hidden: Set<string>, filter: ViewFilter, focusId: string) {
  const nodes = new Set<string>();
  const edges = new Set<string>();
  const kept = (node: GraphNode) =>
    node.id === focusId ||
    node.id === state.root ||
    (!hidden.has(`kind:${node.kind}`) &&
      matches(filter, { kind: node.kind, type: node.type, sources: sourcesOf(node) }));
  for (const edge of state.edges.values()) {
    const owner = state.nodes.get(edge.owner);
    const other = state.nodes.get(edge.other);
    if (!owner || !other || hidden.has(`link:${edge.linkType}`) || !kept(owner) || !kept(other)) {
      continue;
    }
    edges.add(edge.id);
    nodes.add(owner.id);
    nodes.add(other.id);
  }
  if (state.root) nodes.add(state.root);
  if (state.nodes.has(focusId)) nodes.add(focusId);
  return { nodes, edges };
}

/** A node's sources: an event's source, an entity's list. */
function sourcesOf(node: GraphNode): string[] {
  const { source, sources } = node.node;
  if (typeof source === 'string') return [source];
  return Array.isArray(sources) ? sources.filter((s): s is string => typeof s === 'string') : [];
}

function toggled(set: Set<string>, key: string): Set<string> {
  const next = new Set(set);
  if (next.has(key)) next.delete(key);
  else next.add(key);
  return next;
}

/** A node as the next place in the trail. */
function focusOf(node: GraphNode): Focus {
  return {
    id: node.id,
    heading: {
      kind: node.kind,
      type: node.type,
      title: node.label,
      ...familyOf(node.kind, node.type),
    },
  };
}
