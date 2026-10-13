/**
 * The development mode without the stores (VITE_ANALYZE_FIXTURES=1): the read API answered from the real answers saved
 * in fixtures/analyze/ (its README gives the request behind each file). Nothing here is made up but the conclusions
 * (written by hand from the engine's contract while the stack was reloaded; the README says so):
 *
 * - /map/cells and /map/points are answered over map-points-world.json (every event with a point in the sample) and
 *   map-points-conclusions.json (conclusions written by hand from the engine's contract, see the README), with
 *   the API's own filters (bbox, from, to, source, type, kind, limit), so the map works in any window and place.
 * - /graph, /records, /logical and /search answer with the saved file of that exact request; the filters are not
 *   applied to them.
 * - Any other request answers 501 "not in the fixtures", so a missing file is never mistaken for an empty answer.
 *
 * Only http.ts calls this, and only in this mode; the production build leaves it out.
 */
import type { WireDoc, WireList } from './contract';

const files = import.meta.glob<unknown>('../../../fixtures/analyze/*.json', { import: 'default' });

/** An answer as the network would give it. */
export interface FixtureAnswer {
  status: number;
  text: string;
}

const graphs: Record<string, string> = {
  'ent:kev-product:apache/log4j2': 'graph-entity-log4j2',
  'evt:kev:CVE-2021-44228': 'graph-event-cve-2021-44228',
  'evt:ucdp:7578': 'graph-event-ucdp-7578',
  'ent:kev-vendor:microsoft': 'graph-hub-microsoft',
  'evt:gtd:197000000001': 'graph-not-found-gtd-197000000001',
  'con:engine:forecast:84bcf59ffffffff:1989-02': 'graph-conclusion-forecast',
};

const records: Record<string, string> = {
  'evt:kev:CVE-2021-44228': 'records-cve-2021-44228',
  'ent:kev-product:apache/log4j2': 'records-log4j2',
  'evt:ucdp:7578': 'records-ucdp-7578',
  'con:engine:forecast:84bcf59ffffffff:1989-02': 'records-conclusion-forecast',
  'con:engine:forecast_score:ucdp-ged:1989': 'records-conclusion-score',
};

const logical: Record<string, string> = {
  '': 'logical-top',
  'cyber.vendor_product': 'logical-cyber-vendor-product',
  'cyber.vendor_product/Apache': 'logical-cyber-vendor-product-apache',
};

/** The saved searches: the request's parameters (limit aside), and the file. */
const searches: [Record<string, string>, string][] = [
  [{ q: 'log4j' }, 'search-q-log4j'],
  [{ q: 'remote code execution', semantic: 'true' }, 'search-semantic-rce'],
  [{ source: 'src:cisa:kev' }, 'search-source-kev'],
  [{ entity: 'ent:kev-product:apache/log4j2' }, 'search-entity-log4j2'],
  [{ logical: 'cyber.vendor_product/Apache' }, 'search-logical-apache'],
  [{ kind: 'entity' }, 'search-kind-entity'],
  [{ kind: 'conclusion' }, 'search-kind-conclusion'],
];

/** A short wait, so loading states show as they will with the real API. */
const delayMs = 160;

export async function fixtureAnswer(url: string): Promise<FixtureAnswer> {
  await new Promise((resolve) => setTimeout(resolve, delayMs));
  const parsed = new URL(url);
  const path = decodeURIComponent(parsed.pathname);
  const params = parsed.searchParams;

  if (path === '/map/cells') return ok(await cellsOf(params));
  if (path === '/map/points') return ok(await pointsOf(params));
  if (path === '/catalog') return saved('catalog', 200, path);
  if (path === '/graph') {
    const id = params.get('id') ?? '';
    const name = graphs[id];
    return name ? saved(name, id === 'evt:gtd:197000000001' ? 404 : 200, path) : missing(url);
  }
  if (path.startsWith('/records/')) {
    const name = records[path.slice('/records/'.length)];
    return name ? saved(name, 200, path) : missing(url);
  }
  if (path === '/logical') {
    const name = logical[params.get('node') ?? ''];
    return name ? saved(name, 200, path) : missing(url);
  }
  if (path === '/search') {
    const asked = Object.fromEntries([...params].filter(([name]) => name !== 'limit'));
    const found = searches.find(([request]) => sameRequest(request, asked));
    if (!found) return missing(url);
    const list = (await load(found[1])) as WireList;
    const limit = Number(params.get('limit') ?? 50);
    return ok({ total: list.total, items: list.items.slice(0, limit) });
  }
  return missing(url);
}

