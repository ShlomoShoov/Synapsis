/**
 * Freshness: is each feeder sending on its usual rhythm? A feeder runs on a timer (every hour, six hours, a day, a
 * week); each run sends one or more batches within minutes. The hours a source sent in group into runs (neighbouring
 * hours are one run), the median gap between runs is its cadence, and the time since its last batch, measured in
 * cadences, says whether it is on schedule, late, or silent.
 */
import type { SourceHistory } from '../data/model';
import type { Status } from '../outcomes';

const HOUR = 3_600_000;

/** A source's freshness. */
export interface Freshness {
  /** The status colour (undefined when there is too little history to judge). */
  status: Status | undefined;
  /** The word for it: "On schedule", "Late", "Silent", "Ran once", "No data". */
  label: string;
  /** The typical time between runs, when known. */
  cadenceMs: number | null;
  /** When it last sent, and when the next run is due. */
  last: number | null;
  due: number | null;
}

/** The start of each run: consecutive active hours are one run. */
export function runStarts(activeHours: readonly number[]): number[] {
  const starts: number[] = [];
  let previous = -Infinity;
  for (const hour of activeHours) {
    if (hour - previous > HOUR) {
      starts.push(hour);
    }
    previous = hour;
  }
  return starts;
}

/** The median gap between runs, or null with fewer than three runs. */
export function typicalCadence(activeHours: readonly number[]): number | null {
  const starts = runStarts(activeHours);
  if (starts.length < 3) {
    return null;
  }
  const gaps = starts
    .slice(1)
    .map((start, i) => start - (starts[i] as number))
    .sort((a, b) => a - b);
  return gaps[Math.floor(gaps.length / 2)] ?? null;
}

/** How fresh a source is at `now`, from its history (undefined: it never sent anything in the lookback). */
export function freshnessOf(history: SourceHistory | undefined, now: number): Freshness {
  if (!history || history.entries === 0) {
    return { status: undefined, label: 'No data', cadenceMs: null, last: null, due: null };
  }
  const cadence = typicalCadence(history.activeHours);
  if (cadence === null) {
    const runs = runStarts(history.activeHours).length;
    return {
      status: undefined,
      label: runs <= 1 ? 'Ran once' : 'Too few runs',
      cadenceMs: null,
      last: history.last,
      due: null,
    };
  }
  const age = now - history.last;
  // An hour of grace on top: runs start on the hour and a slow run can spill over it.
  const status: Status =
    age <= cadence * 1.5 + HOUR ? 'good' : age <= cadence * 3 + HOUR ? 'warning' : 'critical';
  return {
    status,
    label: status === 'good' ? 'On schedule' : status === 'warning' ? 'Late' : 'Silent',
    cadenceMs: cadence,
    last: history.last,
    due: history.last + cadence,
  };
}
