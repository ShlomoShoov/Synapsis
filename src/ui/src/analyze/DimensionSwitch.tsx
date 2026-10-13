/**
 * The dimensions, top centre: the map (where things are), the graph (what the open object is connected to) and the
 * logical tree (where things are filed). The open object stays open across them; the graph needs one.
 */
import styles from './DimensionSwitch.module.css';

export type Dimension = 'map' | 'graph' | 'logical';

const dimensions: { id: Dimension; label: string; icon: React.ReactNode }[] = [
  {
    id: 'map',
    label: 'Map',
    icon: (
      <>
        <circle cx="10" cy="10" r="6.5" />
        <ellipse cx="10" cy="10" rx="2.8" ry="6.5" />
        <path d="M3.5 10h13" />
      </>
    ),
  },
  {
    id: 'graph',
    label: 'Graph',
    icon: (
      <>
        <circle cx="10" cy="10" r="2.2" />
        <circle cx="4" cy="5" r="1.5" />
        <circle cx="16" cy="6" r="1.5" />
        <circle cx="6" cy="16" r="1.5" />
        <circle cx="15.5" cy="15" r="1.5" />
        <path d="M8.3 8.6 5.2 6M11.9 9.1l2.8-2.2M8.6 11.7l-1.6 3M11.7 11.5l2.6 2.4" />
      </>
    ),
  },
  {
    id: 'logical',
    label: 'Logical',
    icon: (
      <>
        <rect x="3" y="3.5" width="5" height="3.5" rx="1" />
        <rect x="10" y="9" width="6.5" height="3" rx="1" />
        <rect x="10" y="14.5" width="6.5" height="3" rx="1" />
        <path d="M5.5 7v8.5H10M5.5 10.5H10" />
      </>
    ),
  },
];

export function DimensionSwitch({
  value,
  onChange,
  hasObject,
}: {
  value: Dimension;
  onChange: (dimension: Dimension) => void;
  /** An object is open (the graph shows an object's connections). */
  hasObject: boolean;
}) {
  return (
    <nav className={styles.switch} aria-label="Dimension">
      {dimensions.map((dimension) => (
        <button
          key={dimension.id}
          type="button"
          aria-pressed={dimension.id === value}
          disabled={dimension.id === 'graph' && !hasObject}
          title={
            dimension.id === 'graph' && !hasObject
              ? 'Open an object to see its connections'
              : undefined
          }
          onClick={() => onChange(dimension.id)}
        >
          <svg viewBox="0 0 20 20" aria-hidden="true">
            {dimension.icon}
          </svg>
          {dimension.label}
        </button>
      ))}
    </nav>
  );
}
