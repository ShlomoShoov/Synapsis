/**
 * The connections graph as it grows: nodes, links and groups, placed in 3D. Pure.
 *
 * The first object sits at the centre. Each kind of connection (a group of /graph's links, connections.ts) gets its
 * own direction, and its nodes fan out in a cone around it, so the kinds are apart at a glance. Expanding a node
 * places its new neighbours the same way, in a cone pointing away from where it came from; nodes already placed never
 * move, they only gain links. Every node remembers where it grew from, so the view can draw it coming out.
 */
import type { WireGraph, WireGraphNode } from '../api/contract';
import type { RecordKind } from '../api/model';
import { groupsOf } from '../object/connections';
import { familyOf, kindOfNode, labelOfNode } from '../object/describe';

export type Vec3 = [number, number, number];

export interface GraphNode {
  id: string;
  kind: RecordKind;
  type: string;
  label: string;
  color: string;
  node: WireGraphNode;
  position: Vec3;
  /** Where it grew from: the node that brought it. */
  origin: Vec3;
  parent?: string;
  /** When it was added (ms), for the growing. */
  born: number;
  /** Set once it was expanded: all its links (the API's total) and how many the API sent. */
  total?: number;
  shown?: number;
}

export interface GraphEdge {
  id: string;
  /** The expanded node the link was read from, and the node at its other end. */
  owner: string;
  other: string;
  linkType: string;
  group: string;
}

/** A kind of connection of one expanded node: its title, its direction and how many it holds. */
export interface GraphGroup {
  key: string;
  owner: string;
  title: string;
  linkType: string;
  kind: RecordKind;
  /** Its links (as the API sent them), and the nodes placed in its cone. */
  count: number;
  placed: number;
  direction: Vec3;
  /** Where its title is written. */
  anchor: Vec3;
  color: string;
}

export interface GraphState {
  root: string | null;
  nodes: Map<string, GraphNode>;
  edges: Map<string, GraphEdge>;
  groups: Map<string, GraphGroup>;
}

export function emptyGraph(): GraphState {
  return { root: null, nodes: new Map(), edges: new Map(), groups: new Map() };
}

/** The distance from the first object to its neighbours; each step out is shorter. */
const reach = 90;
const shrink = 0.62;
const golden = Math.PI * (3 - Math.sqrt(5));

/** A node standing alone (the object, before or without its connections). */
export function nodeOf(node: WireGraphNode, position: Vec3, born: number): GraphNode {
  const kind = kindOfNode(node);
  const type = typeof node.type === 'string' ? node.type : '';
  return {
    id: node.id,
    kind,
    type,
    label: labelOfNode(node),
    color: familyOf(kind, type).color,
    node,
    position,
    origin: position,
    born,
  };
}

/** The graph with one more node expanded: its links added, its new neighbours placed. */
export function expand(state: GraphState, graph: WireGraph, now: number): GraphState {
  const nodes = new Map(state.nodes);
  const edges = new Map(state.edges);
  const groups = new Map(state.groups);
  const centreId = graph.center.id;
  const known = nodes.get(centreId);
  const centre: GraphNode = {
    ...(known ?? nodeOf(graph.center, [0, 0, 0], now)),
    total: graph.total,
    shown: graph.links.length,
  };
  nodes.set(centreId, centre);

  // Away from where it came from; the whole sphere for the first object.
  const parent = centre.parent ? nodes.get(centre.parent) : undefined;
  const axis = parent ? normal(sub(centre.position, parent.position)) : null;
  const depth = depthOf(nodes, centre);
  const distance = reach * shrink ** depth;

  const linkGroups = groupsOf(graph);
  linkGroups.forEach((linkGroup, index) => {
    const key = `${centreId}|${linkGroup.key}`;
    const before = groups.get(key);
    const direction =
      before?.direction ??
      (axis ? inCone(axis, index, linkGroup.key, 0.95) : onSphere(index, linkGroups.length));
    const fresh = linkGroup.neighbours.filter((neighbour) => !nodes.has(neighbour.id));
    const placed = before?.placed ?? 0;
    const count = placed + fresh.length;
    // The cone widens with the group, but never into the next kind's.
    const widest = axis ? 0.42 : Math.min(0.9, 1.9 / Math.sqrt(linkGroups.length + 1));
    const cone = Math.min(widest, 0.1 + 0.075 * Math.sqrt(count));
    fresh.forEach((neighbour, offset) => {
      const index = placed + offset;
      const position = add(
        centre.position,
        scale(
          fanned(direction, cone, index, count),
          distance * (0.82 + 0.36 * ((index * 0.618) % 1)),
        ),
      );
      nodes.set(neighbour.id, {
        ...nodeOf(neighbour.node, position, now),
        origin: centre.position,
        parent: centreId,
      });
    });
    for (const neighbour of linkGroup.neighbours) {
      const id = `${centreId}|${linkGroup.linkType}|${neighbour.id}`;
      const reverse = `${neighbour.id}|${linkGroup.linkType}|${centreId}`;
      if (!edges.has(reverse)) {
        edges.set(id, {
          id,
          owner: centreId,
          other: neighbour.id,
          linkType: linkGroup.linkType,
          group: key,
        });
      }
    }
    groups.set(key, {
      key,
      owner: centreId,
      title: linkGroup.title,
      linkType: linkGroup.linkType,
      kind: linkGroup.kind,
      count: linkGroup.neighbours.length,
      placed: count,
      direction,
      anchor: anchorOf(
        centre.position,
        linkGroup.neighbours.map((n) => nodes.get(n.id)!.position),
      ),
      color: linkGroup.neighbours[0]?.color ?? centre.color,
    });
  });
  return { root: state.root ?? centreId, nodes, edges, groups };
}

