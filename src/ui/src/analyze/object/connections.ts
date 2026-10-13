/**
 * The links of GET /graph, divided by kind: one group per link type, direction and kind of node at the other end
 * ("Taking part": the entities in an event; "Takes part in": an entity's events; "Conclusions about it"...). Pure.
 *
 * The groups are made from the links the API returned. When it returned fewer than the node has (total above the
 * links), the groups count only those: the panel says so, and never presents them as the whole.
 */
import type { WireGraph, WireGraphLink, WireGraphNode } from '../api/contract';
import type { RecordKind } from '../api/model';
import { familyOf, kindOfNode, labelOfNode, typeWords, words } from './describe';

/** The node at the other end of a link. */
export interface Neighbour {
  id: string;
  kind: RecordKind;
  label: string;
  /** What it is: its type, its role in the link, its date. */
  detail: string;
  color: string;
  node: WireGraphNode;
}

export interface LinkGroup {
  key: string;
  title: string;
  /** The link type as stored (PARTICIPATES, RELATED...). */
  linkType: string;
  /** Away from the centre (it → them) or towards it. */
  outgoing: boolean;
  kind: RecordKind;
  neighbours: Neighbour[];
}

/** The links grouped, the largest group first; within a group, the order the API gave. */
export function groupsOf(graph: WireGraph): LinkGroup[] {
  const groups = new Map<string, LinkGroup>();
  for (const link of graph.links) {
    const outgoing = link.from === graph.center.id;
    const kind = kindOfNode(link.node);
    const relation = relationOf(link);
    const key = `${link.type}|${relation}|${outgoing ? 'out' : 'in'}|${kind}`;
    const group = groups.get(key) ?? {
      key,
      title: titleOf(link.type, relation, outgoing),
      linkType: link.type,
      outgoing,
      kind,
      neighbours: [],
    };
    group.neighbours.push(neighbourOf(link));
    groups.set(key, group);
  }
  return [...groups.values()].sort((a, b) => b.neighbours.length - a.neighbours.length);
}

/** The relation's own type, for a RELATED link (PRODUCT_OF...); empty for the others. */
function relationOf(link: WireGraphLink): string {
  const type = link.properties.type;
  return link.type === 'RELATED' && typeof type === 'string' ? type : '';
}

/** The group's title, read from the centre. */
export function titleOf(linkType: string, relation: string, outgoing: boolean): string {
  switch (linkType) {
    case 'PARTICIPATES':
      return outgoing ? 'Takes part in' : 'Taking part';
    case 'ABOUT':
      return outgoing ? 'About' : 'Conclusions about it';
    case 'PREMISE':
      return outgoing ? 'Premises' : 'Conclusions resting on it';
    case 'RELATED':
      if (!relation) return 'Related';
      return outgoing ? words(relation) : `${words(relation)} it`;
    default:
      return words(linkType);
  }
}

function neighbourOf(link: WireGraphLink): Neighbour {
  const node = link.node;
  const kind = kindOfNode(node);
  const type = typeof node.type === 'string' ? node.type : '';
  const role = typeof link.properties.role === 'string' ? words(link.properties.role) : '';
  const time = typeof node.time_start === 'string' ? node.time_start.slice(0, 10) : '';
  return {
    id: node.id,
    kind,
    label: labelOfNode(node),
    detail: [role, type ? typeWords(type) : '', time].filter(Boolean).join(' · '),
    color: familyOf(kind, type).color,
    node,
  };
}
