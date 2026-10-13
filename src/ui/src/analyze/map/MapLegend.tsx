/**
 * The key to the map, top right: what the marks mean (the hexagons' ramp, or the points' domains), how many events are
 * drawn, and every matching event that is not, with the reason. Nothing disappears silently.
 */
import type { MapLayer } from '../api/model';
import { cellRamp, domainOf, domains, otherDomain } from './palette';
import styles from './MapLegend.module.css';

/** The width of a hexagon (twice its edge), by resolution, as the contract states the edges. */
const acrossKm: Record<number, number> = { 2: 316, 3: 120, 4: 46, 5: 17 };

/** Short words for the reasons the API gives (its own message is the tooltip). */
const reasons: Record<string, (one: boolean) => string> = {
  no_point: (one) =>
    `${one ? 'has' : 'have'} no point on the map: placed by region, logically, or not at all`,
  too_imprecise: (one) => `${one ? 'is' : 'are'} too imprecise for hexagons this size`,
};

export function MapLegend({ layer }: { layer: MapLayer | undefined }) {
  if (!layer) {
    return (
      <aside className={styles.legend} aria-label="Map key">
        <p className={styles.quiet}>Reading the events…</p>
      </aside>
    );
  }
  const max = layer.cells.reduce((most, cell) => Math.max(most, cell.count), 0);
  const presentDomains = [...new Set(layer.points.map((item) => domainOf(item.type)))].sort();
  return (
    <aside className={styles.legend} aria-label="Map key">
      <p className={styles.count}>
        <strong>{layer.events.toLocaleString('en')}</strong>{' '}
        {layer.events === 1 ? 'event' : 'events'} on the map
      </p>

      {layer.layer === 'cells' && layer.cells.length > 0 && (
        <div className={styles.ramp}>
          <span className={styles.caption}>Events per hexagon</span>
          <span
            className={styles.bar}
            style={{ background: `linear-gradient(to right, ${cellRamp.join(', ')})` }}
          />
          <span className={styles.ends}>
            <span>1</span>
            <span>{max.toLocaleString('en')}</span>
          </span>
          <span className={styles.note}>
            Taller and darker: more events.
            {layer.resolution !== undefined && acrossKm[layer.resolution] !== undefined && (
              <> Each hexagon is about {acrossKm[layer.resolution]} km across.</>
            )}{' '}
            Click one to look closer.
          </span>
        </div>
      )}

      {layer.layer === 'points' && presentDomains.length > 0 && (
        <ul className={styles.domains}>
          {presentDomains.map((domain) => {
            const { name, color } = domains[domain] ?? otherDomain;
            return (
              <li key={domain}>
                <span className={styles.swatch} style={{ background: color }} />
                {name}
              </li>
            );
          })}
        </ul>
      )}

      {layer.events === 0 && (
        <p className={styles.quiet}>No event with a place in this time window.</p>
      )}

      {layer.omitted.length > 0 && (
        <ul className={styles.omitted} aria-label="Matching events not drawn">
          {layer.omitted.map((omitted) => (
            <li key={omitted.reason} title={omitted.message}>
              <strong>{omitted.count.toLocaleString('en')}</strong> more{' '}
              {reasons[omitted.reason]?.(omitted.count === 1) ?? omitted.message}
            </li>
          ))}
        </ul>
      )}
    </aside>
  );
}
