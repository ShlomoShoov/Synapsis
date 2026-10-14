/**
 * Below the API: the live flow goes on down from its core, through every stage a record passes after the API
 * (contracts/pipeline-log.md §3): the API stores it in MongoDB, the relay passes it to Kafka, and from the queue the
 * indexer writes it to Elasticsearch and the projector to Neo4j, which the engine reads to send its conclusions back.
 *
 * Drawn as pipes on the same dark ground as the flow, with records running through them at each stage's rate
 * (PipeRenderer.ts) and the stages' cards on top. It shows the moment the flow shows: live, the services' logs as
 * they are written; replaying, as they stood at the playhead (usePipelineAt.ts).
 */
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from 'react';
import { formatAge, formatCompact, formatCount, formatRate, formatSpan } from '../lib/format';
import { useInView } from '../lib/useInView';
import { useReducedMotion } from '../lib/useReducedMotion';
import { useTimeFormats } from '../lib/timeZone';
import type { Playback } from '../flow/playback';
import { speedLabel } from '../flow/playback';
import { Icon } from '../ui/Icon';
import { catchUpMs, isLive, type Pipeline, type Stage } from './pipeline';
import { type Pipe, PipeRenderer, type Queue } from './PipeRenderer';
import styles from './PipelineSection.module.css';
import {
  across,
  type Box,
  bottomOf,
  branch,
  centerX,
  centerY,
  drop,
  motionOf,
  topOf,
  trackOf,
} from './tracks';
import { usePipelineAt } from './usePipelineAt';

/** What each stage does, in plain words. */
const roles: Record<Stage['id'], string> = {
  api: 'Checks each batch the feeders send and stores every record.',
  relay: 'Passes each stored record on to the queue.',
  indexer: 'Makes each record searchable: the map, search and lists.',
  projector: 'Draws each record into the graph of connections.',
  engine: 'Reads the graph and sends back its conclusions.',
};

/** The engine counts as at work for this long after a run. */
const engineBusyMs = 20 * 60_000;

/** Below this width the stages stack in one column and the pipes are not drawn. */
const stackedBelow = 980;

