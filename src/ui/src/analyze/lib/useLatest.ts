/**
 * useLatest: the answer to the newest question, kept current while the question changes.
 *
 * One request at a time, and always the newest question: while a request runs, newer questions wait, and when it ends
 * only the latest of them is asked. So dragging through time redraws the map as fast as the API answers, without a
 * queue of stale requests, and the last position always wins. The previous answer stays until the next arrives.
 * A null key asks nothing.
 */
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { isAbort } from '../api/http';

export interface Latest<A> {
  answer: A | undefined;
  error: Error | null;
  /** True while the shown answer is not the answer to the current question. */
  pending: boolean;
}

export function useLatest<A>(
  key: string | null,
  load: (signal: AbortSignal) => Promise<A>,
): Latest<A> {
  const [arrived, setArrived] = useState<{
    key: string | null;
    answer: A | undefined;
    error: Error | null;
  }>({ key: null, answer: undefined, error: null });
  const wanted = useRef<{ key: string; load: (signal: AbortSignal) => Promise<A> } | null>(null);
  const running = useRef<AbortController | null>(null);
  const loadRef = useRef(load);
  useLayoutEffect(() => {
    loadRef.current = load;
  });

  useEffect(() => {
    wanted.current = key === null ? null : { key, load: loadRef.current };
    if (running.current) return; // the running request asks the newest question when it ends

    const ask = (question: { key: string; load: (signal: AbortSignal) => Promise<A> }) => {
      const controller = new AbortController();
      running.current = controller;
      question.load(controller.signal).then(
        (answer) => finish(controller, { key: question.key, answer, error: null }),
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
      result: { key: string; answer?: A; error: Error | null },
    ) => {
      if (controller.signal.aborted) return;
      running.current = null;
      setArrived((previous) => ({
        key: result.key,
        answer: result.error ? previous.answer : result.answer,
        error: result.error,
      }));
      const next = wanted.current;
      if (next && next.key !== result.key) ask(next);
    };

    if (wanted.current) ask(wanted.current);
  }, [key]);

  // Stop the running request when the view goes away.
  useEffect(
    () => () => {
      running.current?.abort();
      running.current = null;
    },
    [],
  );

  return {
    answer: arrived.answer,
    error: arrived.key === key ? arrived.error : null,
    pending: key !== null && arrived.key !== key,
  };
}
