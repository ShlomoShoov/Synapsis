/**
 * The schema graph: every package the API holds, every definition in them, and how they refer to each other.
 *
 * A package defines codes (an event type, an entity type, a role, a measure, ...); its other definitions use codes,
 * its own or another package's. Those uses are what the map draws:
 * - an event type's roles take entity types ("subject: Vulnerability"), and use the roles themselves;
 * - an event type lists the measures its records may carry;
 * - a relation type joins entity types (from, to);
 * - a shared namespace (core) holds the keys of one entity type.
 *
 * Codes are global within their kind: one package defines a code, any package may use it (core defines the shared
 * vocabulary). A code used but defined nowhere is "unresolved"; a code defined twice keeps the definition of core, or
 * else of the package first by name, and the other is listed as a duplicate. The API refuses both, so either means
 * the registry holds something it should not, and the map says so.
 *
 * Nothing here draws: layout.ts places the packages, SchemaMap.tsx draws them.
 */
import type {
  ConclusionTypeDefinition,
  EntityTypeDefinition,
  EventTypeDefinition,
  LogicalSpaceDefinition,
  MeasureDefinition,
  RelationTypeDefinition,
  RoleDefinition,
  SchemaDocument,
  SharedNamespaceDefinition,
  SourceDefinition,
} from '../data/contracts';
import { domainOfPackage, domains, type DomainId } from '../domains';

/** The kinds of definition, in the order a package card lists them. */
export type DefinitionKind =
  | 'source'
  | 'event'
  | 'entity'
  | 'relation'
  | 'role'
  | 'measure'
  | 'space'
  | 'namespace'
  | 'conclusion'
  | 'vocabulary';

/** How each kind is named. */
export const kindInfo: Record<DefinitionKind, { one: string; many: string }> = {
  source: { one: 'Source', many: 'Sources' },
  event: { one: 'Event type', many: 'Event types' },
  entity: { one: 'Entity type', many: 'Entity types' },
  relation: { one: 'Relation type', many: 'Relation types' },
  role: { one: 'Role', many: 'Roles' },
  measure: { one: 'Measure', many: 'Measures' },
  space: { one: 'Logical space', many: 'Logical spaces' },
  namespace: { one: 'Shared namespace', many: 'Shared namespaces' },
  conclusion: { one: 'Conclusion type', many: 'Conclusion types' },
  vocabulary: { one: 'Vocabulary', many: 'Vocabularies' },
};

/** "3 event types", "1 measure". */
export function countOf(kind: DefinitionKind, count: number): string {
  const info = kindInfo[kind];
  return `${count} ${(count === 1 ? info.one : info.many).toLowerCase()}`;
}

/** Every kind, in card order. */
export const definitionKinds = Object.keys(kindInfo) as DefinitionKind[];

/** The declaration behind a definition, by kind. */
export interface Declarations {
  source: SourceDefinition;
  event: EventTypeDefinition;
  entity: EntityTypeDefinition;
  relation: RelationTypeDefinition;
  role: RoleDefinition;
  measure: MeasureDefinition;
  space: LogicalSpaceDefinition;
  namespace: SharedNamespaceDefinition;
  conclusion: ConclusionTypeDefinition;
  vocabulary: { name: string; values: string[] };
}

/** One definition of one package. */
export type Definition = {
  [K in DefinitionKind]: {
    /** Unique in the graph: "<kind>:<code>" ("entity:Vulnerability", "source:src:cisa:kev"). */
    id: string;
    kind: K;
    /** The code, name or id it is known by. */
    code: string;
    /** What it is, in words. */
    title: string;
    packageName: string;
    declaration: Declarations[K];
  };
}[DefinitionKind];

/** How a definition uses another. */
export type ReferenceKind =
  /** An event type allows a role. */
  | 'role'
  /** An event type's role takes an entity type. */
  | 'type'
  /** An event type's records may carry a measure. */
  | 'measure'
  /** A relation type starts at an entity type. */
  | 'from'
  /** A relation type ends at an entity type. */
  | 'to'
  /** A shared namespace holds the keys of an entity type. */
  | 'namespace';

