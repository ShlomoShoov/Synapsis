/**
 * The viewer's choice of time zone for the whole Observe screen: their own (the default) or UTC, the zone the log
 * is stored in. TimeZoneProvider.tsx holds the choice and remembers it in this browser.
 */
import { createContext, use } from 'react';
import { timeFormats, type TimeFormats } from './format';

interface TimeZoneValue {
  formats: TimeFormats;
  setUtc: (utc: boolean) => void;
}

export const TimeZoneContext = createContext<TimeZoneValue>({
  formats: timeFormats(false),
  setUtc: () => {},
});

/** The time formats in the chosen zone. */
export function useTimeFormats(): TimeFormats {
  return use(TimeZoneContext).formats;
}

/** Changes the zone. */
export function useSetUtc(): (utc: boolean) => void {
  return use(TimeZoneContext).setUtc;
}
