/** A conclusion type's glyph, inline (lists, the key, the panel, hover labels): the same shape as on the map. */
import { glyphs, type Shape } from './conclusion';
import styles from './Conclusion.module.css';

export function Glyph({ shape, color, size = 14 }: { shape: Shape; color: string; size?: number }) {
  return (
    <svg
      className={styles.glyph}
      viewBox="-1 -1 22 22"
      width={size}
      height={size}
      aria-hidden="true"
      style={{ color }}
    >
      <path d={glyphs[shape]} />
    </svg>
  );
}