/** One use of a definition by another. */
export interface Reference {
  /** The definition that uses (an event type, a relation type, a shared namespace). */
  from: string;
  /** The definition used. */
  to: string;
  kind: ReferenceKind;
  /** For a "type" reference, the role the entity type plays ("subject"). */
  role?: string;
  /** Whether the two definitions are in different packages. */
  crossPackage: boolean;
}

/** A code used but defined in no package. */
export interface Unresolved {
  from: string;
  kind: ReferenceKind;
  /** The kind of definition the code should name. */
  expected: DefinitionKind;
  code: string;
}

/** One package. */
export interface PackageNode {
  name: string;
  title: string;
  description: string | null;
  maintainer: string | null;
  version: number;
  fingerprint: string;
  domain: DomainId;
  /** The id namespaces its records and entities may use. */
  recordNamespaces: string[];
  entityNamespaces: string[];
  /** Its definitions, in card order (by kind, then as declared). */
  definitions: Definition[];
  /** False when the API listed it but gave no content. */
  readable: boolean;
}

/** What one package uses of another. */
export interface Dependency {
  from: string;
  to: string;
  references: Reference[];
}

/** The graph of the packages. */
export interface SchemaGraph {
  /** By domain (in the palette's order), then by name. */
  packages: PackageNode[];
  packageByName: ReadonlyMap<string, PackageNode>;
  definitions: ReadonlyMap<string, Definition>;
  references: Reference[];
  /** The references each definition makes, by its id. */
  uses: ReadonlyMap<string, Reference[]>;
  /** The references made to each definition, by its id. */
  usedBy: ReadonlyMap<string, Reference[]>;
  /** Package to package, for every pair with at least one reference. */
  dependencies: Dependency[];
  /**
   * The package the others build on: the one used by the most other packages (core, in Synapsis). Null when no
   * package uses another.
   */
  hub: string | null;
  unresolved: Unresolved[];
  /** Definitions whose code another package already defined (each kept apart, not linked). */
  duplicates: Definition[];
}

/** The id of a definition of `kind` with `code`. */
export function definitionId(kind: DefinitionKind, code: string): string {
  return `${kind}:${code}`;
}

const domainOrder = new Map(domains.map((domain, index) => [domain.id, index]));

/** The definitions a package declares, in card order. */
function declarationsOf(document: SchemaDocument): Omit<Definition, 'id'>[] {
  const content = document.package;
  if (!content) {
    return [];
  }
  const packageName = document.name;
  const out: Omit<Definition, 'id'>[] = [];
  for (const declaration of content.sources ?? []) {
    out.push({
      kind: 'source',
      code: declaration.id,
      title: declaration.title,
      packageName,
      declaration,
    });
  }
  for (const declaration of content.event_types ?? []) {
    out.push({
      kind: 'event',
      code: declaration.code,
      title: declaration.title,
      packageName,
      declaration,
    });
  }
  for (const declaration of content.entity_types ?? []) {
    out.push({
      kind: 'entity',
      code: declaration.code,
      title: declaration.title,
      packageName,
      declaration,
    });
  }
  for (const declaration of content.relation_types ?? []) {
    out.push({
      kind: 'relation',
      code: declaration.code,
      title: declaration.title,
      packageName,
      declaration,
    });
  }
  for (const declaration of content.roles ?? []) {
    out.push({
      kind: 'role',
      code: declaration.code,
      title: declaration.title,
      packageName,
      declaration,
    });
  }
  for (const declaration of content.measures ?? []) {
    out.push({
      kind: 'measure',
      code: declaration.name,
      title: declaration.title,
      packageName,
      declaration,
    });
  }
  for (const declaration of content.logical_spaces ?? []) {
    out.push({
      kind: 'space',
      code: declaration.space,
      title: declaration.title,
      packageName,
      declaration,
    });
  }
  for (const declaration of content.shared_entity_namespaces ?? []) {
    out.push({
      kind: 'namespace',
      code: declaration.namespace,
      title: declaration.title ?? `Keys of ${declaration.entity_type}`,
      packageName,
      declaration,
    });
  }
  for (const declaration of content.conclusion_types ?? []) {
    out.push({
      kind: 'conclusion',
      code: declaration.code,
      title: declaration.title,
      packageName,
      declaration,
    });
  }
  for (const [name, values] of Object.entries(content.vocabularies ?? {})) {
    out.push({
      kind: 'vocabulary',
      code: name,
      title: `${values.length} value${values.length === 1 ? '' : 's'}`,
      packageName,
      declaration: { name, values },
    });
  }
  return out;
}