export function PipelineSection({ playback }: { playback: Playback }) {
  const { pipeline, at, error, reading } = usePipelineAt(playback);
  const play = useSyncExternalStore(playback.subscribe, playback.getSnapshot);
  const reducedMotion = useReducedMotion();
  const [section, setSection] = useState<HTMLElement | null>(null);
  const inView = useInView(section, '160px');
  const canvas = useRef<HTMLCanvasElement>(null);
  const diagram = useRef<HTMLDivElement>(null);
  const [renderer, setRenderer] = useState<PipeRenderer | null>(null);

  const stage = (id: Stage['id']) => pipeline?.stages.find((item) => item.id === id);

  useEffect(() => {
    if (!canvas.current) return;
    const created = new PipeRenderer(canvas.current, reducedMotion);
    setRenderer(created);
    return () => created.dispose();
  }, [reducedMotion]);

  useEffect(() => {
    renderer?.setMotion(() => motionOf(playback.getSnapshot()));
  }, [renderer, playback]);

  useEffect(() => renderer?.setRunning(inView), [renderer, inView]);

  // The pipes follow the cards: measured after every reading and whenever the section changes size.
  const layout = useCallback(() => {
    if (!renderer || !section || !diagram.current) return;
    const width = section.clientWidth;
    const height = section.clientHeight;
    if (width < stackedBelow) {
      renderer.setPipes(width, height, [], null, null);
      return;
    }
    const origin = section.getBoundingClientRect();
    const boxes = new Map<string, Box>();
    for (const element of diagram.current.querySelectorAll<HTMLElement>('[data-node]')) {
      const rect = element.getBoundingClientRect();
      boxes.set(element.dataset.node ?? '', {
        left: rect.left - origin.left,
        top: rect.top - origin.top,
        width: rect.width,
        height: rect.height,
      });
    }
    const box = (id: string) => boxes.get(id) as Box;
    if (
      [
        'api',
        'mongo',
        'relay',
        'kafka',
        'channel',
        'indexer',
        'projector',
        'es',
        'neo4j',
        'engine',
        'back',
      ].some((id) => !boxes.has(id))
    ) {
      return;
    }
    renderer.setPipes(width, height, pipesOf(pipeline, at, box, width), queueOf(pipeline, box), {
      x: coreX(width),
      y: 0,
    });
  }, [renderer, section, pipeline, at]);

  useLayoutEffect(layout, [layout]);
  useEffect(() => {
    if (!section) return;
    const observer = new ResizeObserver(() => layout());
    observer.observe(section);
    return () => observer.disconnect();
  }, [section, layout]);

  const indexer = stage('indexer');
  const projector = stage('projector');
  const waiting =
    indexer?.lag != null || projector?.lag != null
      ? (indexer?.lag ?? 0) + (projector?.lag ?? 0)
      : null;

  return (
    <section
      ref={setSection}
      id="below-api"
      className={styles.section}
      aria-labelledby="below-api-title"
      data-offscreen={!inView || undefined}
    >
      <canvas ref={canvas} className={styles.pipes} aria-hidden="true" />
      <div ref={diagram} className={styles.diagram}>
        <header className={styles.head}>
          <p className={styles.eyebrow}>
            <span>Below the API</span>
            <span className={styles.eyebrowRule} aria-hidden="true" />
          </p>
          <h3 id="below-api-title" className={styles.title}>
            The <em>pipeline</em>
          </h3>
          <p className={styles.lede}>
            Where each record goes once the API has stored it: every stage at the moment the flow
            above shows, what waits for it and when it catches up.
          </p>
          <Moment playback={playback} play={play} at={at} reading={reading} />
          {error && (
            <p className={styles.error}>
              The services' logs cannot be read now ({error}). The flow above is not affected.
            </p>
          )}
        </header>

        <StageNode id="api" stage={stage('api')} at={at} past={play.mode === 'replay'} />
        <StoreNode id="mongo" name="MongoDB" caption="every record, as the API stored it" />
        <StageNode id="relay" stage={stage('relay')} at={at} past={play.mode === 'replay'} />

        <div className={styles.kafka} data-node="kafka" data-live={waiting !== null || undefined}>
          <div className={styles.kafkaHead}>
            <div>
              <strong>Kafka</strong>
              <span>the queue between the relay and its readers</span>
            </div>
            <div className={styles.kafkaTotal}>
              {waiting === null ? '—' : formatCompact(waiting)}
              <span>waiting</span>
            </div>
          </div>
          <div className={styles.channel} data-node="channel" />
          <div className={styles.kafkaLegs}>
            <Leg stage={indexer} reader="the indexer" />
            <span />
            <Leg stage={projector} reader="the projector" />
          </div>
        </div>

        <StageNode id="indexer" stage={indexer} at={at} past={play.mode === 'replay'} />
        <StageNode id="projector" stage={projector} at={at} past={play.mode === 'replay'} />
        <StoreNode id="es" name="Elasticsearch" caption="search, the map and the lists" />
        <StoreNode id="neo4j" name="Neo4j" caption="the graph of connections" />
        <div className={styles.engineCell}>
          <StageNode id="engine" stage={stage('engine')} at={at} past={play.mode === 'replay'} />
          <p className={styles.back} data-node="back">
            <Icon name="refresh" size={12} /> its conclusions go back through the API
          </p>
        </div>

        <aside className={styles.key} aria-label="How to read the pipes">
          <p className={styles.keyTitle}>Reading the pipes</p>
          <ul>
            <li>
              <i className={styles.keyDot} /> records on their way; more dots, more records a second
            </li>
            <li>
              <i className={styles.keyDot} data-tone="fail" /> records that failed at that stage
            </li>
            <li>
              <i className={styles.keyCrowd} /> records waiting in the queue
            </li>
            <li>
              <i className={styles.keyLine} /> a quiet stage: nothing in the last two minutes
            </li>
          </ul>
        </aside>
      </div>

      <div className={styles.runsBlock}>
        <h3 className={styles.heading}>
          The engine's last runs <span>conclusions it sent back through the API</span>
        </h3>
        {!pipeline ? (
          <p className={styles.quiet}>{error ? '—' : 'Reading the services’ logs…'}</p>
        ) : pipeline.runs.length === 0 ? (
          <p className={styles.quiet}>The engine has not run yet.</p>
        ) : (
          <ul className={styles.runs}>
            {pipeline.runs.map((run) => (
              <li key={`${run.at}-${run.source}`}>
                <span className={styles.muted}>{formatAge(run.at, at)}</span>
                <strong>{run.source.replace(/^src:/, '') || 'all sources'}</strong>
                <span>{run.message || `${formatCount(run.written)} conclusions sent`}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

/** Where the flow's core sits across the section: left of the middle on a wide screen (FlowEngine's view offset). */
function coreX(width: number) {
  return width > 1100 ? width / 2 - 150 : width / 2;
}

/** The pipes between the cards, and what flows in each at the moment shown. */
function pipesOf(
  pipeline: Pipeline | null,
  at: number,
  box: (id: string) => Box,
  width: number,
): Pipe[] {
  const stage = (id: Stage['id']) => pipeline?.stages.find((item) => item.id === id);
  const flowOf = (id: Stage['id']) => {
    const found = stage(id);
    const live = !!found && isLive(found, at);
    const failing =
      found && found.failed > 0
        ? Math.min(0.25, Math.max(0.04, found.failed / Math.max(1, found.handled + found.failed)))
        : 0;
    return { rate: live ? (found?.rate ?? null) : null, live, failing };
  };
  const engine = stage('engine');
  const engineLive =
    !!engine && engine.lastAt !== null && engine.lastAt <= at && at - engine.lastAt < engineBusyMs;
  const engineFlow = { rate: engineLive ? 2 : null, live: engineLive, failing: 0 };
  const api = flowOf('api');
  const relay = flowOf('relay');
  const indexer = flowOf('indexer');
  const projector = flowOf('projector');

  const channel = box('channel');
  const middle = { x: centerX(channel), y: centerY(channel) };
  const neo4j = box('neo4j');
  const engineBox = box('engine');
  const intoEngine = {
    x: engineBox.left + engineBox.width,
    y: Math.min(
      engineBox.top + engineBox.height - 16,
      Math.max(engineBox.top + 16, centerY(neo4j)),
    ),
  };
  const pipe = (
    id: string,
    points: { x: number; y: number }[],
    flow: typeof api,
    dashed = false,
  ) => ({
    id,
    track: trackOf(points),
    ...flow,
    dashed,
  });

  return [
    // The stem: down from the core of the flow above into the API, the API's refusals falling out of it.
    pipe('stem', drop({ x: coreX(width), y: 0 }, topOf(box('api'))), api),
    pipe('api-mongo', drop(bottomOf(box('api')), topOf(box('mongo'))), { ...api, failing: 0 }),
    pipe('mongo-relay', drop(bottomOf(box('mongo')), topOf(box('relay'))), relay),
    pipe('relay-kafka', drop(bottomOf(box('relay')), middle), { ...relay, failing: 0 }),
    pipe('kafka-indexer', branch(middle, topOf(box('indexer'))), indexer),
    pipe('kafka-projector', branch(middle, topOf(box('projector'))), projector),
    pipe('indexer-es', drop(bottomOf(box('indexer')), topOf(box('es'))), {
      ...indexer,
      failing: 0,
    }),
    pipe('projector-neo4j', drop(bottomOf(box('projector')), topOf(neo4j)), {
      ...projector,
      failing: 0,
    }),
    pipe('neo4j-engine', across({ x: neo4j.left, y: centerY(neo4j) }, intoEngine), engineFlow),
    pipe('engine-back', drop(bottomOf(engineBox), topOf(box('back'))), engineFlow, true),
  ];
}

/** The queue's channel, and what waits there for each reader. */
function queueOf(pipeline: Pipeline | null, box: (id: string) => Box): Queue {
  const lag = (id: Stage['id']) => pipeline?.stages.find((item) => item.id === id)?.lag ?? null;
  return {
    channel: box('channel'),
    left: { x: centerX(box('indexer')), lag: lag('indexer') },
    right: { x: centerX(box('projector')), lag: lag('projector') },
  };
}

/** The moment the diagram shows, and the way back to the present. */
function Moment({
  playback,
  play,
  at,
  reading,
}: {
  playback: Playback;
  play: ReturnType<Playback['getSnapshot']>;
  at: number;
  reading: boolean;
}) {
  const formats = useTimeFormats();
  if (play.mode === 'live') {
    return (
      <p className={styles.moment} data-mode="live">
        <span className={styles.momentDot} aria-hidden="true" />
        Live, as the services write
        <span className={styles.momentWhere}>read every 30 s</span>
      </p>
    );
  }
  return (
    <p className={styles.moment} data-mode="replay" data-reading={reading || undefined}>
      <Icon name="clock" size={13} />
      As it stood at {formats.dateTime(at)}
      <span className={styles.momentWhere}>
        {play.playing ? `replaying at ${speedLabel(play.speed)}` : 'paused'}
      </span>
      <button type="button" className={styles.momentLive} onClick={() => playback.goLive()}>
        Back to live
      </button>
    </p>
  );
}

/** One reader's share of the queue, and how long it needs to empty it at its rate. */
function Leg({ stage, reader }: { stage: Stage | undefined; reader: string }) {
  const catchUp = stage ? catchUpMs(stage) : null;
  return (
    <span className={styles.leg}>
      <strong>{stage?.lag == null ? '—' : formatCompact(stage.lag)}</strong> for {reader}
      {catchUp !== null && (
        <small data-bad={catchUp > 3_600_000 || undefined}> · about {formatSpan(catchUp)}</small>
      )}
      {stage?.lag === 0 && <small> · caught up</small>}
    </span>
  );
}

/** A store a stage writes to. */
function StoreNode({ id, name, caption }: { id: string; name: string; caption: string }) {
  return (
    <div className={styles.store} data-node={id} style={{ gridArea: id }}>
      <Icon name="database" size={14} />
      <strong>{name}</strong>
      <span>{caption}</span>
    </div>
  );
}

function StageNode({
  id,
  stage,
  at,
  past,
}: {
  id: Stage['id'];
  stage: Stage | undefined;
  at: number;
  /** The moment shown is a replayed one: its rates were then, not now. */
  past: boolean;
}) {
  const live = !!stage && isLive(stage, at);
  const catchUp = stage ? catchUpMs(stage) : null;
  const moving = live && stage.rate !== null && stage.rate > 0;
  return (
    <div
      className={styles.stage}
      data-node={id}
      data-live={live || undefined}
      data-moving={moving || undefined}
      style={{ gridArea: id }}
    >
      <div className={styles.stageHead}>
        <span className={styles.dot} aria-hidden="true" />
        <strong>{stage?.title ?? id}</strong>
        <span className={styles.muted}>→ {stage?.into ?? '…'}</span>
        {stage?.instances && (
          <span
            className={styles.copies}
            title="Copies of this service that reported in the last 30 seconds"
          >
            ×{stage.instances}
          </span>
        )}
      </div>
      <div className={styles.rate}>
        {id === 'engine'
          ? stage?.lastAt != null
            ? formatSpan(Math.max(0, at - stage.lastAt))
            : '—'
          : stage?.rate == null || !live
            ? '—'
            : formatRate(stage.rate)}
        <span>
          {id === 'engine'
            ? stage?.lastAt != null
              ? 'since its last run'
              : 'runs on demand'
            : past
              ? ' then'
              : ' now'}
        </span>
      </div>
      <p className={styles.role}>{roles[id]}</p>
      {stage && (
        <dl className={styles.facts}>
          <dt>Last 24 h</dt>
          <dd>
            {formatCompact(stage.handled)} {id === 'engine' ? 'conclusions' : 'handled'}
          </dd>
          {stage.lag !== null && (
            <>
              <dt>Waiting</dt>
              <dd data-bad={catchUp !== null && catchUp > 3_600_000 ? true : undefined}>
                {formatCompact(stage.lag)}
                {catchUp !== null ? (
                  <small> · about {formatSpan(catchUp)} to catch up</small>
                ) : (
                  stage.lag === 0 && <small> · caught up</small>
                )}
              </dd>
            </>
          )}
          <dt>{id === 'api' ? 'Refused' : 'Failed'}</dt>
          <dd data-bad={stage.failed > 0 || undefined}>{formatCount(stage.failed)}</dd>
          {stage.skipped > 0 && (
            <>
              <dt>Skipped</dt>
              <dd>{formatCompact(stage.skipped)}</dd>
            </>
          )}
        </dl>
      )}
      <p className={styles.seen}>
        {!stage || stage.lastAt === null
          ? 'No word yet'
          : `Last word ${formatAge(stage.lastAt, at)}`}
        {stage?.message && <span> · {stage.message}</span>}
      </p>
    </div>
  );
}
