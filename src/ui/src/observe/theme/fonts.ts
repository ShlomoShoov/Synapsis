/**
 * The faces Observe adds to the brand serif (loaded by main.tsx). Imported by the Observe page only, so the home page
 * never downloads them. Each file carries Latin and Latin Extended; the browser fetches only what a page uses.
 *
 * - Instrument Sans (variable weight 400-700, width 75-100%): the interface and every number.
 * - Fragment Mono: codes, ids, times and labels.
 */
import '@fontsource-variable/instrument-sans/wdth.css';
import '@fontsource/fragment-mono/400.css';