/** The codes a definition uses, before they are resolved. */
function usesOf(
  definition: Definition,
): { kind: ReferenceKind; expected: DefinitionKind; code: string; role?: string }[] {
  switch (definition.kind) {
    case 'event': {
      const uses: { kind: ReferenceKind; expected: DefinitionKind; code: string; role?: string }[] =
        [];
      for (const [role, types] of Object.entries(definition.declaration.roles ?? {})) {
        uses.push({ kind: 'role', expected: 'role', code: role });
        for (const type of types) {
          uses.push({ kind: 'type', expected: 'entity', code: type, role });
        }
      }
      for (const measure of definition.declaration.measures ?? []) {
        uses.push({ kind: 'measure', expected: 'measure', code: measure });
      }
      return uses;
    }
    case 'relation':
      return [
        ...definition.declaration.from.map((code) => ({
          kind: 'from' as const,
          expected: 'entity' as const,
          code,
        })),
        ...definition.declaration.to.map((code) => ({
          kind: 'to' as const,
          expected: 'entity' as const,
          code,
        })),
      ];
    case 'namespace':
      return [{ kind: 'namespace', expected: 'entity', code: definition.declaration.entity_type }];
    default:
      return [];
  }
}

/** Adds `item` to the list under `key`. */
function push<K, V>(map: Map<K, V[]>, key: K, item: V) {
  const list = map.get(key);
  if (list) {
    list.push(item);
  } else {
    map.set(key, [item]);
  }
}

/**
 * The graph of a set of schema documents. `domainOf` places a package in a domain (the catalog's choice, so the map
 * and the dashboards agree); by default, as domains.ts does.
 */
