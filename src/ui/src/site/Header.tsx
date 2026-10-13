/**
 * The bar over every page outside Observe: the name on the left and the three areas on the right.
 * Over the dawn the buttons sit in a pane of frosted glass; once paper slides under the bar (onPaper), the bar takes
 * the paper's colour and the buttons turn to ink. The page decides when the name shows: the home page hides it while
 * its own large name is in view.
 */
import { Link, NavLink } from 'react-router';
import { areas } from './areas';
import styles from './Header.module.css';

interface HeaderProps {
  /** Paper is under the bar (instead of the dawn). */
  onPaper?: boolean;
  /** Show the name on the left. */
  wordmark?: boolean;
  /** Arrive after the page's opening (the home page's first screen). */
  late?: boolean;
}

/** The site header. */
export function Header({ onPaper = false, wordmark = true, late = false }: HeaderProps) {
  return (
    <header
      className={styles.header}
      data-paper={onPaper || undefined}
      data-late={late || undefined}
    >
      <Link
        to="/home"
        className={styles.wordmark}
        data-shown={wordmark || undefined}
        aria-hidden={!wordmark || undefined}
        tabIndex={wordmark ? undefined : -1}
      >
        Synapsis
      </Link>
      <nav aria-label="Main" className={styles.nav}>
        {areas.map((area) => (
          <NavLink key={area.path} to={area.path} className={styles.button}>
            {area.name}
          </NavLink>
        ))}
      </nav>
    </header>
  );
}
