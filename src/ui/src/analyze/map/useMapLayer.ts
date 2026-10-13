/**
 * useMapLayer: the map's layer for the filters and the view, kept current while either changes.
 *
 * One request at a time, and always the newest question: while a request runs, newer questions wait, and when it
 * ends only the latest of them is asked. So dragging the time window redraws the map as fast as the API answers,
 * without a queue of stale requests, and the last position always wins. The previous answer stays drawn until the
 * next arrives.
 */
import { useEffect, useRef, useState } from 'react';
import { fetchMapLayer } from '../api/client';
import { isAbort } from '../api/http';
import type { MapLayer } from '../api/model';
import { type Filters, type MapView, mapQuery } from '../api/query';

export interface MapLayerState {
  layer: MapLayer | undefined;
  /** The question the shown layer answers. */
  answeredKey: string | null;
  error: Error | null;
  /** True while the shown layer is not the answer to the current question. */
  pending: boolean;
}

interface Question {
  key: string;
  filters: Filters;
  view: MapView;
}

export function useMapLayer(filters: Filters, view: MapView | null): MapLayerState {
  const key = view ? mapQuery(filters, view) : null;
  const [arrived, setArrived] = useState<{
    key: string | null;
    layer: MapLayer | undefined;
    error: Error | null;
  }>({ key: null, layer: undefined, error: null });

  const wanted = useRef<Question | null>(null);
  const running = useRef<AbortController | null>(null);

  useEffect(() => {
    wanted.current = key && view ? { key, filters, view } : null;
    if (running.current) return; // the running request asks the newest question when it ends

    const ask = (question: Question) => {
      const controller = new AbortController();
      running.current = controller;
      fetchMapLayer(question.filters, question.view, controller.signal).then(
        (layer) => finish(controller, { key: question.key, layer, error: null }),
        (error: unknown) => {
          if (isAbort(error)) return;
          finish(controller, {
            key: question.key,
            error: error instanceof Error ? error : new Error(String(error)),
          });
        },
      );
    };
    const finish = (
      controller: AbortController,
      result: { key: string; layer?: MapLayer; error: Error | null },
    ) => {
      if (controller.signal.aborted) return;
      running.current = null;
      setArrived((previous) => ({
        key: result.key,
        layer: result.layer ?? previous.layer,
        error: result.error,
      }));
      const next = wanted.current;
      if (next && next.key !== result.key) ask(next);
    };

    if (wanted.current) ask(wanted.current);
    // filters and view are read through `key`, which names them.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  // Stop the running request when the map goes away.
  useEffect(
    () => () => {
      running.current?.abort();
      running.current = null;
    },
    [],
  );

  return {
    layer: arrived.layer,
    answeredKey: arrived.key,
    error: arrived.key === key ? arrived.error : null,
    pending: key !== null && arrived.key !== key,
  };
}
