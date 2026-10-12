/**
 * The simulated ingestion log: what the API would have written to Elasticsearch if the demo feeders of profiles.ts
 * had been running. It is invented, seeded and deterministic: the same epoch and seed always give the same log.
 *
 * The story it tells, so every view has something to show:
 * - 30 days of history: each feeder installed (a first full load), then running on its own timer; two feeders never
 *   ran (their keys are missing) and one went quiet 12 days ago;
 * - 26 hours ago, a large USGS backfill;
 * - for the last 3 hours, busy traffic (as if the feeders had just been started), which goes on live;
 * - inside it, a slow spell (batches taking seven times longer), a storm of rejected Johns Hopkins corrections, and
 *   a few records refused before their source could be read ("unknown").
 *
 * An entry becomes visible once the API would have finished its batch (its time plus its duration), as in the real
 * log, where a batch is written when it is done.
 */
import type { LogEntry, LoggedRejection, TimeRange } from '../model';
import { profiles, type SourceProfile } from './profiles';
import { hashText, Random } from './random';

const SECOND = 1_000;
const MINUTE = 60 * SECOND;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** How far back the invented history goes. */
export const historyLength = 30 * DAY;
/** How long before the epoch the busy traffic began. */
const busyLength = 3 * HOUR;
/** Busy traffic, in batches per millisecond (about one batch every two and a half seconds). */
const busyRate = 0.4 / SECOND;
/** The simulation generates busy traffic in steps of this length. */
const chunkLength = MINUTE;
/** The largest batch the API accepts. */
const maxBatch = 5_000;

/** The settings of a simulation. */
export interface SimulationOptions {
  /** The moment the demo begins: the end of the invented history. Live traffic follows it. */
  epoch: number;
  /** Another seed gives another, equally plausible, log. */
  seed?: number;
}

/** A batch's modifications by an incident. */
interface Twist {
  /** Multiplies the share of refused records. */
  rejectFactor?: number;
  /** Multiplies the batch's duration. */
  slowFactor?: number;
  /** Overrides the share of new records (an installation is mostly new). */
  newShare?: number;
  /** Overrides the size of the batch. */
  size?: number;
}

/** The simulated log. */
export class LogSimulator {
  readonly epoch: number;
  private readonly seed: number;
  /** Scheduled runs, installations and incidents, before and after the epoch, oldest first. */
  private readonly fixed: LogEntry[];
  /** Busy traffic, generated a chunk at a time, oldest first. */
  private readonly busy: LogEntry[] = [];
  /** The index of the next busy chunk to generate (chunk 0 starts at the epoch). */
  private nextChunk: number;
  private counter = 0;

  constructor(options: SimulationOptions) {
    this.epoch = Math.floor(options.epoch / SECOND) * SECOND;
    this.seed = options.seed ?? 1897;
    const fixed: LogEntry[] = [];
    for (const profile of profiles) {
      this.scheduleRuns(profile, fixed);
    }
    this.addIncidents(fixed);
    this.fixed = fixed.sort(byTime);
    this.nextChunk = -Math.round(busyLength / chunkLength);
  }

  /** Entries received in `range` that are visible at `now` (their batch was done by then), oldest first. */
  select(range: TimeRange, now: number): LogEntry[] {
    const end = Math.min(range.to, now + 1);
    this.generateUntil(end);
    const result = [
      ...slice(this.fixed, range.from, end, now),
      ...slice(this.busy, range.from, end, now),
    ];
    return result.sort(byTime);
  }

  /** Generates busy traffic up to `time`. */
  private generateUntil(time: number) {
    while (this.epoch + this.nextChunk * chunkLength < time) {
      this.generateChunk(this.nextChunk);
      this.nextChunk++;
    }
  }

  /** One chunk of busy traffic: batches arriving at random, from feeders chosen by their busy weight. */
  private generateChunk(index: number) {
    const random = new Random(hashText(`${this.seed}:busy:${index}`));
    const start = this.epoch + index * chunkLength;
    const end = start + chunkLength;
    const sinceBusy = start - (this.epoch - busyLength);
    const ramp = Math.min(1, sinceBusy / (12 * MINUTE));
    const busyProfiles = profiles.filter((profile) => profile.busyWeight > 0);
    for (let time = start + random.exponential(busyRate * ramp); time < end;) {
      const profile = random.weighted(busyProfiles, (item) => item.busyWeight);
      const size = Math.round(random.logNormal(Math.min(profile.size[0], 1800) * 0.5 + 12, 0.9));
      const twist: Twist = { size, ...this.incidentAt(time) };
      if (random.next() < 0.035) {
        twist.rejectFactor = (twist.rejectFactor ?? 1) * 30;
      }
      this.busy.push(...this.batch(random, Math.round(time), profile, twist));
      time += random.exponential(busyRate * ramp);
    }
  }

  /** The incidents that change busy batches at a moment: a slow spell. */
  private incidentAt(time: number): Twist {
    const slowFrom = this.epoch - 2 * HOUR - 20 * MINUTE;
    if (time >= slowFrom && time < slowFrom + 16 * MINUTE) {
      return { slowFactor: 7 };
    }
    return {};
  }

