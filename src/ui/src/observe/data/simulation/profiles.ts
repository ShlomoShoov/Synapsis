/**
 * How each demo source behaves in the simulation: which feeder sends it, how often it runs on its own timer (as in
 * deploy/feeders/compose.yml), how large its batches are, and which rules refuse its records, with the codes and
 * reasons the real API gives. Invented numbers, chosen to look like a working system.
 */

/** One error code a source's refused records carry. */
export interface CodeProfile {
  code: string;
  /** How often this code is the reason, relative to the source's other codes. */
  weight: number;
  /** Where in the record the problem is reported. */
  reasons: readonly string[];
}

/** How one feeder and its sources behave. */
export interface SourceProfile {
  /** The feeder's name: producer "feeder:<name>", batch ids "<name>-<hash>". */
  feeder: string;
  /** The sources its batches name; more than one means every batch carries all of them (as delphi does). */
  sources: readonly string[];
  /** The id namespace of its records, for refused record ids ("evt:<namespace>:..."). */
  namespace: string;
  /** How often it runs on its own timer; null when it runs once, at installation; "never" when its key is missing. */
  cadence: number | null | 'never';
  /** Records in each batch of a scheduled run: median and spread. */
  size: [number, number];
  /** Batches in each scheduled run: at least and at most. */
  batchesPerRun: [number, number];
  /** Batches at installation (a first full load), for sources that run once or begin with a backfill. */
  initialBatches: number;
  /** Its share of the busy traffic of the demo's recent hours (0 for none). */
  busyWeight: number;
  /** Share of accepted records stored for the first time, and as a new version (the rest are unchanged). */
  newShare: number;
  changedShare: number;
  /** Share of records refused. */
  rejectRate: number;
  codes: readonly CodeProfile[];
  /** The source stopped sending this long before the demo began (a feeder that went quiet). */
  quietFor?: number;
}

const HOUR = 3_600_000;
const DAY = 24 * HOUR;

