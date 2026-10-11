/**
 * The domains: the families the sources belong to (cyber, conflict and security, ...). Observe groups and colours every
 * source by its domain: one sector of the 3D ring, one colour in every chart.
 *
 * A source belongs to a domain by the publisher in its id (cisa in src:cisa:kev), listed below in the same groups as
 * deploy/feeders/compose.yml; a source with an unlisted publisher lands in "other".
 *
 * The colours are a validated set: in this order (the last one wrapping round to the first, as on the ring), every
 * neighbour pair stays apart for colour-blind readers (OKLab delta E >= 11 under simulated protanopia and
 * deuteranopia) and for everyone else (>= 19), and each clears 3:1 on the night surfaces. Change them only together,
 * and measure the pairs again before shipping a change.
 */

/** The id of a domain. */
export type DomainId =
  'cyber' | 'conflict' | 'society' | 'environment' | 'health' | 'finance' | 'system' | 'other';

/** One domain. */
export interface Domain {
  id: DomainId;
  /** The name shown on the 3D ring, in tables and in tooltips. */
  name: string;
  /** A one-word name, where space is short (legends). */
  short: string;
  /** The colour of its marks on the night surfaces. */
  color: string;
  /** The middle segment of source ids that place a source here (cisa in src:cisa:kev). */
  publishers: readonly string[];
}

/** Every domain, in the validated colour order (also the order of the sectors around the ring). */
export const domains: readonly Domain[] = [
  {
    id: 'cyber',
    name: 'Cyber',
    short: 'Cyber',
    color: '#5197db',
    publishers: ['cisa', 'nist', 'first', 'ransomware-live', 'tukaani'],
  },
  {
    id: 'conflict',
    name: 'Conflict & security',
    short: 'Conflict',
    color: '#c0571e',
    publishers: ['ucdp', 'gdelt', 'start', 'opensanctions', 'gfw'],
  },
  {
    id: 'society',
    name: 'Elections & social',
    short: 'Society',
    color: '#9881d9',
    publishers: ['wikipedia', 'reddit', 'hn'],
  },
  {
    id: 'environment',
    name: 'Environment',
    short: 'Environment',
    color: '#3a8f42',
    publishers: ['nasa', 'usgs'],
  },
  {
    id: 'health',
    name: 'Health',
    short: 'Health',
    color: '#d76797',
    publishers: ['delphi', 'jhu'],
  },
  {
    id: 'finance',
    name: 'Finance & markets',
    short: 'Finance',
    color: '#a17000',
    publishers: ['fdic', 'fred', 'polymarket'],
  },
  {
    id: 'system',
    name: 'Synapsis',
    short: 'Synapsis',
    color: '#d9d1c0',
    publishers: [],
  },
  {
    id: 'other',
    name: 'Other',
    short: 'Other',
    color: '#8f8778',
    publishers: [],
  },
];

/** The domains that hold sources from the outside world: the six coloured sectors. */
export const sourceDomains = domains.filter(
  (domain) => domain.id !== 'system' && domain.id !== 'other',
);

const byId = new Map(domains.map((domain) => [domain.id, domain]));

/** A domain by its id. */
export function domainById(id: DomainId): Domain {
  return byId.get(id) ?? (byId.get('other') as Domain);
}

/**
 * The domain of a source id, read from the publisher segment, src:<publisher>:<name>.
 */
export function guessDomainOfSource(sourceId: string): DomainId {
  if (sourceId === 'src:synapsis:engine') {
    return 'system';
  }
  if (sourceId === 'src:synapsis:kev-classes') {
    return 'cyber';
  }
  const publisher = sourceId.split(':')[1] ?? '';
  for (const domain of domains) {
    if (domain.publishers.includes(publisher)) {
      return domain.id;
    }
  }
  return 'other';
}

/** The short name of a source: its id without the "src:" prefix ("cisa:kev"). */
export function shortSourceName(sourceId: string): string {
  return sourceId.startsWith('src:') ? sourceId.slice(4) : sourceId;
}