  /** A feeder's installation and its runs on its own timer, over the history and a week beyond the epoch. */
  private scheduleRuns(profile: SourceProfile, into: LogEntry[]) {
    if (profile.cadence === 'never') {
      return;
    }
    const random = new Random(hashText(`${this.seed}:runs:${profile.feeder}`));
    const install = this.epoch - historyLength + random.int(10, 600) * MINUTE;
    let time = install;
    for (let i = 0; i < profile.initialBatches; i++) {
      into.push(
        ...this.batch(random, time, profile, {
          newShare: 0.98,
          size: profile.size[0] > 1000 ? maxBatch : undefined,
        }),
      );
      time += random.int(8, 40) * SECOND;
    }
    if (profile.cadence === null) {
      return;
    }
    const stop = profile.quietFor ? this.epoch - profile.quietFor : this.epoch + 7 * DAY;
    for (let run = install + profile.cadence; run < stop; run += profile.cadence) {
      let at = run + random.int(0, 90) * SECOND;
      const batches = random.int(profile.batchesPerRun[0], profile.batchesPerRun[1]);
      for (let i = 0; i < batches; i++) {
        into.push(...this.batch(random, at, profile, {}));
        at += random.int(4, 30) * SECOND;
      }
    }
  }

  /** The incidents of the history: the USGS backfill, the corrections storm, records without a source. */
  private addIncidents(into: LogEntry[]) {
    const random = new Random(hashText(`${this.seed}:incidents`));
    const usgs = profileOf('usgs');
    let time = this.epoch - 26 * HOUR;
    for (let i = 0; i < 36; i++) {
      into.push(...this.batch(random, time, usgs, { size: maxBatch, newShare: 0.97 }));
      time += random.int(14, 26) * SECOND;
    }
    const jhu = profileOf('jhu-covid');
    time = this.epoch - 100 * MINUTE;
    while (time < this.epoch - 88 * MINUTE) {
      into.push(
        ...this.batch(random, time, jhu, { size: random.int(1800, 5000), rejectFactor: 20 }),
      );
      time += random.int(12, 28) * SECOND;
    }
    time = this.epoch - 50 * MINUTE;
    for (let i = 0; i < 4; i++) {
      into.push(this.unknownSourceEntry(random, time));
      time += random.int(40, 200) * SECOND;
    }
  }

  /** One batch of a feeder: one entry per source it names. */
  private batch(random: Random, time: number, profile: SourceProfile, twist: Twist): LogEntry[] {
    const size = Math.max(
      1,
      Math.min(
        maxBatch,
        Math.round(twist.size ?? random.logNormal(profile.size[0], profile.size[1])),
      ),
    );
    const batchId = `${profile.feeder}-${hex(random, 24)}`;
    const everySource = profile.feeder === 'delphi';
    const sources = everySource ? profile.sources : [pickSource(random, profile)];
    const shares = sources.map(() => 0.6 + random.next());
    const shareTotal = shares.reduce((sum, value) => sum + value, 0);
    const duration = Math.round(
      (28 + 0.12 * size) * random.logNormal(1, 0.3) * (twist.slowFactor ?? 1),
    );
    return sources.map((source, i) => {
      const received = Math.max(1, Math.round((size * (shares[i] ?? 1)) / shareTotal));
      return this.entry(random, { time, batchId, profile, source, received, duration, twist });
    });
  }

  /** One log entry, its records divided between the outcomes. */
  private entry(
    random: Random,
    batch: {
      time: number;
      batchId: string;
      profile: SourceProfile;
      source: string;
      received: number;
      duration: number;
      twist: Twist;
    },
  ): LogEntry {
    const { profile, received, twist } = batch;
    const rejectRate = Math.min(0.6, profile.rejectRate * (twist.rejectFactor ?? 1));
    const rejected = profile.codes.length > 0 ? random.binomial(received, rejectRate) : 0;
    const duplicates = random.binomial(received - rejected, 0.003);
    const accepted = received - rejected - duplicates;
    const newShare = twist.newShare ?? profile.newShare;
    const fresh = random.binomial(accepted, newShare);
    const changedShare = newShare < 1 ? profile.changedShare / (1 - newShare) : 0;
    const changed = random.binomial(accepted - fresh, Math.min(1, changedShare));
    const rejectionCounts = splitByCode(random, rejected, profile);
    return {
      id: `sim-${++this.counter}`,
      time: batch.time,
      batchId: batch.batchId,
      producer: `feeder:${profile.feeder}`,
      source: batch.source,
      received,
      new: fresh,
      changed,
      unchanged: accepted - fresh - changed,
      duplicates,
      rejected,
      rejectionCounts,
      rejections: rejectionDetails(random, received, rejectionCounts, profile),
      durationMs: batch.duration,
    };
  }

