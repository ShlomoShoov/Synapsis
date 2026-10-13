/**
 * The end of the home page: the name, and the Greek word it comes from.
 */
import styles from './HomePage.module.css';

/** The home page's footer. */
export function Footer() {
  return (
    <footer className={styles.footer}>
      <span>Synapsis, a knowledge engine.</span>
      <span>
        <i lang="el">σύναψις</i>, a joining together
      </span>
    </footer>
  );
}
