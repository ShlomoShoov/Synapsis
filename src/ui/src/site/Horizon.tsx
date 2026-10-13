/**
 * The dawn, veiled: Shlomo's horizon image blurred into colour alone, with a light that breathes where the sun will
 * rise, a mist that drifts across it and a printed grain. The backdrop of the notice pages ("Coming soon", not
 * found); decoration only. The home page shows the image itself, untouched (home/Hero).
 * Visitors who ask for reduced motion get the still picture (styles.css).
 */
import styles from './Horizon.module.css';

/** Shlomo's image (assets/media/home/): square and soft. */
export const horizonImage = '/media/home/horizon.jpg';

/** The veiled dawn, filling its positioned parent. */
export function Horizon() {
  return (
    <div className={styles.horizon} aria-hidden="true">
      <img className={styles.image} src={horizonImage} alt="" />
      <div className={styles.mist} style={{ backgroundImage: `url(${horizonImage})` }} />
      <div className={styles.light} />
      <div className={styles.grain} />
    </div>
  );
}
