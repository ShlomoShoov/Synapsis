/**
 * The first thing on the home page's paper: what Synapsis is, in one paragraph, and its areas set like the contents
 * of a book: number, name, a dotted leader, and whether the area is open yet. Each line leads to its area.
 */
import { Link } from 'react-router';
import { areas } from '@/site/areas';
import styles from './Overview.module.css';

/** What Synapsis is, and where to go. */
export function Overview() {
  return (
    <section aria-labelledby="overview-title">
      <h2 id="overview-title" className={styles.eyebrow}>
        A knowledge engine
      </h2>
      <p className={styles.lead}>
        Synapsis keeps public records from many sources in one language:{' '}
        <em>when each thing happened</em>, and <em>when it became known</em>. On top of them it
        learns routines, notices what breaks them, and forecasts what comes next.
      </p>
      <nav aria-label="Areas">
        <ol className={styles.contents}>
          {areas.map((area, index) => (
            <li key={area.path}>
              <Link to={area.path} className={styles.entry}>
                <span className={styles.number}>({index + 1})</span>
                <span className={styles.name}>{area.name}</span>
                <span className={styles.leader} aria-hidden="true" />
                <span className={styles.state}>{area.ready ? 'Open' : 'Coming soon'}</span>
                <span className={styles.line}>{area.line}</span>
              </Link>
            </li>
          ))}
        </ol>
      </nav>
    </section>
  );
}
