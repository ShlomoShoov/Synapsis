/**
 * Events placed only roughly. The indexer gives an event a cell at a resolution only when its place is more precise
 * than the cell's edge (EMERGENCY.md §11), so a UCDP event placed by its province has a cell at resolution 3 but none
 * at 4: zooming in made it vanish. The map therefore also counts the coarser resolutions in view, and what a coarse
 * cell holds beyond its finer cells (events with no finer place) stays as a light of its own at that cell's centre.
 */
import { cellToParent, getResolution } from 'h3-js';
import type { Cell, MapLayer } from '../api/model';

/** The most cells one answer holds (GET /map/cells); a full answer may be cut, so its leftovers are not trusted. */
const mostCells = 10000;

/** About how far a cell of each resolution places an event, in km (its edge). */
const edgeKm: Record<number, number> = { 2: 158, 3: 60, 4: 23, 5: 8.5 };

/** About how far a cell places its events, in km. */
export function edgeKmOf(cell: string): number | undefined {
  return edgeKm[getResolution(cell)];
}

/**
 * The cells of the zoom's resolution, plus one rough cell per coarser cell that holds more events than its finer
 * cells in view. `coarser` goes from the coarsest resolution up; `events` becomes the coarsest count (every event with
 * a cell in view).
 */
export function withRough(cells: MapLayer, coarser: MapLayer[]): MapLayer {
  if (coarser.length === 0) return cells;
  const levels = [...coarser, cells];
  const rough: Cell[] = [];
  for (let level = 0; level < levels.length - 1; level++) {
    const parents = levels[level]!;
    const children = levels[level + 1]!;
    if (children.cells.length >= mostCells || parents.resolution === undefined) continue;
    const placed = new Map<string, number>();
    for (const child of children.cells) {
      const parent = cellToParent(child.id, parents.resolution);
      placed.set(parent, (placed.get(parent) ?? 0) + child.count);
    }
    for (const parent of parents.cells) {
      const left = parent.count - (placed.get(parent.id) ?? 0);
      // Counts above 3,000 per cell are estimates (about 1 %): a small difference there is noise.
      if (left > (parent.count > 3000 ? parent.count * 0.02 : 0)) {
        rough.push({ ...parent, count: left, rough: true });
      }
    }
  }
  return {
    ...cells,
    events: Math.max(cells.events, levels[0]!.events),
    total: cells.total + rough.length,
    cells: [...cells.cells, ...rough],
  };
}