export function buildSchemaGraph(
  documents: readonly SchemaDocument[],
  domainOf: (document: SchemaDocument) => DomainId = (document) =>
    domainOfPackage(
      document.name,
      (document.package?.event_types ?? []).map((type) => type.code),
    ),
): SchemaGraph {
  const definitions = new Map<string, Definition>();
  const duplicates: Definition[] = [];
  // Definitions are registered core first, then by package name, so which of two definitions of a code wins never
  // depends on the order the API lists the packages in.
  const ordered = [...documents].sort(
    (a, b) => Number(b.name === 'core') - Number(a.name === 'core') || a.name.localeCompare(b.name),
  );
  const packages: PackageNode[] = ordered.map((document) => {
    const own: Definition[] = [];
    for (const item of declarationsOf(document)) {
      const id = definitionId(item.kind, item.code);
      if (definitions.has(id)) {
        // A second definition of the same code: kept on its card under its own id, but never linked.
        const duplicate = { ...item, id: `${id}@${document.name}` } as Definition;
        duplicates.push(duplicate);
        own.push(duplicate);
        continue;
      }
      const definition = { ...item, id } as Definition;
      definitions.set(id, definition);
      own.push(definition);
    }
    const content = document.package;
    return {
      name: document.name,
      title: content?.title ?? document.name,
      description: content?.description?.trim() || null,
      maintainer: content?.maintainer ?? null,
      version: document.version,
      fingerprint: document.fingerprint,
      domain: domainOf(document),
      recordNamespaces: content?.namespaces?.records ?? [],
      entityNamespaces: content?.namespaces?.entities ?? [],
      definitions: own,
      readable: content !== null,
    };
  });
  packages.sort(
    (a, b) =>
      (domainOrder.get(a.domain) ?? 99) - (domainOrder.get(b.domain) ?? 99) ||
      a.name.localeCompare(b.name),
  );

  const references: Reference[] = [];
  const unresolved: Unresolved[] = [];
  for (const pkg of packages) {
    for (const definition of pkg.definitions) {
      if (definition.id.includes('@')) {
        continue;
      }
      const seen = new Set<string>();
      for (const use of usesOf(definition)) {
        const target = definitions.get(definitionId(use.expected, use.code));
        if (!target) {
          unresolved.push({
            from: definition.id,
            kind: use.kind,
            expected: use.expected,
            code: use.code,
          });
          continue;
        }
        // One reference per target and way (an entity type in two roles is two references, with their roles).
        const key = `${use.kind}|${target.id}|${use.role ?? ''}`;
        if (seen.has(key)) {
          continue;
        }
        seen.add(key);
        references.push({
          from: definition.id,
          to: target.id,
          kind: use.kind,
          ...(use.role ? { role: use.role } : {}),
          crossPackage: target.packageName !== definition.packageName,
        });
      }
    }
  }

  const uses = new Map<string, Reference[]>();
  const usedBy = new Map<string, Reference[]>();
  const pairs = new Map<string, Dependency>();
  for (const reference of references) {
    push(uses, reference.from, reference);
    push(usedBy, reference.to, reference);
    if (reference.crossPackage) {
      const from = (definitions.get(reference.from) as Definition).packageName;
      const to = (definitions.get(reference.to) as Definition).packageName;
      const key = `${from}>${to}`;
      const pair = pairs.get(key) ?? { from, to, references: [] };
      pair.references.push(reference);
      pairs.set(key, pair);
    }
  }
  const dependencies = [...pairs.values()].sort(
    (a, b) => a.from.localeCompare(b.from) || a.to.localeCompare(b.to),
  );

  // The hub: used by the most other packages; on a tie, by the most references, then by name.
  const users = new Map<string, Set<string>>();
  const weight = new Map<string, number>();
  for (const dependency of dependencies) {
    const set = users.get(dependency.to) ?? new Set<string>();
    set.add(dependency.from);
    users.set(dependency.to, set);
    weight.set(dependency.to, (weight.get(dependency.to) ?? 0) + dependency.references.length);
  }
  const hub =
    [...users.entries()]
      .sort(
        ([a, setA], [b, setB]) =>
          setB.size - setA.size ||
          (weight.get(b) ?? 0) - (weight.get(a) ?? 0) ||
          a.localeCompare(b),
      )
      .map(([name]) => name)[0] ?? null;

  return {
    packages,
    packageByName: new Map(packages.map((pkg) => [pkg.name, pkg])),
    definitions,
    references,
    uses,
    usedBy,
    dependencies,
    hub,
    unresolved,
    duplicates,
  };
}

/** The packages that use any definition of `name`, and those it uses. */
export function neighboursOf(
  graph: SchemaGraph,
  name: string,
): { uses: string[]; usedBy: string[] } {
  return {
    uses: graph.dependencies.filter((item) => item.from === name).map((item) => item.to),
    usedBy: graph.dependencies.filter((item) => item.to === name).map((item) => item.from),
  };
}

/**
 * The definitions linked to `id` (what it uses, what uses it) and the references between them: what a focus on one
 * row lights up.
 */
export function linksOf(
  graph: SchemaGraph,
  id: string,
): { ids: Set<string>; references: Reference[] } {
  const references = [...(graph.uses.get(id) ?? []), ...(graph.usedBy.get(id) ?? [])];
  const ids = new Set<string>([id]);
  for (const reference of references) {
    ids.add(reference.from);
    ids.add(reference.to);
  }
  return { ids, references };
}

/** A text that changes whenever any package changes: names, versions and fingerprints. */
export function signatureOf(documents: readonly SchemaDocument[]): string {
  return documents
    .map((document) => `${document.name}@${document.version}:${document.fingerprint}`)
    .join('|');
}
