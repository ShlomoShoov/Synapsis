/**
 * An answer made readable: long lists cut to their first items with a note of how many more, then written as indented
 * JSON and split into tokens for colouring. Pure.
 */

/** The marker that stands for the rest of a cut list. */
const restMark = '\u2026 more';

/** A copy of the value with every list longer than `items` cut to its first `items` and a note of the rest. */
export function trimmed(value: unknown, items = 3, depth = 8): unknown {
  if (Array.isArray(value)) {
    if (depth <= 0) return [`${restMark} (${value.length} items)`];
    const head = value.slice(0, items).map((entry) => trimmed(entry, items, depth - 1));
    return value.length > items ? [...head, `${restMark}: ${value.length - items}`] : head;
  }
  if (value !== null && typeof value === 'object') {
    if (depth <= 0) return '{…}';
    return Object.fromEntries(
      Object.entries(value).map(([key, entry]) => [key, trimmed(entry, items, depth - 1)]),
    );
  }
  return value;
}

export type TokenKind = 'key' | 'string' | 'number' | 'literal' | 'punct' | 'note' | 'space';

export interface Token {
  text: string;
  kind: TokenKind;
}

/** JSON text as coloured tokens; the notes of cut lists are their own kind. */
export function tokensOf(json: string): Token[] {
  const tokens: Token[] = [];
  const pattern =
    /("(?:\\.|[^"\\])*")(\s*:)?|(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)|(true|false|null)|([{}[\],:])|(\s+)/g;
  let match: RegExpExecArray | null;
  while ((match = pattern.exec(json)) !== null) {
    const [, string, colon, number, literal, punct, space] = match;
    if (string !== undefined) {
      if (colon !== undefined) {
        tokens.push({ text: string, kind: 'key' }, { text: colon, kind: 'punct' });
      } else {
        tokens.push({ text: string, kind: string.includes(restMark) ? 'note' : 'string' });
      }
    } else if (number !== undefined) tokens.push({ text: number, kind: 'number' });
    else if (literal !== undefined) tokens.push({ text: literal, kind: 'literal' });
    else if (punct !== undefined) tokens.push({ text: punct, kind: 'punct' });
    else if (space !== undefined) tokens.push({ text: space, kind: 'space' });
  }
  return tokens;
}

/** A size in words: "812 B", "12.4 kB", "1.3 MB". */
export function sizeOf(bytes: number | undefined): string {
  if (bytes === undefined) return '';
  if (bytes < 1000) return `${bytes} B`;
  if (bytes < 1_000_000) return `${(bytes / 1000).toFixed(1)} kB`;
  return `${(bytes / 1_000_000).toFixed(1)} MB`;
}
