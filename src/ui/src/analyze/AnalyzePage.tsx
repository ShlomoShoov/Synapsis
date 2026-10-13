/**
 * Analyze (route /analyze): where the user investigates the data. The night Earth fills the page in a sky of stars, the
 * events shine on it as lights coloured by category. Over it: the time card (top right), the key (bottom left), the
 * query panel (left), and the view buttons (right). A click on an event opens it in the object panel (right, under
 * the time card), and marks it on the globe; a click on the map away from the lights, Esc or the panel's close
 * returns to the map alone. The switch at the top shows another dimension over the globe: the graph of the open
 * object's connections (graph/), or the logical tree (logical/), where the open object's place is opened. Left, above
 * the key, the filter of the view (filter/): what does not match disappears from every dimension. The search window
 * (search/, the button at the top or "/") finds records with every parameter of GET /search.
 *
 * The address can open a window, a moment of knowledge and a place: ?from=1970-01-01&to=1971-01-01 (the second date
 * excluded), &as_of=2026-10-01, &view=35.5,33.4,7 (longitude, latitude, zoom, and optionally pitch and bearing), and
 * an object: &object=evt:ucdp:7578, seen in a dimension: &dimension=graph (or logical).
 */
import '@fontsource-variable/instrument-sans/wght.css';
import '@fontsource/fragment-mono/400.css';
import { lazy, Suspense, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { DimensionSwitch, type Dimension } from './DimensionSwitch';
import { EventsKey } from './map/EventsKey';
import { GlobeMap, type Camera, type GlobeController, type MapLook } from './map/GlobeMap';
import { MapControls } from './map/MapControls';
import { useEvents } from './map/useEvents';
import { requestOf } from './map/viewport';
import { fetchCatalog } from './api/client';
import type { Item } from './api/model';
import { FilterCard } from './filter/FilterCard';
import { noFilter, timeWindowOf, withValue, type FilterField } from './filter/viewFilter';
import { useLatest } from './lib/useLatest';
import { LogicalView } from './logical/LogicalView';
import { SearchSheet } from './search/SearchSheet';
import { headingOfItem, headingOfRecord } from './object/describe';
import { ObjectPanel } from './object/ObjectPanel';
import { current, useObject, type Focus } from './object/useObject';
import { QueryPanel } from './query/QueryPanel';
import { TimeCard } from './time/TimeCard';
import { useLive } from './time/useLive';
import { fromDayInput, lastMonth, type TimeWindow } from './time/window';
import styles from './AnalyzePage.module.css';

// The graph carries three.js: its code loads when it is first opened.
const GraphView = lazy(async () => ({ default: (await import('./graph/GraphView')).GraphView }));

/** The width the object panel takes on the right (its width and the gaps around it), kept clear on the globe. */
const panelInset = 388 + 18 + 18;

export function AnalyzePage() {
  const [searchParams] = useSearchParams();
  // The moment the page opened: "today" for the timeline (a page left open over midnight keeps its day).
  const [now] = useState(() => Date.now());
  const [timeWindow, setTimeWindow] = useState<TimeWindow>(() =>
    windowFromAddress(searchParams, now),
  );
  const [asOf, setAsOf] = useState<number | undefined>(
    () => fromDayInput(searchParams.get('as_of') ?? '') ?? undefined,
  );
  const [initialView] = useState(() => viewFromAddress(searchParams));
  const [look, setLook] = useState<MapLook | null>(null);
  const [camera, setCamera] = useState({ bearing: 0, pitch: 0 });
  const [controller, setController] = useState<GlobeController | null>(null);

  const request = look ? requestOf(look.zoom, look.bounds) : null;
  // The read API has no as_of (EMERGENCY.md §12): the control stays, the map reads what is known now.
  // The view's filter: the map sends source, type and kind with the time card's window.
  const [viewFilter, setViewFilter] = useState(noFilter);
  const filters = {
    from: timeWindow.from,
    to: timeWindow.to,
    sources: viewFilter.sources,
    types: viewFilter.types,
  };
  const {
    answer: events,
    pending,
    error,
  } = useEvents(filters, request, viewFilter.kind ?? undefined);
  const catalog = useLatest('catalog', (signal) => fetchCatalog(signal));
  const [searching, setSearching] = useState(false);
  const live = useLive(timeWindow, (time) => controller?.setLive(time));

  // The object looked at: the trail of records the investigation went through, the last one shown.
  const [trail, setTrail] = useState<Focus[]>(() => {
    const id = searchParams.get('object');
    return id ? [{ id }] : [];
  });
  const focus = trail.at(-1) ?? null;
  const [dimension, setDimension] = useState<Dimension>(() => {
    const asked = searchParams.get('dimension');
    if (asked === 'logical') return 'logical';
    return asked === 'graph' && searchParams.get('object') ? 'graph' : 'map';
  });
  const [keySlot, setKeySlot] = useState<HTMLElement | null>(null);
  const [controlsSlot, setControlsSlot] = useState<HTMLElement | null>(null);
  const onMap = dimension === 'map';
  // Closing the object leaves the graph (it shows an object) for the map; the tree stays.
  const close = () => {
    setTrail([]);
    setDimension((shown) => (shown === 'graph' ? 'map' : shown));
  };
  const answers = useObject(focus?.id ?? null);
  const record = current(answers.records)?.[0]?.record;
  const place = focus?.point ?? record?.place?.point;
  const heading = record ? headingOfRecord(record) : focus?.heading;
  const color = heading?.color ?? '#8fb6ff';
  // The logical place of the object: known from where it was opened, or from its record (undefined while unknown).
  const filed = record?.place?.logical;
  const logicalPlace =
    focus?.logical ??
    (filed?.space && filed.path?.length
      ? [filed.space, ...filed.path].join('/')
      : answers.records.pending
        ? undefined
        : null);
  /** Moves the panel to another record, the next step of the trail. */
  const open = (next: Focus) =>
    setTrail((steps) => (steps.at(-1)?.id === next.id ? steps : [...steps, next]));
  const placeKey = focus && place ? `${focus.id}@${place.join(',')}` : null;

  // The globe turns to the object when it is hidden behind the panel or the Earth.
  useEffect(() => {
    if (onMap && placeKey && place) controller?.reveal(place, panelInset);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- once per object and place
  }, [placeKey, controller, onMap]);

  // Esc closes the search window, else returns to the map; "/" opens the search window.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      const typing = target?.closest('input, textarea, select');
      if (event.key === 'Escape' && searching) setSearching(false);
      else if (event.key === 'Escape' && focus && !typing) close();
      else if (event.key === '/' && !typing) {
        event.preventDefault();
        setSearching(true);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [focus, searching]);

  /** A field of a record becomes a filter of the view (its date, the time card's window). */
  const filterBy = (field: FilterField | 'time', value: string) => {
    if (field === 'time') {
      const window = timeWindowOf(value);
      if (window) setTimeWindow(window);
    } else {
      setViewFilter((current) => withValue(current, field, value));
    }
  };

  /** A search result: on the map when it has a place, else in the tree when it is filed; always in the panel. */
  const goTo = (item: Item) => {
    setSearching(false);
    setTrail([
      {
        id: item.id,
        heading: headingOfItem(item),
        point: item.place?.point,
        logical: item.logical[0],
      },
    ]);
    if (item.place?.point) setDimension('map');
    else if (item.logical.length > 0) setDimension('logical');
  };

  return (
    <div className={styles.page} data-object={focus ? '' : undefined}>
      <GlobeMap
        initialView={initialView}
        lights={events?.lights ?? null}
        onLookChange={setLook}
        onPick={(id, item) =>
          setTrail([
            { id, heading: item ? headingOfItem(item) : undefined, point: item?.place?.point },
          ])
        }
        onEmptyClick={close}
        selected={place ? { place, color } : null}
        onCameraChange={setCamera}
        onReady={setController}
      />

      {focus && dimension === 'graph' && (
        <Suspense fallback={null}>
          <GraphView
            trail={trail}
            graph={answers.graph}
            filter={viewFilter}
            inset={panelInset}
            onOpen={open}
            keySlot={keySlot}
            controlsSlot={controlsSlot}
          />
        </Suspense>
      )}
      {dimension === 'logical' && (
        <LogicalView
          key={`${viewFilter.sources.join(',')}|${viewFilter.types.join(',')}`}
          filters={{ sources: viewFilter.sources, types: viewFilter.types }}
          object={
            focus
              ? {
                  id: focus.id,
                  title: heading?.title ?? focus.id,
                  place: logicalPlace,
                }
              : null
          }
          onOpen={open}
        />
      )}
      <DimensionSwitch
        value={dimension}
        onChange={setDimension}
        hasObject={focus !== null}
        extra={
          <button
            type="button"
            className={styles.search}
            onClick={() => setSearching(true)}
            aria-label="Search (/)"
          >
            <svg viewBox="0 0 20 20" aria-hidden="true">
              <circle cx="8.5" cy="8.5" r="5" />
              <path d="m12.5 12.5 4 4" />
            </svg>
            Search
            <kbd>/</kbd>
          </button>
        }
      />
      {searching && (
        <SearchSheet
          start={viewFilter}
          catalog={catalog.answer}
          onResult={goTo}
          onClose={() => setSearching(false)}
        />
      )}

      <header className={styles.header}>
        <Link className={styles.brand} to="/home">
          Synapsis
        </Link>
        <span className={styles.area}>Analyze</span>
      </header>

      <div className={styles.left}>
        <QueryPanel />
        <FilterCard
          filter={viewFilter}
          onChange={setViewFilter}
          catalog={catalog.answer}
          onMap={onMap}
        />
        {onMap ? <EventsKey events={events} /> : <div ref={setKeySlot} />}
      </div>

      <div className={styles.right} data-object={focus ? '' : undefined}>
        <div className={styles.timeSlot} data-hidden={onMap ? undefined : ''}>
          <TimeCard
            window={timeWindow}
            onWindowChange={setTimeWindow}
            asOf={asOf}
            onAsOfChange={setAsOf}
            now={now}
            live={live}
            status={
              pending ? (
                <span className={styles.reading}>reading</span>
              ) : events ? (
                `${events.layer.events.toLocaleString('en')} shown`
              ) : null
            }
            liveNote={
              events?.layer.layer === 'cells'
                ? 'Too many events here to play one by one: zoom in or narrow the window.'
                : undefined
            }
          />
        </div>
        {onMap && error && (
          <p className={styles.error} role="alert">
            {error.message}
          </p>
        )}
        {focus && (
          <ObjectPanel
            trail={trail}
            answers={answers}
            onOpen={open}
            onTrail={(index) => setTrail((steps) => steps.slice(0, index + 1))}
            onClose={close}
            onOpenGraph={dimension === 'graph' ? undefined : () => setDimension('graph')}
            onShowInTree={() => setDimension('logical')}
            onFilter={filterBy}
            onShowOnMap={(point) => {
              setDimension('map');
              controller?.reveal(point, panelInset);
            }}
          />
        )}
      </div>

      <div className={styles.controls}>
        {onMap ? (
          <MapControls controller={controller} bearing={camera.bearing} pitch={camera.pitch} />
        ) : (
          <div ref={setControlsSlot} />
        )}
      </div>
    </div>
  );
}

/** The window named in the address (from, to: dates, to excluded), or the last month. */
function windowFromAddress(params: URLSearchParams, now: number): TimeWindow {
  const from = fromDayInput(params.get('from') ?? '');
  const to = fromDayInput(params.get('to') ?? '');
  if (from !== null && to !== null && from < to) return { from, to };
  return lastMonth(now);
}

/** The view named in the address: ?view=lon,lat,zoom[,pitch[,bearing]]. */
function viewFromAddress(params: URLSearchParams): Camera | undefined {
  const parts = (params.get('view') ?? '').split(',').map(Number);
  const [lon, lat, zoom, pitch = 0, bearing = 0] = parts;
  if (
    parts.length < 3 ||
    parts.some(Number.isNaN) ||
    lon === undefined ||
    lat === undefined ||
    zoom === undefined
  ) {
    return undefined;
  }
  return { center: [lon, lat], zoom, pitch, bearing };
}
