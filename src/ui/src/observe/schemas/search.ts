/**
 * Search on the schema map: packages and definitions by code, name or title, best matches first.
 */
import type { Definition, SchemaGraph } from './graph';

/** One result: a package or a definition. */
export type SearchResult =
  | { type: 'package'; name: string; title: string; score: number }
  | { type: 'definition'; definition: Definition; score: number };

/** How well `text` matches `query` (both lower case): 0 for not at all, higher is better. */
function scoreOf(text: string, query: string, weight: number): number {
  if (!text) {
    return 0;
  }
  if (text === query) {
    return 100 * weight;
  }
  if (text.startsWith(query)) {
    return 60 * weight;
  }
  // A match at the start of a word ("vuln" in "CYB.VULN.EXPLOITED", "known" in "Known exploited").
  if (new RegExp(`[^a-z0-9]${query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`).test(text)) {
    return 40 * weight;
  }
  return text.includes(query) ? 20 * weight : 0;
}

/** The best matches of `query`, at most `limit`. */
export function searchGraph(graph: SchemaGraph, query: string, limit = 10): SearchResult[] {
  const needle = query.trim().toLowerCase();
  if (!needle) {
    return [];
  }
  const results: SearchResult[] = [];
  for (const pkg of graph.packages) {
    const score = Math.max(
      scoreOf(pkg.name.toLowerCase(), needle, 1.2),
      scoreOf(pkg.title.toLowerCase(), needle, 0.5),
    );
    if (score > 0) {
      results.push({ type: 'package', name: pkg.name, title: pkg.title, score });
    }
    for (const definition of pkg.definitions) {
      const code = definition.code.toLowerCase();
      // A source is also found by its id without "src:" ("cisa:kev").
      const short = definition.kind === 'source' ? code.replace(/^src:/, '') : code;
      const best = Math.max(
        scoreOf(code, needle, 1),
        scoreOf(short, needle, 1),
        scoreOf(definition.title.toLowerCase(), needle, 0.5),
      );
      if (best > 0) {
        results.push({ type: 'definition', definition, score: best });
      }
    }
  }
  return results
    .sort((a, b) => b.score - a.score || labelOf(a).localeCompare(labelOf(b)))
    .slice(0, limit);
}

/** The text a result is known by. */
export function labelOf(result: SearchResult): string {
  return result.type === 'package' ? result.name : result.definition.code;
}
