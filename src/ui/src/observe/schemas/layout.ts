/**
 * Where everything stands on the schema map: one card per package, the rows inside it, the groups of the domains,
 * and the threads between rows.
 *
 * The hub (core, the vocabulary the others build on) stands in the middle, as the API core does in the live flow.
 * The other packages stand in columns on its two sides:
 * - the column next to the hub holds the packages that use it, so their threads reach it without crossing a card;
 *   they are ordered by where their threads land on the hub (the barycentre rule), which keeps threads from crossing;
 * - the outer columns hold the packages that use no other, each domain's together, filled from the inside out.
 * Domains are dealt to the side that is shorter so far, in the domains' order, so the map stays balanced; every
 * column is centred on the same middle line.
 *
 * Every size is fixed (a row is 24 pixels), so the layout is computed from the content alone, never measured: the
 * same on every screen, and testable. SchemaMap.tsx draws it; PackageCard.tsx keeps to the same sizes.
 */
import type { DomainId } from '../domains';
import { domains } from '../domains';
import type { Definition, DefinitionKind, PackageNode, Reference, SchemaGraph } from './graph';
import { definitionKinds } from './graph';

/** The sizes of the map, in map pixels. */
export const metrics = {
  cardWidth: 272,
  hubWidth: 304,
  /** The card's head: the package's name and title. */
  header: 56,
  sectionLabel: 22,
  row: 24,
  sectionGap: 4,
  paddingBottom: 8,
  /** Between two cards of a group. */
  cardGap: 18,
  /** The domain's name above its group. */
  groupLabel: 34,
  groupPadding: 14,
  /** Between two groups of a column. */
  groupGap: 48,
  /** Between two columns of a side. */
  columnGap: 56,
  /** Between the hub and the columns next to it, where the threads run. */
  hubGap: 220,
  /** The rows a section shows before "N more" while the card is closed. */
  maxRows: 5,
} as const;

/**
 * Kinds always listed one row per definition; the others are summed up in one line until the card is opened (or
 * listed anyway when another package uses one of them).
 */
const listedKinds: ReadonlySet<DefinitionKind> = new Set(['source', 'event', 'entity']);

/** One row of a card. */
export interface RowLayout {
  /** A definition's id; "more:<package>:<kind>", "summary:<package>" or "less:<package>" for the others. */
  key: string;
  type: 'definition' | 'more' | 'summary' | 'less';
  definition: Definition | null;
  /** For "more" and "summary": the definitions the row stands for. */
  hidden: Definition[];
  /** The top of the row, from the top of the card. */
  y: number;
}

/** One section of a card: the definitions of one kind. */
export interface SectionLayout {
  kind: DefinitionKind;
  /** The top of its label, from the top of the card. */
  y: number;
  /** How many definitions of the kind the package has (shown or not). */
  count: number;
  rows: RowLayout[];
}

/** Which side of the hub a card stands on. */
export type Side = 'hub' | 'left' | 'right';

/** One package's card. */
export interface CardLayout {
  name: string;
  domain: DomainId;
  side: Side;
  column: 'hub' | 'inner' | 'outer';
  x: number;
  y: number;
  width: number;
  height: number;
  sections: SectionLayout[];
  /** The line that sums up the kinds not listed ("5 measures · 1 logical space"), or the line that closes the card. */
  footer: RowLayout | null;
  /** True when it has rows hidden while closed. */
  expandable: boolean;
  expanded: boolean;
  /** The middle of the row that shows each definition (its own row, or the row that sums it up), from the card's top. */
  anchors: ReadonlyMap<string, number>;
}

/** The packages of one domain in one column, under the domain's name. */
export interface GroupLayout {
  domain: DomainId;
  side: 'left' | 'right';
  /** The packages in it. */
  members: string[];
  x: number;
  y: number;
  width: number;
  height: number;
}

