/**
 * The key to the lights, bottom left: how many events shine, the colour of each category with its count, and every
 * matching event that cannot be drawn, with the reason. Nothing disappears silently.
 */
import type { Events } from './useEvents';
import { categoryOf, densityColor } from './palette';
import styles from './EventsKey.module.css';

/** Short words for the reasons the API gives (its own message is the tooltip). */
const reasons: Record<string, (one: boolean) => string> = {
  no_point: (one) => `${one ? 'has' : 'have'} no point: placed by region, logically, or not at all`,
  too_imprecise: (one) => `${one ? 'is' : 'are'} too imprecise for this scale`,
};

export function EventsKey({ events }: { events: Events | undefined }) {
  if (!events) {
    return (
      <aside className={styles.key} aria-label="Key">
        <p className={styles.quiet}>Reading the events…</p>
      </aside>
    );
  }
  const { layer, lights } = events;
  const counts = new Map<string, { name: string; color: string; count: number }>();
  for (const item of lights.items) {
    if (!item) continue;
    const category = categoryOf(item.type);
    const entry = counts.get(category.key) ?? { ...category, count: 0 };
    entry.count += 1;
    counts.set(category.key, entry);
  }
  const rows = [...counts.values()].sort((a, b) => b.count - a.count);
  return (
    <aside className={styles.key} aria-label="Key">
      <p className={styles.total}>
        <strong>{layer.events.toLocaleString('en')}</strong>
        {layer.events === 1 ? 'event' : 'events'} on the map
      </p>
      {layer.layer === 'points' && rows.length > 0 && (
        <ul className={styles.categories}>
          {rows.map((row) => (
            <li key={row.name}>
              <i style={{ background: row.color, boxShadow: `0 0 10px 1px ${row.color}` }} />
              <span>{row.name}</span>
              <b>{row.count.toLocaleString('en')}</b>
            </li>
          ))}
        </ul>
      )}
      {layer.layer === 'cells' && (
        <p className={styles.density}>
          <i style={{ background: densityColor, boxShadow: `0 0 10px 1px ${densityColor}` }} />
          Too many to show one by one: each light gathers the events of its area, larger with more.
          Zoom in to tell them apart.
        </p>
      )}
      {layer.events === 0 && <p className={styles.quiet}>No event with a place in this window.</p>}
      {layer.omitted.length > 0 && (
        <ul className={styles.omitted} aria-label="Matching events not drawn">
          {layer.omitted.map((omitted) => (
            <li key={omitted.reason} title={omitted.message}>
              <b>{omitted.count.toLocaleString('en')}</b> more{' '}
              {reasons[omitted.reason]?.(omitted.count === 1) ?? omitted.message}
            </li>
          ))}
        </ul>
      )}
    </aside>
  );
}
