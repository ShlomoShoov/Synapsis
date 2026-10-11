/**
 * Holds the viewer's choice of time zone (timeZone.ts) and remembers it in this browser.
 */
import { useMemo, useState, type ReactNode } from 'react';
import { timeFormats } from './format';
import { readStored, writeStored } from './storage';
import { TimeZoneContext } from './timeZone';

/** Provides the time zone choice to everything inside it. */
export function TimeZoneProvider({ children }: { children: ReactNode }) {
  const [utc, setUtcState] = useState(() => readStored('utc', false));
  const value = useMemo(
    () => ({
      formats: timeFormats(utc),
      setUtc: (next: boolean) => {
        writeStored('utc', next);
        setUtcState(next);
      },
    }),
    [utc],
  );
  return <TimeZoneContext value={value}>{children}</TimeZoneContext>;
}
