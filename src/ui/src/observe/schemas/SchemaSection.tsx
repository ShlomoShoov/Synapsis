/**
 * Section 03, the schema map: every schema package the API holds, what each defines, and how they connect, on a
 * plane that moves and zooms (SchemaMap.tsx), with the details of what is selected beside it (Inspector.tsx).
 *
 * The packages are read once per data mode and again every minute (ObserveDataProvider); the graph (graph.ts) and the
 * layout (layout.ts) follow them. Source rows show what the log says of each source: records in the last 24 hours
 * and whether its feeder runs on time.
 *
 * Keys while the section is on screen: "/" searches, "0" fits the map, "+" and "−" zoom, Escape clears.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useObserveData } from '../data/observeContext';
import { useResource } from '../data/useResource';
import { useNow } from '../lib/clock';
import { formatCount } from '../lib/format';
import { ignoreShortcut } from '../lib/keyboard';
import { DAY, MINUTE } from '../lib/time';
import { useInView } from '../lib/useInView';
import { freshnessOf } from '../metrics/freshness';
import { SectionHeader } from '../shell/SectionHeader';
import { sectionInfo } from '../shell/sections';
import controls from '../ui/controls.module.css';
import { Icon } from '../ui/Icon';
import { highlightOf, sameTarget, type Target } from './focus';
import { buildSchemaGraph, kindInfo } from './graph';
import { Inspector } from './Inspector';
import { KindGlyph } from './KindGlyph';
import { layoutSchemaGraph } from './layout';
import type { SourceLive } from './PackageCard';
import { SchemaMap, type MapHandle } from './SchemaMap';
import styles from './SchemaSection.module.css';
import { searchGraph, type SearchResult } from './search';

const section = sectionInfo('schemas');
const inspectorWidth = 380;

/** The target of a search result. */
function targetOf(result: SearchResult): Target {
  return result.type === 'package'
    ? { type: 'package', name: result.name }
    : { type: 'definition', id: result.definition.id };
}

