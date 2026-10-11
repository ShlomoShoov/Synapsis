/**
 * The data colours, in the two forms Observe needs:
 *
 * - CSS custom properties, set on the Observe root, so stylesheets write `var(--domain-cyber)` or
 *   `var(--outcome-rejected)` and never a raw value;
 * - glows for the 3D scene: the same hues, lifted in lightness, because light that is added to a dark room has to be
 *   brighter than ink on a page to read as the same colour.
 *
 * The values themselves live with their meaning: domains in ../domains.ts, outcomes and status in ../outcomes.ts.
 */
import type { CSSProperties } from 'react';
import { domains, type DomainId } from '../domains';
import { mix, withLightness } from '../lib/color';
import { outcomes, statusColors } from '../outcomes';

/** The custom properties for the Observe root element. */
export function paletteStyle(): CSSProperties {
  const style: Record<string, string> = {};
  for (const domain of domains) {
    style[`--domain-${domain.id}`] = domain.color;
  }
  for (const item of outcomes) {
    style[`--outcome-${item.key}`] = item.color;
  }
  for (const [name, color] of Object.entries(statusColors)) {
    style[`--status-${name}`] = color;
  }
  return style as CSSProperties;
}

/** The colour of a domain, lifted for light in the 3D scene. */
export function domainGlow(id: DomainId): string {
  const domain = domains.find((item) => item.id === id);
  return withLightness(domain?.color ?? '#8f8778', 0.8);
}

/** The faint end and the bright end of the sequential ramp (heatmaps): from just above the room to the paper. */
const rampLow = '#2a2620';
const rampHigh = '#f4f0e7';

/** The sequential colour for a share t between 0 (least) and 1 (most): one hue, darker means less. */
export function sequential(t: number): string {
  return mix(rampLow, rampHigh, Math.min(1, Math.max(0, t)));
}

/** The colours the 3D scene uses outside the data. */
export const sceneColors = {
  /** The room behind the scene: the same as --surface-0. */
  background: '#0b0a08',
  /** The light of the core: warm paper. */
  core: '#f6efe0',
  /** Engraved lines on the ground. */
  grid: '#b8af9e',
  /** Records refused at the core. */
  rejected: '#ff6a4f',
  /** Records written: the light the core takes in. */
  written: '#fff5e2',
} as const;
