/**
 * The typefaces of Synapsis, registered from the files found in this folder.
 *
 * - Anthropic Serif (variable): the face of Synapsis: the name, the buttons, the text.
 * - The Seasons: the tagline. Until its files are here, Playfair Display stands in (styles.css, --font-tagline).
 *
 * Both are licensed for local use only, so their files are kept out of git (src/ui/.gitignore). A fresh clone has none
 * of them and still builds and runs: each page then draws with the next font of its stack. To add The Seasons, copy
 * its files here under the names listed below (.woff2, .woff or .otf); nothing else changes.
 */

/** Every font file in this folder, by name, as the address the browser loads it from. */
const files = import.meta.glob<string>('./*.{woff2,woff,otf}', {
  eager: true,
  query: '?url',
  import: 'default',
});

/** One face: a family, its file (without the extension) and the weights and style it covers. */
interface Face {
  family: string;
  file: string;
  weight: string;
  style: 'normal' | 'italic';
}

const faces: Face[] = [
  { family: 'Anthropic Serif', file: 'anthropic-serif-roman', weight: '300 800', style: 'normal' },
  { family: 'Anthropic Serif', file: 'anthropic-serif-italic', weight: '300 800', style: 'italic' },
  { family: 'The Seasons', file: 'the-seasons-light', weight: '300', style: 'normal' },
  { family: 'The Seasons', file: 'the-seasons-regular', weight: '400', style: 'normal' },
  { family: 'The Seasons', file: 'the-seasons-bold', weight: '700', style: 'normal' },
  { family: 'The Seasons', file: 'the-seasons-italic', weight: '400', style: 'italic' },
];

/** Registers every face whose file is present. Each file is fetched only when a page first draws with it. */
export function registerFonts() {
  for (const face of faces) {
    const url = ['woff2', 'woff', 'otf'].map((ext) => files[`./${face.file}.${ext}`]).find(Boolean);
    if (url) {
      const font = new FontFace(face.family, `url(${url})`, {
        weight: face.weight,
        style: face.style,
        display: 'swap',
      });
      document.fonts.add(font);
    }
  }
}
