/**
 * A quiet page over the veiled dawn: a small label, a title, an optional line, and the way back home.
 * The layout of the "Coming soon" pages and of the not-found page.
 */
import { Link } from 'react-router';
import { Header } from './Header';
import { Horizon } from './Horizon';
import { useFontsReady } from './useFontsReady';
import styles from './NoticePage.module.css';

interface NoticePageProps {
  label: string;
  title: string;
  line?: string;
}

/** A page with one short message. */
export function NoticePage({ label, title, line }: NoticePageProps) {
  const ready = useFontsReady();

  return (
    <div className={styles.page}>
      <Horizon />
      <Header />
      <main className={styles.content} data-ready={ready || undefined}>
        <p className={styles.label}>{label}</p>
        <h1 className={styles.title}>{title}</h1>
        {line && <p className={styles.line}>{line}</p>}
        <Link to="/home" className={styles.home}>
          Return home
        </Link>
      </main>
    </div>
  );
}