/** A rectangle. */
export interface Box {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** The whole map. */
export interface MapLayout {
  cards: CardLayout[];
  cardOf: ReadonlyMap<string, CardLayout>;
  groups: GroupLayout[];
  /** Everything, with a margin. */
  bounds: Box;
}

/** The rows and height of a card, before it is placed. */
function cardContent(
  pkg: PackageNode,
  expanded: boolean,
  usedElsewhere: (definition: Definition) => boolean,
): Pick<CardLayout, 'sections' | 'footer' | 'expandable' | 'height' | 'anchors'> {
  const byKind = new Map<DefinitionKind, Definition[]>();
  for (const definition of pkg.definitions) {
    const list = byKind.get(definition.kind) ?? [];
    list.push(definition);
    byKind.set(definition.kind, list);
  }
  const anchors = new Map<string, number>();
  const sections: SectionLayout[] = [];
  const summed: Definition[] = [];
  let expandable = false;
  let y = metrics.header;
  for (const kind of definitionKinds) {
    const all = byKind.get(kind);
    if (!all?.length) {
      continue;
    }
    // A kind is listed when it always is, when the card is open, or when another package uses one of its codes
    // (core's measures), so every thread from elsewhere ends on a row of its own.
    const listed = expanded || listedKinds.has(kind) || all.some(usedElsewhere);
    if (!listed) {
      summed.push(...all);
      expandable = true;
      continue;
    }
    const section: SectionLayout = { kind, y, count: all.length, rows: [] };
    y += metrics.sectionLabel;
    const cut = !expanded && all.length > metrics.maxRows + 1;
    const shown = cut ? all.slice(0, metrics.maxRows) : all;
    for (const definition of shown) {
      section.rows.push({ key: definition.id, type: 'definition', definition, hidden: [], y });
      anchors.set(definition.id, y + metrics.row / 2);
      y += metrics.row;
    }
    if (cut) {
      const hidden = all.slice(metrics.maxRows);
      section.rows.push({
        key: `more:${pkg.name}:${kind}`,
        type: 'more',
        definition: null,
        hidden,
        y,
      });
      for (const definition of hidden) {
        anchors.set(definition.id, y + metrics.row / 2);
      }
      y += metrics.row;
      expandable = true;
    }
    y += metrics.sectionGap;
    sections.push(section);
  }
  let footer: RowLayout | null = null;
  if (summed.length) {
    footer = { key: `summary:${pkg.name}`, type: 'summary', definition: null, hidden: summed, y };
    for (const definition of summed) {
      anchors.set(definition.id, y + metrics.row / 2);
    }
    y += metrics.row;
  } else if (expanded && pkg.definitions.length > 0) {
    footer = { key: `less:${pkg.name}`, type: 'less', definition: null, hidden: [], y };
    y += metrics.row;
  }
  return {
    sections,
    footer,
    expandable: expandable || expanded,
    height: y + metrics.paddingBottom,
    anchors,
  };
}

const domainOrder = new Map(domains.map((domain, index) => [domain.id, index]));

/**
 * The layout of a graph. `expanded` names the packages whose cards are open (every row shown).
 */
export function layoutSchemaGraph(
  graph: SchemaGraph,
  expanded: ReadonlySet<string> = new Set(),
): MapLayout {
  const usedElsewhere = (definition: Definition) =>
    (graph.usedBy.get(definition.id) ?? []).some((reference) => reference.crossPackage);
  const card = (pkg: PackageNode, side: Side, column: CardLayout['column']): CardLayout => ({
    name: pkg.name,
    domain: pkg.domain,
    side,
    column,
    x: 0,
    y: 0,
    width: column === 'hub' ? metrics.hubWidth : metrics.cardWidth,
    ...cardContent(pkg, expanded.has(pkg.name), usedElsewhere),
    expanded: expanded.has(pkg.name),
  });

  const hubPackage = graph.hub ? graph.packageByName.get(graph.hub) : undefined;
  const hub = hubPackage ? card(hubPackage, 'hub', 'hub') : null;
  const hubWidth = hub ? metrics.hubWidth : 0;
  const hubGap = hub ? metrics.hubGap : metrics.columnGap / 2;

  // Where each package's threads land on the hub (the mean height of the rows it uses), for the barycentre rule.
  const landing = new Map<string, number>();
  if (hub) {
    for (const dependency of graph.dependencies) {
      if (dependency.to !== hub.name) {
        continue;
      }
      const heights = dependency.references.map((reference) => hub.anchors.get(reference.to) ?? 0);
      landing.set(dependency.from, heights.reduce((sum, value) => sum + value, 0) / heights.length);
    }
  }

  // The packages of each domain, the hub aside: those that use the hub, and the others.
  const byDomain = new Map<DomainId, { using: CardLayout[]; other: CardLayout[] }>();
  for (const pkg of graph.packages) {
    if (pkg.name === hub?.name) {
      continue;
    }
    const entry = byDomain.get(pkg.domain) ?? { using: [], other: [] };
    if (landing.has(pkg.name)) {
      entry.using.push(card(pkg, 'left', 'inner'));
    } else {
      entry.other.push(card(pkg, 'left', 'outer'));
    }
    byDomain.set(pkg.domain, entry);
  }
  for (const entry of byDomain.values()) {
    entry.using.sort(
      (a, b) =>
        (landing.get(a.name) ?? 0) - (landing.get(b.name) ?? 0) || a.name.localeCompare(b.name),
    );
  }

  /** The height of a group of cards under its label. */
  const groupHeight = (members: readonly CardLayout[]) =>
    members.length === 0
      ? 0
      : metrics.groupLabel +
        members.reduce((sum, member) => sum + member.height, 0) +
        metrics.cardGap * (members.length - 1) +
        metrics.groupPadding;

  // A side is three columns: next to the hub, then two outer ones. Each holds groups, stacked.
  type Column = { groups: { domain: DomainId; members: CardLayout[] }[]; height: number };
  const emptySide = (): Column[] => [0, 1, 2].map(() => ({ groups: [], height: 0 }));
  const sides = { left: emptySide(), right: emptySide() };
  const add = (column: Column, domain: DomainId, members: CardLayout[]) => {
    if (members.length === 0) {
      return;
    }
    column.height += (column.groups.length ? metrics.groupGap : 0) + groupHeight(members);
    column.groups.push({ domain, members });
  };
  const sideHeight = (side: Column[]) => Math.max(...side.map((column) => column.height));
  const domainsInOrder = [...byDomain.keys()].sort(
    (a, b) => (domainOrder.get(a) ?? 99) - (domainOrder.get(b) ?? 99),
  );
  for (const domain of domainsInOrder) {
    const entry = byDomain.get(domain) as { using: CardLayout[]; other: CardLayout[] };
    // The side whose inner column is shorter takes a domain that uses the hub; the shorter side overall, one that
    // does not. With no hub, every column is open to every group.
    const innerHeight = (side: Column[]) => side[0]?.height ?? 0;
    const pick = entry.using.length ? innerHeight : sideHeight;
    const side =
      pick(sides.left) < pick(sides.right) ||
      (pick(sides.left) === pick(sides.right) && sideHeight(sides.left) <= sideHeight(sides.right))
        ? sides.left
        : sides.right;
    add(side[0] as Column, domain, entry.using);
    // The others fill the outer columns from the inside out: the first one that stays within the side's height,
    // or else the shortest, so the map grows outwards only when it must.
    const open = hub ? side.slice(1) : side;
    const limit = sideHeight(side);
    const added = metrics.groupGap + groupHeight(entry.other);
    const target =
      open.find((column) => column.height + added <= limit) ??
      open.reduce((best, column) => (column.height < best.height ? column : best));
    add(target, domain, entry.other);
  }

  // Place the columns: the first one next to the hub, the others further out; each centred on the middle line.
  const total = Math.max(sideHeight(sides.left), sideHeight(sides.right), hub?.height ?? 0);
  const cards: CardLayout[] = [];
  const groups: GroupLayout[] = [];
  for (const sideName of ['left', 'right'] as const) {
    sides[sideName].forEach((column, index) => {
      const offset = hubWidth / 2 + hubGap + index * (metrics.cardWidth + metrics.columnGap);
      const x = sideName === 'left' ? -offset - metrics.cardWidth : offset;
      let y = (total - column.height) / 2;
      for (const group of column.groups) {
        const height = groupHeight(group.members);
        groups.push({
          domain: group.domain,
          side: sideName,
          members: group.members.map((member) => member.name),
          x: x - metrics.groupPadding,
          y,
          width: metrics.cardWidth + metrics.groupPadding * 2,
          height,
        });
        let cardY = y + metrics.groupLabel;
        for (const member of group.members) {
          member.side = sideName;
          member.column = index === 0 && hub ? 'inner' : 'outer';
          member.x = x;
          member.y = cardY;
          cardY += member.height + metrics.cardGap;
          cards.push(member);
        }
        y += height + metrics.groupGap;
      }
    });
  }
  if (hub) {
    hub.x = -hubWidth / 2;
    hub.y = (total - hub.height) / 2;
    cards.unshift(hub);
  }

  const boxes: Box[] = [...cards, ...groups];
  const minX = Math.min(...boxes.map((box) => box.x), 0);
  const minY = Math.min(...boxes.map((box) => box.y), 0);
  const maxX = Math.max(...boxes.map((box) => box.x + box.width), 0);
  const maxY = Math.max(...boxes.map((box) => box.y + box.height), 0);
  const margin = 80;
  return {
    cards,
    cardOf: new Map(cards.map((item) => [item.name, item])),
    groups,
    bounds: {
      x: minX - margin,
      y: minY - margin,
      width: maxX - minX + margin * 2,
      height: maxY - minY + margin * 2,
    },
  };
}

/** A point. */
export interface Point {
  x: number;
  y: number;
}

/** One thread of the map: a reference drawn from the row that uses to the row used. */
export interface Thread {
  reference: Reference;
  from: Point;
  to: Point;
  /** An SVG path. */
  d: string;
}

/**
 * The thread of a reference: from the side of the using card that faces the used one, in a smooth S, to the facing
 * side of the used card. Within one card (or one column), it loops out on the card's outer side.
 */
export function threadOf(
  layout: MapLayout,
  graph: SchemaGraph,
  reference: Reference,
): Thread | null {
  const fromDefinition = graph.definitions.get(reference.from);
  const toDefinition = graph.definitions.get(reference.to);
  const a = fromDefinition ? layout.cardOf.get(fromDefinition.packageName) : undefined;
  const b = toDefinition ? layout.cardOf.get(toDefinition.packageName) : undefined;
  const ay = a?.anchors.get(reference.from);
  const by = b?.anchors.get(reference.to);
  if (!a || !b || ay === undefined || by === undefined) {
    return null;
  }
  const y1 = a.y + ay;
  const y2 = b.y + by;
  if (a === b || a.x === b.x) {
    // Out and back on the side away from the hub (the right side of the hub itself).
    const outward = a.side === 'left' ? -1 : 1;
    const x1 = outward < 0 ? a.x : a.x + a.width;
    const x2 = outward < 0 ? b.x : b.x + b.width;
    const reach = 26 + Math.min(40, Math.abs(y2 - y1) * 0.12);
    return {
      reference,
      from: { x: x1, y: y1 },
      to: { x: x2, y: y2 },
      d: `M${x1} ${y1}C${x1 + outward * reach} ${y1} ${x2 + outward * reach} ${y2} ${x2} ${y2}`,
    };
  }
  const rightward = a.x < b.x;
  const x1 = rightward ? a.x + a.width : a.x;
  const x2 = rightward ? b.x : b.x + b.width;
  const bend = Math.max(48, Math.abs(x2 - x1) * 0.48) * (rightward ? 1 : -1);
  return {
    reference,
    from: { x: x1, y: y1 },
    to: { x: x2, y: y2 },
    d: `M${x1} ${y1}C${x1 + bend} ${y1} ${x2 - bend} ${y2} ${x2} ${y2}`,
  };
}

/** Whether two boxes overlap. */
export function overlaps(a: Box, b: Box): boolean {
  return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;
}
