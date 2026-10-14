/**
 * The pipeline at the live flow's moment: the same clock (playback.ts) drives both. Live, the logs are read every
 * 30 seconds; replaying, they are read as they stood at the playhead, again every two seconds while it plays, and once
 * a jump settles (dragging the time bar asks only when the hand stops).
 */
import { useEffect, useState, useSyncExternalStore } from 'react';
import { isAbort } from '../data/http';
import { useObserveData } from '../data/observeContext';
import type { Playback } from '../flow/playback';
import { type Pipeline, pipelinePollMs, readPipeline } from './pipeline';
import { readSimulatedPipeline } from './simulated';

/** How often a playing replay reads the pipeline again, in real time. */
export const replayPollMs = 2_000;

/** How long a jump must hold before the pipeline is read at it. */
const settleMs = 260;

/** The pipeline at a moment, and how the last reading went. */
export interface PipelineAt {
  pipeline: Pipeline | null;
  /** The moment `pipeline` describes. */
  at: number;
  error: string | null;
  /** A reading for a newer moment is on its way. */
  reading: boolean;
}

/** The pipeline at the playhead's moment. */
export function usePipelineAt(playback: Playback): PipelineAt {
  const { mode, log } = useObserveData();
  const play = useSyncExternalStore(playback.subscribe, playback.getSnapshot);
  const [state, setState] = useState<PipelineAt>(() => ({
    pipeline: null,
    at: playback.now(),
    error: null,
    reading: true,
  }));
  const replaying = play.mode === 'replay';
  const playing = play.playing;

  useEffect(() => {
    const controller = new AbortController();
    let timer = 0;
    const read = async () => {
      const at = Math.round(playback.now());
      setState((now) => (now.reading ? now : { ...now, reading: true }));
      try {
        const pipeline =
          mode === 'simulated'
            ? await readSimulatedPipeline(log, at, controller.signal)
            : await readPipeline(at, controller.signal, replaying);
        if (controller.signal.aborted) return;
        setState({ pipeline, at, error: null, reading: false });
      } catch (reason) {
        if (controller.signal.aborted || isAbort(reason)) return;
        const error = reason instanceof Error ? reason.message : String(reason);
        setState((now) => ({ ...now, at, error, reading: false }));
      }
      if (controller.signal.aborted) return;
      const every = !replaying ? pipelinePollMs : playing ? replayPollMs : null;
      if (every !== null) timer = window.setTimeout(() => void read(), every);
    };
    timer = window.setTimeout(() => void read(), replaying ? settleMs : 0);
    return () => {
      controller.abort();
      window.clearTimeout(timer);
    };
  }, [playback, mode, log, replaying, playing, play.seekCount, play.speed]);

  return state;
}