/** Every demo feeder. The core's own source (src:synapsis:engine) sends nothing yet, as in the real system. */
export const profiles: readonly SourceProfile[] = [
  {
    feeder: 'kev',
    sources: ['src:cisa:kev'],
    namespace: 'kev',
    cadence: DAY,
    size: [14, 0.6],
    batchesPerRun: [1, 1],
    initialBatches: 1,
    busyWeight: 1.4,
    newShare: 0.35,
    changedShare: 0.08,
    rejectRate: 0.002,
    codes: [
      { code: 'E-MISSING', weight: 3, reasons: ['participants is missing'] },
      {
        code: 'E-DUPLICATE-ID',
        weight: 1,
        reasons: ['the same id and source appear earlier in the batch with other content'],
      },
    ],
  },
  {
    feeder: 'kev-classes',
    sources: ['src:synapsis:kev-classes'],
    namespace: 'kev-classes',
    cadence: null,
    size: [720, 0],
    batchesPerRun: [1, 1],
    initialBatches: 1,
    busyWeight: 0.2,
    newShare: 0.02,
    changedShare: 0,
    rejectRate: 0.001,
    codes: [
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.place'],
      },
    ],
  },
  {
    feeder: 'nvd',
    sources: ['src:nist:nvd'],
    namespace: 'nvd',
    cadence: 6 * HOUR,
    size: [420, 0.7],
    batchesPerRun: [1, 3],
    initialBatches: 16,
    busyWeight: 5,
    newShare: 0.55,
    changedShare: 0.3,
    rejectRate: 0.004,
    codes: [
      {
        code: 'E-DUPLICATE-ID',
        weight: 4,
        reasons: ['the same id and source appear earlier in the batch with other content'],
      },
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.measures'],
      },
    ],
  },
  {
    feeder: 'epss',
    sources: ['src:first:epss'],
    namespace: 'epss',
    cadence: DAY,
    size: [5000, 0],
    batchesPerRun: [4, 6],
    initialBatches: 24,
    busyWeight: 2,
    newShare: 0.96,
    changedShare: 0,
    rejectRate: 0.0003,
    codes: [
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.measures'],
      },
    ],
  },
  {
    feeder: 'ransomware-live',
    sources: ['src:ransomware-live:victims'],
    namespace: 'ransomware-live',
    cadence: 6 * HOUR,
    size: [60, 0.6],
    batchesPerRun: [1, 1],
    initialBatches: 6,
    busyWeight: 3,
    newShare: 0.7,
    changedShare: 0.05,
    rejectRate: 0.012,
    codes: [
      {
        code: 'E-DUPLICATE-ID',
        weight: 3,
        reasons: ['the same id and source appear earlier in the batch with other content'],
      },
      { code: 'E-MISSING', weight: 1, reasons: ['name is missing'] },
    ],
  },
  {
    feeder: 'xz-git',
    sources: ['src:tukaani:xz-git'],
    namespace: 'xz-git',
    cadence: DAY,
    size: [8, 0.8],
    batchesPerRun: [1, 1],
    initialBatches: 2,
    busyWeight: 0.8,
    newShare: 0.9,
    changedShare: 0,
    rejectRate: 0,
    codes: [],
  },
  {
    feeder: 'ucdp',
    sources: ['src:ucdp:ged', 'src:ucdp:candidate'],
    namespace: 'ucdp',
    cadence: 7 * DAY,
    size: [4200, 0.25],
    batchesPerRun: [2, 4],
    initialBatches: 30,
    busyWeight: 2,
    newShare: 0.3,
    changedShare: 0.25,
    rejectRate: 0.003,
    codes: [
      {
        code: 'E-JSON',
        weight: 3,
        reasons: ['The JSON value could not be converted. Path: $.place'],
      },
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.valid_time'],
      },
    ],
  },
  {
    feeder: 'gdelt',
    sources: ['src:gdelt:events'],
    namespace: 'gdelt',
    cadence: DAY,
    size: [2600, 0.5],
    batchesPerRun: [1, 2],
    initialBatches: 20,
    busyWeight: 6,
    newShare: 0.92,
    changedShare: 0,
    rejectRate: 0.018,
    codes: [
      {
        code: 'E-JSON',
        weight: 5,
        reasons: ['The JSON value could not be converted. Path: $.place'],
      },
      { code: 'E-MISSING', weight: 1, reasons: ['place is missing'] },
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.measures'],
      },
    ],
  },
  {
    feeder: 'gtd',
    sources: ['src:start:gtd'],
    namespace: 'gtd',
    cadence: null,
    size: [5000, 0],
    batchesPerRun: [1, 1],
    initialBatches: 12,
    busyWeight: 0.6,
    newShare: 0.05,
    changedShare: 0,
    rejectRate: 0.002,
    codes: [
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.valid_time'],
      },
    ],
  },
  {
    feeder: 'opensanctions',
    sources: ['src:opensanctions:maritime'],
    namespace: 'opensanctions',
    cadence: DAY,
    size: [2100, 0.3],
    batchesPerRun: [1, 2],
    initialBatches: 2,
    busyWeight: 2,
    newShare: 0.03,
    changedShare: 0.07,
    rejectRate: 0.004,
    codes: [
      {
        code: 'E-DUPLICATE-ID',
        weight: 1,
        reasons: ['the same id and source appear earlier in the batch with other content'],
      },
    ],
  },
  {
    feeder: 'gfw',
    sources: ['src:gfw:events'],
    namespace: 'gfw',
    cadence: 7 * DAY,
    size: [900, 0.5],
    batchesPerRun: [1, 2],
    initialBatches: 4,
    busyWeight: 0,
    newShare: 0.6,
    changedShare: 0.1,
    rejectRate: 0.002,
    codes: [{ code: 'E-MISSING', weight: 1, reasons: ['participants is missing'] }],
    quietFor: 12 * DAY,
  },
  {
    feeder: 'firms',
    sources: ['src:nasa:firms'],
    namespace: 'firms',
    cadence: 'never',
    size: [3000, 0.5],
    batchesPerRun: [1, 1],
    initialBatches: 0,
    busyWeight: 0,
    newShare: 1,
    changedShare: 0,
    rejectRate: 0,
    codes: [],
  },
  {
    feeder: 'usgs',
    sources: ['src:usgs:comcat'],
    namespace: 'usgs',
    cadence: HOUR,
    size: [9, 0.6],
    batchesPerRun: [1, 1],
    initialBatches: 8,
    busyWeight: 5,
    newShare: 0.55,
    changedShare: 0.35,
    rejectRate: 0.001,
    codes: [
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.measures'],
      },
    ],
  },
  {
    feeder: 'fdic',
    sources: ['src:fdic:financials', 'src:fdic:failures'],
    namespace: 'fdic',
    cadence: 7 * DAY,
    size: [4800, 0.1],
    batchesPerRun: [3, 6],
    initialBatches: 40,
    busyWeight: 2,
    newShare: 0.2,
    changedShare: 0.05,
    rejectRate: 0.0005,
    codes: [
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.measures'],
      },
    ],
  },
  {
    feeder: 'fred',
    sources: ['src:fred:series'],
    namespace: 'fred',
    cadence: 'never',
    size: [4, 0.3],
    batchesPerRun: [1, 1],
    initialBatches: 0,
    busyWeight: 0,
    newShare: 1,
    changedShare: 0,
    rejectRate: 0,
    codes: [],
  },
  {
    feeder: 'polymarket',
    sources: ['src:polymarket:prices'],
    namespace: 'polymarket',
    cadence: DAY,
    size: [3400, 0.4],
    batchesPerRun: [1, 3],
    initialBatches: 10,
    busyWeight: 3,
    newShare: 0.85,
    changedShare: 0,
    rejectRate: 0.003,
    codes: [
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.measures'],
      },
    ],
  },
  {
    feeder: 'delphi',
    sources: [
      'src:delphi:hhs-admissions',
      'src:delphi:jhu-cases',
      'src:delphi:doctor-visits',
      'src:delphi:google-symptoms',
      'src:delphi:fb-survey',
    ],
    namespace: 'delphi',
    cadence: null,
    size: [4900, 0.05],
    batchesPerRun: [1, 1],
    initialBatches: 18,
    busyWeight: 3,
    newShare: 0.1,
    changedShare: 0.12,
    rejectRate: 0.001,
    codes: [
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.measures'],
      },
    ],
  },
  {
    feeder: 'jhu-covid',
    sources: ['src:jhu:csse-covid'],
    namespace: 'jhu-covid',
    cadence: null,
    size: [5000, 0],
    batchesPerRun: [1, 1],
    initialBatches: 24,
    busyWeight: 2,
    newShare: 0.15,
    changedShare: 0,
    rejectRate: 0.014,
    codes: [
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.measures'],
      },
    ],
  },
  {
    feeder: 'wikipedia-elections',
    sources: ['src:wikipedia:polls', 'src:wikipedia:results'],
    namespace: 'wikipedia-elections',
    cadence: 7 * DAY,
    size: [1200, 0.4],
    batchesPerRun: [1, 2],
    initialBatches: 3,
    busyWeight: 2,
    newShare: 0.08,
    changedShare: 0.04,
    rejectRate: 0.006,
    codes: [
      {
        code: 'E-JSON',
        weight: 2,
        reasons: ['The JSON value could not be converted. Path: $.place'],
      },
      {
        code: 'E-JSON',
        weight: 1,
        reasons: ['The JSON value could not be converted. Path: $.measures'],
      },
    ],
  },
  {
    feeder: 'reddit',
    sources: ['src:reddit:arcticshift'],
    namespace: 'reddit',
    cadence: DAY,
    size: [1400, 0.5],
    batchesPerRun: [1, 2],
    initialBatches: 14,
    busyWeight: 4,
    newShare: 0.6,
    changedShare: 0.05,
    rejectRate: 0.005,
    codes: [
      { code: 'E-MISSING', weight: 3, reasons: ['name is missing'] },
      {
        code: 'E-DUPLICATE-ID',
        weight: 1,
        reasons: ['the same id and source appear earlier in the batch with other content'],
      },
    ],
  },
  {
    feeder: 'hackernews',
    sources: ['src:hn:algolia'],
    namespace: 'hackernews',
    cadence: 6 * HOUR,
    size: [120, 0.6],
    batchesPerRun: [1, 1],
    initialBatches: 3,
    busyWeight: 3,
    newShare: 0.5,
    changedShare: 0.1,
    rejectRate: 0.004,
    codes: [{ code: 'E-MISSING', weight: 1, reasons: ['participants is missing'] }],
  },
];