  /** A batch whose records could not even be read for their source: all refused, logged under "unknown". */
  private unknownSourceEntry(random: Random, time: number): LogEntry {
    const received = random.int(2, 14);
    const kind = random.int(0, received);
    const rejectionCounts = [
      { code: 'E-MISSING', count: received - kind },
      { code: 'E-JSON', count: kind },
    ].filter((item) => item.count > 0);
    return {
      id: `sim-${++this.counter}`,
      time,
      batchId: `gdelt-${hex(random, 24)}`,
      producer: 'feeder:gdelt',
      source: 'unknown',
      received,
      new: 0,
      changed: 0,
      unchanged: 0,
      duplicates: 0,
      rejected: received,
      rejectionCounts,
      rejections: Array.from({ length: received }, (_, index) => ({
        index,
        id: null,
        code: index < kind ? 'E-JSON' : 'E-MISSING',
        reason:
          index < kind
            ? "kind must be entity, event, relation or conclusion, not ''"
            : 'source is missing',
      })),
      durationMs: random.int(20, 60),
    };
  }
}

function byTime(a: LogEntry, b: LogEntry): number {
  return a.time - b.time || a.batchId.localeCompare(b.batchId) || a.source.localeCompare(b.source);
}

/** The entries of a sorted list received in [from, to) and done by `now`. */
function slice(entries: readonly LogEntry[], from: number, to: number, now: number): LogEntry[] {
  let low = 0;
  let high = entries.length;
  while (low < high) {
    const middle = (low + high) >> 1;
    if ((entries[middle] as LogEntry).time < from) {
      low = middle + 1;
    } else {
      high = middle;
    }
  }
  const result: LogEntry[] = [];
  for (let i = low; i < entries.length; i++) {
    const entry = entries[i] as LogEntry;
    if (entry.time >= to) {
      break;
    }
    if (entry.time + entry.durationMs <= now) {
      result.push(entry);
    }
  }
  return result;
}

function profileOf(feeder: string): SourceProfile {
  return profiles.find((profile) => profile.feeder === feeder) as SourceProfile;
}

/** One of a feeder's sources: the first most of the time (UCDP's annual release, FDIC's financials). */
function pickSource(random: Random, profile: SourceProfile): string {
  if (profile.sources.length === 1 || random.next() < 0.8) {
    return profile.sources[0] as string;
  }
  return random.pick(profile.sources.slice(1));
}

/** Random lower-case hex, as the feeders' batch ids end. */
function hex(random: Random, length: number): string {
  let text = '';
  for (let i = 0; i < length; i++) {
    text += Math.floor(random.next() * 16).toString(16);
  }
  return text;
}

/** The refused records of an entry divided between its feeder's codes. */
function splitByCode(
  random: Random,
  rejected: number,
  profile: SourceProfile,
): { code: string; count: number }[] {
  if (rejected === 0) {
    return [];
  }
  const counts = new Map<string, number>();
  const totalWeight = profile.codes.reduce((sum, code) => sum + code.weight, 0);
  let left = rejected;
  profile.codes.forEach((code, i) => {
    const count =
      i === profile.codes.length - 1 ? left : random.binomial(left, code.weight / totalWeight);
    if (count > 0) {
      counts.set(code.code, (counts.get(code.code) ?? 0) + count);
    }
    left -= count;
  });
  return [...counts].map(([code, count]) => ({ code, count })).sort((a, b) => b.count - a.count);
}

/** The first 50 refused records in detail, at random places in the batch. */
function rejectionDetails(
  random: Random,
  received: number,
  counts: { code: string; count: number }[],
  profile: SourceProfile,
): LoggedRejection[] {
  const codes = counts
    .flatMap((item) => Array.from({ length: item.count }, () => item.code))
    .slice(0, 50);
  const indexes = new Set<number>();
  while (indexes.size < codes.length) {
    indexes.add(random.int(0, received - 1));
  }
  const sorted = [...indexes].sort((a, b) => a - b);
  return codes.map((code, i) => ({
    index: sorted[i] as number,
    id: recordId(random, profile.namespace),
    code,
    reason: random.pick(profile.codes.find((item) => item.code === code)?.reasons ?? ['']),
  }));
}

/** A plausible record id in a namespace. */
function recordId(random: Random, namespace: string): string {
  switch (namespace) {
    case 'nvd':
    case 'kev':
    case 'epss':
      return `evt:${namespace}:CVE-${random.int(2014, 2026)}-${random.int(1000, 59999)}`;
    case 'usgs':
      return `evt:usgs:us${random.int(6000, 7000)}${hex(random, 4)}`;
    case 'jhu-covid':
      return `evt:jhu-covid:${random.pick(['US', 'CA', 'BR', 'IN', 'GB', 'FR'])}:2021-${String(random.int(1, 12)).padStart(2, '0')}-${String(random.int(1, 28)).padStart(2, '0')}`;
    case 'gdelt':
      return `evt:gdelt:${random.int(900000000, 999999999)}`;
    default:
      return `evt:${namespace}:${hex(random, 10)}`;
  }
}
