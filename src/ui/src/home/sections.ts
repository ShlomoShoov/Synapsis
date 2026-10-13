/**
 * The explanations on the home page, shown under the overview in this order. Each is a title, a text, and one image
 * or one video. Media files go in assets/media/home/ and are addressed from the site root ('/media/home/x.jpg').
 *
 * Empty for now: the page shows nothing here until the first item is added.
 */

/** The picture or film of an explanation. */
export type SectionMedia =
  { kind: 'image'; src: string; alt: string } | { kind: 'video'; src: string; label: string };

/** One explanation. */
export interface Section {
  title: string;
  text: string;
  media: SectionMedia;
}

export const sections: readonly Section[] = [];
