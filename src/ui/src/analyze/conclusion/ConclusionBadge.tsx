/** A conclusion in a list row: its glyph, "Conclusion · <type>", and its key numbers. */
import type { CSSProperties } from 'react';
import type { Item } from '../api/model';
import { badgeOf, conclusionTypeOf, keyNumbers, numbersOfItem } from './conclusion';
import { Glyph } from './Glyph';
import styles from './Conclusion.module.css';

export function ConclusionBadge({ item }: { item: Item }) {
  const style = conclusionTypeOf(item.type);
  const numbers = keyNumbers(item.type, numbersOfItem(item));
  return (
    <>
      <span className={styles.rowBadge} style={{ '--type': style.color } as CSSProperties}>
        <Glyph shape={style.shape} color={style.color} size={11} />
        {badgeOf(item.type).toLowerCase()}
      </span>
      {numbers.length > 0 && <span className={styles.rowNumbers}>{numbers.join(' · ')}</span>}
    </>
  );
}

/** The mark at the start of a row: a conclusion's glyph, else the record's round light. */
export function RowMark({ item, color }: { item: Item; color: string }) {
  if (item.kind === 'conclusion') {
    const style = conclusionTypeOf(item.type);
    return <Glyph shape={style.shape} color={style.color} size={12} />;
  }
  return <i style={{ background: color, boxShadow: `0 0 8px ${color}` }} />;
}