/** Where a group's title goes: beyond the middle of its nodes, seen from the node they belong to. */
function anchorOf(centre: Vec3, members: Vec3[]): Vec3 {
  const middle = scale(members.reduce(add, [0, 0, 0] as Vec3), 1 / Math.max(1, members.length));
  return add(centre, scale(sub(middle, centre), 1.3));
}

function depthOf(nodes: Map<string, GraphNode>, node: GraphNode): number {
  let depth = 0;
  let step: GraphNode | undefined = node;
  while (step?.parent && depth < 12) {
    step = nodes.get(step.parent);
    depth += 1;
  }
  return depth;
}

/** The i-th of n directions spread evenly over the sphere, tilted so the first view is not flat. */
export function onSphere(index: number, count: number): Vec3 {
  if (count === 1) return normal([1, 0.35, 0.25]);
  const y = 1 - ((index + 0.5) / count) * 2;
  const radius = Math.sqrt(1 - y * y);
  const theta = golden * index + 0.6;
  return normal([Math.cos(theta) * radius, y * 0.85, Math.sin(theta) * radius]);
}

/** A direction within `angle` of the axis, chosen by index (and the group, so it is the same every time). */
function inCone(axis: Vec3, index: number, salt: string, angle: number): Vec3 {
  const turn = index * golden + hash(salt) * Math.PI * 2;
  return tilt(axis, angle * (index === 0 ? 0.35 : 0.75), turn);
}

/** The i-th of n directions in a cone around a direction (a sunflower seen from the centre). */
function fanned(direction: Vec3, cone: number, index: number, count: number): Vec3 {
  const angle = cone * Math.sqrt((index + 0.5) / Math.max(1, count));
  return tilt(direction, angle, index * golden);
}

/** The direction turned away from itself by `angle`, towards the side `turn` (radians around it). */
function tilt(direction: Vec3, angle: number, turn: number): Vec3 {
  const helper: Vec3 = Math.abs(direction[1]) < 0.9 ? [0, 1, 0] : [1, 0, 0];
  const u = normal(cross(direction, helper));
  const v = cross(direction, u);
  const side = add(scale(u, Math.cos(turn)), scale(v, Math.sin(turn)));
  return normal(add(scale(direction, Math.cos(angle)), scale(side, Math.sin(angle))));
}

function hash(text: string): number {
  let value = 2166136261;
  for (let index = 0; index < text.length; index++) {
    value = Math.imul(value ^ text.charCodeAt(index), 16777619);
  }
  return (value >>> 0) / 4294967296;
}

function add(a: Vec3, b: Vec3): Vec3 {
  return [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
}

function sub(a: Vec3, b: Vec3): Vec3 {
  return [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
}

function scale(a: Vec3, factor: number): Vec3 {
  return [a[0] * factor, a[1] * factor, a[2] * factor];
}

function cross(a: Vec3, b: Vec3): Vec3 {
  return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
}

function normal(a: Vec3): Vec3 {
  const length = Math.hypot(a[0], a[1], a[2]) || 1;
  return scale(a, 1 / length);
}
