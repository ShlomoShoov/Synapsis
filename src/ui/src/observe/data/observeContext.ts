/**
 * The context of Observe's data, shared by its two sections: where it comes from, what is known about each source,
 * and the health of each service. ObserveDataProvider.tsx fills it.
 */
import { createContext, use } from 'react';
import type { Catalog } from './catalog';
import type { ApiService, IngestLog, Ping } from './model';

/** Where the data comes from. */
export type DataMode = 'live' | 'simulated';

/** The health of the two services, as last checked (null while a check is on its way). */
export interface Health {
  log: Ping | null;
  api: Ping | null;
  checkedAt: number | null;
}

/** What the sections read from the context. */
export interface ObserveDataValue {
  mode: DataMode;
  setMode: (mode: DataMode) => void;
  log: IngestLog;
  api: ApiService;
  /** What is known about each source: empty for now, so names and domains are read from the source ids. */
  catalog: Catalog;
  health: Health;
}

export const ObserveDataContext = createContext<ObserveDataValue | null>(null);

/** The data of Observe; throws outside ObserveDataProvider. */
export function useObserveData(): ObserveDataValue {
  const value = use(ObserveDataContext);
  if (!value) {
    throw new Error('useObserveData is used outside ObserveDataProvider.');
  }
  return value;
}
