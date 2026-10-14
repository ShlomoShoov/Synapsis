/**
 * Provides Observe's data (observeContext.ts) to its sections.
 *
 * The mode is "live" unless the viewer chose the simulation: from the page (remembered in this browser) or with
 * ?data=simulated in the address. The simulation is never chosen silently: when the services cannot be reached,
 * the sections say so and offer it.
 */
import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { healthCheckMs } from '../config';
import { writeStored } from '../lib/storage';
import { ApiClient } from './api/ApiClient';
import { emptyCatalog } from './catalog';
import { ElasticIngestLog } from './elastic/ElasticIngestLog';
import { isAbort } from './http';
import type { ApiService, IngestLog, Ping } from './model';
import {
  ObserveDataContext,
  type DataMode,
  type Health,
  type ObserveDataValue,
} from './observeContext';
import { SimulatedIngestLog } from './simulation/SimulatedIngestLog';
import { SimulatedApi } from './simulation/SimulatedApi';
import { LogSimulator } from './simulation/simulator';

/** The two services of a mode. */
interface Services {
  log: IngestLog;
  api: ApiService;
}

/** The mode asked for in the address (?data=simulated or ?data=live), else the remembered one, else live. */
function initialMode(): DataMode {
  const asked = new URLSearchParams(window.location.search).get('data');
  if (asked === 'simulated' || asked === 'live') {
    return asked;
  }
  // The page no longer offers the switch: only the address asks for the simulation.
  return 'live';
}

/** The services of a mode. */
function servicesOf(mode: DataMode, epoch: number): Services {
  if (mode === 'simulated') {
    return {
      log: new SimulatedIngestLog(new LogSimulator({ epoch })),
      api: new SimulatedApi(),
    };
  }
  return { log: new ElasticIngestLog(), api: new ApiClient() };
}

/** Provides the data of Observe to everything inside it. */
export function ObserveDataProvider({ children }: { children: ReactNode }) {
  const [choice, setChoice] = useState<{ mode: DataMode; since: number }>(() => ({
    mode: initialMode(),
    since: Date.now(),
  }));
  const mode = choice.mode;
  const services = useMemo(() => servicesOf(choice.mode, choice.since), [choice]);
  /** The last health check, with the services it checked (a check of other services counts as none). */
  const [checked, setChecked] = useState<{
    services: Services;
    log: Ping;
    api: Ping;
    at: number;
  } | null>(null);

  // Checks both services now and then; the sidebar shows the answers.
  useEffect(() => {
    const controller = new AbortController();
    const check = async () => {
      try {
        const [log, api] = await Promise.all([
          services.log.ping(controller.signal),
          services.api.ping(controller.signal),
        ]);
        setChecked({ services, log, api, at: Date.now() });
      } catch (error) {
        if (!isAbort(error)) {
          throw error;
        }
      }
    };
    void check();
    const timer = window.setInterval(() => void check(), healthCheckMs);
    return () => {
      controller.abort();
      window.clearInterval(timer);
    };
  }, [services]);

  const health: Health =
    checked && checked.services === services
      ? { log: checked.log, api: checked.api, checkedAt: checked.at }
      : { log: null, api: null, checkedAt: null };

  const value: ObserveDataValue = {
    mode,
    setMode: (next: DataMode) => {
      writeStored('data-mode', next);
      const url = new URL(window.location.href);
      url.searchParams.delete('data');
      window.history.replaceState(window.history.state, '', url);
      setChoice({ mode: next, since: Date.now() });
    },
    log: services.log,
    api: services.api,
    catalog: emptyCatalog,
    health,
  };

  return <ObserveDataContext value={value}>{children}</ObserveDataContext>;
}