async function load(name: string): Promise<unknown> {
  const read = files[`../../../fixtures/analyze/${name}.json`];
  if (!read) throw new Error(`fixtures/analyze/${name}.json is missing.`);
  return read();
}

async function saved(name: string, status: number, path: string): Promise<FixtureAnswer> {
  const body = await load(name);
  return status === 200 ? ok(body) : { status, text: JSON.stringify(body ?? { title: path }) };
}

function ok(body: unknown): FixtureAnswer {
  return { status: 200, text: JSON.stringify(body) };
}

function missing(url: string): FixtureAnswer {
  const parsed = new URL(url);
  return {
    status: 501,
    text: JSON.stringify({
      title: 'Not in the fixtures',
      detail: `development mode: no saved answer for ${decodeURIComponent(parsed.pathname + parsed.search)} (fixtures/analyze/README.md lists them).`,
    }),
  };
}

function sameRequest(a: Record<string, string>, b: Record<string, string>): boolean {
  const keys = Object.keys(a);
  return keys.length === Object.keys(b).length && keys.every((key) => a[key] === b[key]);
}

/** The world's events that match the map's question (its filters and box). */
async function matching(params: URLSearchParams): Promise<WireDoc[]> {
  const world = (await load('map-points-world')) as WireList;
  // The conclusions were written by hand (the README says so): the map answers them the same way.
  const conclusions = (await load('map-points-conclusions')) as WireList;
  const from = params.get('from') ? Date.parse(params.get('from')!) : null;
  const to = params.get('to') ? Date.parse(params.get('to')!) : null;
  const sources = params.getAll('source');
  const types = params.getAll('type');
  const kind = params.get('kind') ?? 'event';
  const box = params.get('bbox')?.split(',').map(Number);
  return [...world.items, ...conclusions.items].filter((doc) => {
    const start = doc.time_start ? Date.parse(doc.time_start) : null;
    if (doc.kind !== kind) return false;
    if (from !== null && (start === null || start < from)) return false;
    if (to !== null && (start === null || start >= to)) return false;
    if (sources.length > 0 && !sources.includes(doc.source)) return false;
    if (types.length > 0 && !types.includes(doc.type)) return false;
    return !box || (doc.location !== undefined && inBox(doc.location, box));
  });
}

async function cellsOf(params: URLSearchParams) {
  const field = `h3_r${params.get('resolution') ?? '2'}` as 'h3_r2';
  const cells = new Map<string, Set<string>>();
  for (const doc of await matching(params)) {
    const cell = doc[field];
    if (!cell) continue;
    const ids = cells.get(cell) ?? new Set<string>();
    ids.add(doc.id);
    cells.set(cell, ids);
  }
  const buckets = [...cells]
    .map(([key, ids]) => ({ key, doc_count: ids.size, records: { value: ids.size } }))
    .sort((a, b) => b.records.value - a.records.value);
  return {
    records: buckets.reduce((sum, bucket) => sum + bucket.records.value, 0),
    sum_other_doc_count: 0,
    buckets,
  };
}

async function pointsOf(params: URLSearchParams): Promise<WireList> {
  const docs = (await matching(params)).filter((doc) => doc.location);
  return { total: docs.length, items: docs.slice(0, Number(params.get('limit') ?? 5000)) };
}

/** A point in a box [west, south, east, north]; a west edge east of the east edge crosses the 180th meridian. */
function inBox(
  [lon, lat]: [number, number],
  [west = -180, south = -90, east = 180, north = 90]: number[],
) {
  if (lat < south || lat > north) return false;
  return west <= east ? lon >= west && lon <= east : lon >= west || lon <= east;
}
