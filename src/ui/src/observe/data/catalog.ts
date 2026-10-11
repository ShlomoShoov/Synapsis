/**
 * The catalog: every declared source with what Observe needs to show it (its title, its package, its domain),
 * read from the schema packages. Sources the log names but no package declares (or "unknown") still get an entry,
 * placed by guessDomainOfSource.
 */
import {
  domainOfPackage,
  domains,
  guessDomainOfSource,
  shortSourceName,
  type DomainId,
} from '../domains';
import type { SchemaDocument, SourceDefinition } from './contracts';

/** One source, as Observe shows it. */
export interface SourceInfo {
  id: string;
  /** The id without "src:", as labels show it ("cisa:kev"). */
  short: string;
  /** The declared title, null when no package declares the source. */
  title: string | null;
  publisher: string | null;
  /** The package that declares it. */
  packageName: string | null;
  domain: DomainId;
  definition: SourceDefinition | null;
}

/** The catalog of sources and packages. */
export interface Catalog {
  /** Every declared source, by domain (in the domains' order), then by id. */
  sources: SourceInfo[];
  byId: ReadonlyMap<string, SourceInfo>;
  packages: SchemaDocument[];
  /** The domain of each package. */
  packageDomains: ReadonlyMap<string, DomainId>;
}

const domainOrder = new Map(domains.map((domain, index) => [domain.id, index]));

/** Sorts sources by their domain's place in the palette, then by id. */
export function bySourceOrder(a: SourceInfo, b: SourceInfo): number {
  return (
    (domainOrder.get(a.domain) ?? 99) - (domainOrder.get(b.domain) ?? 99) ||
    a.id.localeCompare(b.id)
  );
}

/** The catalog of a set of schema packages. */
export function buildCatalog(documents: readonly SchemaDocument[]): Catalog {
  const packageDomains = new Map<string, DomainId>();
  const sources: SourceInfo[] = [];
  for (const document of documents) {
    const content = document.package;
    const domain = domainOfPackage(
      document.name,
      (content?.event_types ?? []).map((type) => type.code),
    );
    packageDomains.set(document.name, domain);
    for (const definition of content?.sources ?? []) {
      sources.push({
        id: definition.id,
        short: shortSourceName(definition.id),
        title: definition.title ?? null,
        publisher: definition.publisher ?? null,
        packageName: document.name,
        domain: domain === 'other' ? guessDomainOfSource(definition.id) : domain,
        definition,
      });
    }
  }
  sources.sort(bySourceOrder);
  return {
    sources,
    byId: new Map(sources.map((source) => [source.id, source])),
    packages: [...documents],
    packageDomains,
  };
}

/** An empty catalog, before the packages arrive. */
export const emptyCatalog: Catalog = buildCatalog([]);

/** What Observe knows about a source id: from the catalog, or guessed from the id. */
export function sourceInfo(catalog: Catalog, id: string): SourceInfo {
  return (
    catalog.byId.get(id) ?? {
      id,
      short: shortSourceName(id),
      title: id === 'unknown' ? 'Records without a readable source' : null,
      publisher: null,
      packageName: null,
      domain: guessDomainOfSource(id),
      definition: null,
    }
  );
}
