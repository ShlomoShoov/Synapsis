/**
 * The stories: the proof cases of the demo, each one click away. A story is an address of Analyze (the time window,
 * the camera, the filter, the object and the dimension), so it opens like any shared link. Pure.
 */

export interface Story {
  id: string;
  title: string;
  /** One line: what the story shows. */
  line: string;
  /** From and to as days (to excluded). */
  from: string;
  to: string;
  /** lon, lat, zoom */
  view?: [number, number, number];
  sources?: string[];
  kind?: 'event' | 'conclusion' | 'entity';
  object?: string;
  dimension?: 'map' | 'graph' | 'logical';
}

export const stories: Story[] = [
  {
    id: 'tohoku',
    title: 'Tōhoku, 10–11 Mar 2011',
    line: 'The forecast cells known on 10 March, then the earthquakes of the 11th.',
    from: '2011-03-10',
    to: '2011-03-12',
    view: [142.4, 38.3, 4.3],
    sources: ['src:usgs:comcat', 'src:synapsis:engine'],
  },
  {
    id: 'conflict',
    title: 'Conflict, Apr 2023',
    line: 'UCDP events of the week of 10 April 2023 and the cells forecast for it.',
    from: '2023-04-10',
    to: '2023-04-17',
    view: [31, 13, 3.2],
    sources: ['src:ucdp:ged', 'src:synapsis:engine'],
  },
  {
    id: 'svb',
    title: 'SVB, Feb 2023',
    line: 'Silicon Valley Bank: its reports, the joint anomaly, and its failure on 10 March.',
    from: '2023-02-01',
    to: '2023-03-15',
    view: [-122, 37.4, 6],
    object: 'ent:fdic:cert-24735',
    dimension: 'graph',
  },
  {
    id: 'elections',
    title: 'Elections 2024',
    line: 'Pennsylvania: the calibrated estimate issued before election day, its polls and the result.',
    from: '2024-10-01',
    to: '2024-11-08',
    view: [-77.6, 40.9, 5],
    object: 'con:engine:calibrated_estimate:wikipedia-polls:US-PA:2024-11-05',
    dimension: 'graph',
  },
  {
    id: 'kev',
    title: 'Log4Shell, Dec 2021',
    line: 'One CVE, listed by CISA KEV on 10 December: the same vulnerability in NVD, EPSS, Hacker News and Reddit.',
    from: '2021-12-01',
    to: '2022-01-01',
    object: 'ent:cve:CVE-2021-44228',
    dimension: 'graph',
  },
  {
    id: 'score',
    title: 'Report card: UCDP 2010–2024',
    line: "How the engine's weekly conflict forecasts did over 783 weeks, against the routine.",
    from: '2010-01-04',
    to: '2025-01-01',
    view: [31, 13, 2.5],
    sources: ['src:ucdp:ged'],
    object: 'con:engine:forecast_score:ucdp-ged:cell:test',
  },
];

/** The address of Analyze that opens the story. */
export function addressOf(story: Story): string {
  const params = new URLSearchParams({ from: story.from, to: story.to });
  if (story.view) params.set('view', story.view.join(','));
  if (story.sources?.length) params.set('source', story.sources.join(','));
  if (story.kind) params.set('kind', story.kind);
  if (story.object) params.set('object', story.object);
  if (story.dimension) params.set('dimension', story.dimension);
  return `/analyze?${params.toString()}`;
}
