/**
 * The catalog: what Observe shows for each source (its short name, a title, its domain). There are no schema packages
 * any more, so every entry is read from the source id itself (guessDomainOfSource); the catalog starts empty.
 */
import { domains, guessDomainOfSource, shortSourceName, type DomainId } from '../domains';

/** One source, as Observe shows it. */
export interface SourceInfo {
  id: string;
  /** The id without "src:", as labels show it ("cisa:kev"). */
  short: string;
  /** A title, null when none is known. */
  title: string | null;
  domain: DomainId;
}

/** The catalog of sources. */
export interface Catalog {
  /** Every known source, by domain (in the domains' order), then by id. */
  sources: SourceInfo[];
  byId: ReadonlyMap<string, SourceInfo>;
}

const domainOrder = new Map(domains.map((domain, index) => [domain.id, index]));

/** Sorts sources by their domain's place in the palette, then by id. */
export function bySourceOrder(a: SourceInfo, b: SourceInfo): number {
  return (
    (domainOrder.get(a.domain) ?? 99) - (domainOrder.get(b.domain) ?? 99) ||
    a.id.localeCompare(b.id)
  );
}

/** The empty catalog. */
export const emptyCatalog: Catalog = { sources: [], byId: new Map() };

/** What Observe knows about a source id: from the catalog, or read from the id. */
export function sourceInfo(catalog: Catalog, id: string): SourceInfo {
  return (
    catalog.byId.get(id) ?? {
      id,
      short: shortSourceName(id),
      title: id === 'unknown' ? 'Records without a readable source' : null,
      domain: guessDomainOfSource(id),
    }
  );
}