/** The schema map section. */
export function SchemaSection() {
  const { schemas, catalog, mode, setMode, log, registry } = useObserveData();
  const documents = schemas.data;
  const graph = useMemo(
    () =>
      documents
        ? buildSchemaGraph(
            documents,
            (document) => catalog.packageDomains.get(document.name) ?? 'other',
          )
        : null,
    [documents, catalog],
  );
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set());
  const layout = useMemo(
    () => (graph ? layoutSchemaGraph(graph, expanded) : null),
    [graph, expanded],
  );

  // What the log says of each source: the last 24 hours, and its runs over 30 days (read every 5 minutes).
  const at = useNow(5 * MINUTE);
  const windows = useResource(`schema-windows:${mode}:${at}`, (signal) =>
    log.sourceWindows({ range: { from: at - DAY, to: at } }, signal),
  );
  const history = useResource(`schema-history:${mode}:${at}`, (signal) =>
    log.sourceHistory({ to: at, lookbackMs: 30 * DAY }, signal),
  );
  const live = useMemo(() => {
    const map = new Map<string, SourceLive>();
    const recent = new Map((windows.data ?? []).map((item) => [item.source, item]));
    for (const item of history.data ?? []) {
      const window = recent.get(item.source);
      map.set(item.source, {
        received: window?.received ?? 0,
        rejected: window?.rejected ?? 0,
        last: item.last || null,
        freshness: freshnessOf(item, at),
      });
    }
    return map;
  }, [windows.data, history.data, at]);

  const [selected, setSelected] = useState<Target | null>(null);
  const [hover, setHover] = useState<Target | null>(null);
  const hoverTimer = useRef(0);
  // Leaving a row clears the light a moment later, so moving to the next row does not flash the whole map.
  const onHover = useCallback((target: Target | null) => {
    window.clearTimeout(hoverTimer.current);
    if (target) {
      setHover((previous) => (sameTarget(previous, target) ? previous : target));
    } else {
      hoverTimer.current = window.setTimeout(() => setHover(null), 80);
    }
  }, []);
  const highlight = useMemo(
    () => (graph ? highlightOf(graph, hover ?? selected) : null),
    [graph, hover, selected],
  );
  const map = useRef<MapHandle>(null);
  const onSelect = useCallback((target: Target) => {
    setSelected((previous) => (sameTarget(previous, target) ? null : target));
  }, []);
  const choose = useCallback((target: Target) => {
    setSelected(target);
    map.current?.show(target);
  }, []);
  const onToggle = useCallback((name: string) => {
    setExpanded((previous) => {
      const next = new Set(previous);
      if (!next.delete(name)) {
        next.add(name);
      }
      return next;
    });
  }, []);

  // Search.
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const search = useRef<HTMLInputElement>(null);
  const results = useMemo(() => (graph ? searchGraph(graph, query, 8) : []), [graph, query]);
  const pick = (result: SearchResult | undefined) => {
    if (result) {
      choose(targetOf(result));
      setQuery('');
      search.current?.blur();
    }
  };

  // Keys while the section is on screen.
  const [element, setElement] = useState<HTMLElement | null>(null);
  const inView = useInView(element);
  useEffect(() => {
    if (!inView) {
      return;
    }
    const onKey = (event: KeyboardEvent) => {
      if (ignoreShortcut(event)) {
        return;
      }
      if (event.key === '/') {
        event.preventDefault();
        search.current?.focus();
      } else if (event.key === '0') {
        map.current?.fit();
      } else if (event.key === '+' || event.key === '=') {
        map.current?.zoomBy(1.4);
      } else if (event.key === '-') {
        map.current?.zoomBy(1 / 1.4);
      } else if (event.key === 'Escape') {
        setSelected(null);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [inView]);

  const sources = graph
    ? graph.packages.reduce(
        (sum, pkg) => sum + pkg.definitions.filter((item) => item.kind === 'source').length,
        0,
      )
    : 0;
  const events = graph
    ? graph.packages.reduce(
        (sum, pkg) => sum + pkg.definitions.filter((item) => item.kind === 'event').length,
        0,
      )
    : 0;
  const problems = graph ? graph.unresolved.length + graph.duplicates.length : 0;

  let body;
  if (graph && layout && highlight && graph.packages.length > 0) {
    body = (
      <div className={styles.stage}>
        <SchemaMap
          ref={map}
          graph={graph}
          layout={layout}
          highlight={highlight}
          selected={selected}
          live={live}
          coveredRight={selected ? inspectorWidth : 0}
          onSelect={onSelect}
          onHover={onHover}
          onToggle={onToggle}
        />
        {selected ? (
          <Inspector
            graph={graph}
            target={selected}
            live={live}
            now={at}
            onSelect={choose}
            onClose={() => setSelected(null)}
          />
        ) : null}
      </div>
    );
  } else {
    body = (
      <div className={styles.message}>
        {schemas.error && !documents ? (
          <>
            <Icon name="alert" size={18} />
            <p>
              The schema packages could not be read from {registry.location}:{' '}
              {schemas.error.message}
            </p>
            {mode === 'live' ? (
              <button
                type="button"
                className={controls.button}
                onClick={() => setMode('simulated')}
              >
                Use simulated data
              </button>
            ) : null}
          </>
        ) : documents ? (
          <p>The API holds no schema packages yet. Register one with PUT /schemas/{'{package}'}.</p>
        ) : (
          <p>Reading the schema packages from {registry.location}…</p>
        )}
      </div>
    );
  }

  return (
    <section
      ref={setElement}
      id="schemas"
      data-nav-section="schemas"
      className={styles.section}
      aria-labelledby="schemas-title"
    >
      <SectionHeader section={section}>
        {graph ? (
          <p className={styles.counts}>
            {formatCount(graph.packages.length)} packages · {formatCount(sources)} sources ·{' '}
            {formatCount(events)} event types
            {problems ? (
              <span className={styles.problems}>
                <Icon name="warning" size={13} /> {problems} definition{problems === 1 ? '' : 's'}{' '}
                to check
              </span>
            ) : null}
          </p>
        ) : null}
      </SectionHeader>

      <div className={styles.toolbar}>
        <div className={styles.search}>
          <Icon name="search" size={15} />
          <input
            ref={search}
            type="search"
            value={query}
            placeholder="Find a package, source, type or measure"
            aria-label="Find on the map"
            onChange={(event) => {
              setQuery(event.target.value);
              setActive(0);
            }}
            onKeyDown={(event) => {
              if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault();
                setActive(
                  (index) =>
                    (index + (event.key === 'ArrowDown' ? 1 : -1) + results.length) %
                    Math.max(1, results.length),
                );
              } else if (event.key === 'Enter') {
                pick(results[active]);
              } else if (event.key === 'Escape') {
                setQuery('');
              }
            }}
          />
          <kbd className={controls.kbd}>/</kbd>
          {query && results.length ? (
            <ul className={styles.results} aria-label="Matches">
              {results.map((result, index) => (
                <li key={result.type === 'package' ? `p:${result.name}` : result.definition.id}>
                  <button
                    type="button"
                    className={styles.result}
                    data-active={index === active || undefined}
                    onMouseDown={(event) => event.preventDefault()}
                    onClick={() => pick(result)}
                  >
                    {result.type === 'package' ? (
                      <>
                        <Icon name="layers" size={13} />
                        <span className={styles.resultCode}>{result.name}</span>
                        <span className={styles.resultNote}>Package · {result.title}</span>
                      </>
                    ) : (
                      <>
                        <KindGlyph kind={result.definition.kind} />
                        <span className={styles.resultCode}>{result.definition.code}</span>
                        <span className={styles.resultNote}>
                          {kindInfo[result.definition.kind].one} · {result.definition.packageName}
                        </span>
                      </>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
        <p className={styles.legend}>
          <span className={styles.legendLine} aria-hidden="true" />A definition used from another
          package, in the colour of the user’s domain
        </p>
      </div>

      {body}
    </section>
  );
}
