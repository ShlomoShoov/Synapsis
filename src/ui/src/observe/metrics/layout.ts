/**
 * The panels of the dashboards and how the viewer arranged them: which are shown, in which order, at which width.
 * The arrangement is remembered in this browser. A panel added to the code later appears at the end; one removed
 * from the code is forgotten.
 */
import { useCallback, useState } from 'react';
import { readStored, writeStored } from '../lib/storage';

/** The id of a panel. */
export type PanelId =
  | 'figures'
  | 'volume'
  | 'throughput'
  | 'latency'
  | 'sources'
  | 'rejections'
  | 'heatmap'
  | 'batches';

/** A panel's width: the whole row, or half of it. */
export type PanelSize = 'full' | 'half';

/** One panel. */
export interface PanelInfo {
  id: PanelId;
  title: string;
  /** What it answers, in one line (the customize sheet shows it). */
  description: string;
  size: PanelSize;
  /** False when the panel needs the whole row to be readable. */
  canBeHalf: boolean;
}

/** Every panel, in its default order. */
export const panels: readonly PanelInfo[] = [
  {
    id: 'figures',
    title: 'Key figures',
    description: 'Records, writes, refusals, requests, throughput and duration, with their change.',
    size: 'full',
    canBeHalf: false,
  },
  {
    id: 'volume',
    title: 'Records over time',
    description: 'What arrived in each interval, by outcome or by domain.',
    size: 'full',
    canBeHalf: true,
  },
  {
    id: 'throughput',
    title: 'Throughput',
    description: 'Records or requests per second.',
    size: 'half',
    canBeHalf: true,
  },
  {
    id: 'latency',
    title: 'Batch duration',
    description: 'How long the API took per batch: the median and the slowest 5%.',
    size: 'half',
    canBeHalf: true,
  },
  {
    id: 'sources',
    title: 'Sources',
    description: 'Each source: what it sent, what was refused, and whether it runs on time.',
    size: 'full',
    canBeHalf: false,
  },
  {
    id: 'rejections',
    title: 'Rejections',
    description: 'Refused records by error code and by source, and the latest examples.',
    size: 'full',
    canBeHalf: false,
  },
  {
    id: 'heatmap',
    title: 'Activity by source',
    description: 'Each source over time: its rhythm, and its gaps.',
    size: 'full',
    canBeHalf: false,
  },
  {
    id: 'batches',
    title: 'Batch log',
    description: 'Every batch, newest first, with its outcome; open one for its refused records.',
    size: 'full',
    canBeHalf: false,
  },
];

/** The viewer's arrangement. */
export interface Layout {
  order: PanelId[];
  hidden: PanelId[];
  sizes: Partial<Record<PanelId, PanelSize>>;
}

const storageKey = 'metrics-layout-v1';

/** The arrangement before anyone changes it. */
export function defaultLayout(): Layout {
  return { order: panels.map((panel) => panel.id), hidden: [], sizes: {} };
}

/** An arrangement made valid against the panels that exist now. */
export function normalizeLayout(layout: Partial<Layout> | null | undefined): Layout {
  const known = new Set(panels.map((panel) => panel.id));
  const order = (layout?.order ?? []).filter((id) => known.has(id));
  for (const panel of panels) {
    if (!order.includes(panel.id)) {
      order.push(panel.id);
    }
  }
  return {
    order,
    hidden: (layout?.hidden ?? []).filter((id) => known.has(id)),
    sizes: Object.fromEntries(
      Object.entries(layout?.sizes ?? {}).filter(
        ([id, size]) => known.has(id as PanelId) && (size === 'full' || size === 'half'),
      ),
    ),
  };
}

/** The width a panel takes in an arrangement. */
export function sizeOf(layout: Layout, panel: PanelInfo): PanelSize {
  return panel.canBeHalf ? (layout.sizes[panel.id] ?? panel.size) : 'full';
}

/** The arrangement, remembered in this browser. */
export function useLayout(): [Layout, (layout: Layout) => void, () => void] {
  const [layout, setLayoutState] = useState<Layout>(() =>
    normalizeLayout(readStored<Partial<Layout> | null>(storageKey, null)),
  );
  const setLayout = useCallback((next: Layout) => {
    writeStored(storageKey, next);
    setLayoutState(next);
  }, []);
  const reset = useCallback(() => {
    writeStored(storageKey, undefined);
    setLayoutState(defaultLayout());
  }, []);
  return [layout, setLayout, reset];
}
