/**
 * What the map lights up. A target is a package or a definition, under the pointer or selected; lighting it shows its
 * connections and dims everything else, like a spotlight:
 * - a definition lights itself, what it uses and what uses it, and the threads between them (even those inside one
 *   package, and uses of roles, which are not drawn otherwise);
 * - a package lights its definitions' connections across packages, and the packages at the other end.
 * Chosen domains light their packages.
 */
import type { DomainId } from '../domains';
import { linksOf, type Reference, type SchemaGraph } from './graph';

/** A package or a definition. */
export type Target = { type: 'package'; name: string } | { type: 'definition'; id: string };

/** How a row is lit. */
export type RowLight = 'focus' | 'linked';

/** What is lit. */
export interface Highlight {
  /** The cards left bright; null when nothing is dimmed. */
  cards: ReadonlySet<string> | null;
  /** The rows lit, by definition id. */
  rows: ReadonlyMap<string, RowLight>;
  /** The references whose threads are drawn bright. */
  references: ReadonlySet<Reference>;
}

const none: Highlight = { cards: null, rows: new Map(), references: new Set() };

/** Whether two targets are the same. */
export function sameTarget(a: Target | null, b: Target | null): boolean {
  if (!a || !b) {
    return a === b;
  }
  return a.type === 'package'
    ? b.type === 'package' && a.name === b.name
    : b.type === 'definition' && a.id === b.id;
}

/** What a target (and a choice of domains) lights up. */
export function highlightOf(
  graph: SchemaGraph,
  target: Target | null,
  domains: ReadonlySet<DomainId> = new Set(),
): Highlight {
  const byDomain =
    domains.size > 0
      ? new Set(graph.packages.filter((pkg) => domains.has(pkg.domain)).map((pkg) => pkg.name))
      : null;
  if (!target) {
    return byDomain ? { ...none, cards: byDomain } : none;
  }
  const rows = new Map<string, RowLight>();
  const references = new Set<Reference>();
  const cards = new Set<string>();
  const packageOf = (id: string) => graph.definitions.get(id)?.packageName;

  if (target.type === 'definition') {
    const definition = graph.definitions.get(target.id);
    if (!definition) {
      return none;
    }
    const links = linksOf(graph, target.id);
    for (const id of links.ids) {
      rows.set(id, id === target.id ? 'focus' : 'linked');
      const name = packageOf(id);
      if (name) {
        cards.add(name);
      }
    }
    for (const reference of links.references) {
      references.add(reference);
    }
  } else {
    const pkg = graph.packageByName.get(target.name);
    if (!pkg) {
      return none;
    }
    cards.add(pkg.name);
    for (const definition of pkg.definitions) {
      for (const reference of [
        ...(graph.uses.get(definition.id) ?? []),
        ...(graph.usedBy.get(definition.id) ?? []),
      ]) {
        if (!reference.crossPackage) {
          continue;
        }
        references.add(reference);
        const other = reference.from === definition.id ? reference.to : reference.from;
        rows.set(other, 'linked');
        rows.set(definition.id, 'linked');
        const name = packageOf(other);
        if (name) {
          cards.add(name);
        }
      }
    }
  }
  if (byDomain) {
    for (const name of byDomain) {
      cards.add(name);
    }
  }
  return { cards, rows, references };
}
