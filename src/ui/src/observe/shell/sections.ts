/**
 * The sections of Observe, in page order. The sidebar lists them; each section's element carries
 * data-nav-section="<id>" and its own id, so the sidebar can find it, follow it and scroll to it.
 *
 * Inside a section, any element with an id and data-nav-anchor="<label>" appears under the section in the sidebar
 * (the dashboard panels do this), so a new panel never needs a change here.
 */
import type { IconName } from '../ui/Icon';

/** The id of a section. */
export type SectionId = 'flow' | 'metrics' | 'pipeline';

/** One section. */
export interface SectionInfo {
  id: SectionId;
  /** Its number, as the sidebar and the section's heading write it. */
  number: string;
  title: string;
  icon: IconName;
  /** One line on what it shows. */
  summary: string;
  /** Its keyboard shortcut. */
  key: string;
}

/** A section by its id. */
export function sectionInfo(id: SectionId): SectionInfo {
  return sections.find((section) => section.id === id) as SectionInfo;
}

/** Every section, in page order. */
export const sections: readonly SectionInfo[] = [
  {
    id: 'flow',
    number: '01',
    title: 'Live flow',
    icon: 'flow',
    summary: 'Every batch on its way from its source to the core.',
    key: '1',
  },
  {
    id: 'metrics',
    number: '02',
    title: 'Metrics',
    icon: 'metrics',
    summary: 'What arrived, what was stored, what was refused.',
    key: '2',
  },
  {
    id: 'pipeline',
    number: '03',
    title: 'Pipeline',
    icon: 'chart',
    summary: 'Below the API, stage by stage: how fast, how far behind, what failed.',
    key: '3',
  },
];
