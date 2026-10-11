/**
 * Seeded randomness for the simulation: the same seed always gives the same sequence, so the simulated log is the
 * same every time a moment of it is read (and in tests).
 */

/** A 32-bit hash of a text (FNV-1a), to turn names into seeds. */
export function hashText(text: string): number {
  let hash = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) {
    hash ^= text.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193);
  }
  return hash >>> 0;
}

/** A sequence of numbers between 0 and 1 from a seed (mulberry32). */
export class Random {
  private state: number;

  constructor(seed: number) {
    this.state = seed >>> 0;
  }

  /** The next number, 0 included, 1 excluded. */
  next(): number {
    this.state = (this.state + 0x6d2b79f5) >>> 0;
    let t = this.state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  }

  /** A whole number from `min` to `max`, both included. */
  int(min: number, max: number): number {
    return min + Math.floor(this.next() * (max - min + 1));
  }

  /** One item of a list. */
  pick<T>(items: readonly T[]): T {
    return items[Math.floor(this.next() * items.length)] as T;
  }

  /** One item of a list, each as likely as its weight. */
  weighted<T>(items: readonly T[], weight: (item: T) => number): T {
    const total = items.reduce((sum, item) => sum + weight(item), 0);
    let left = this.next() * total;
    for (const item of items) {
      left -= weight(item);
      if (left <= 0) {
        return item;
      }
    }
    return items[items.length - 1] as T;
  }

  /** The wait until the next of events that happen `rate` times per unit on average (exponential). */
  exponential(rate: number): number {
    return -Math.log(1 - this.next()) / rate;
  }

  /** A normal number (mean 0, deviation 1), by Box-Muller. */
  normal(): number {
    const u = 1 - this.next();
    const v = this.next();
    return Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v);
  }

  /** A positive number spread around `median`: `spread` 0.5 gives most values within about x0.6 to x1.6. */
  logNormal(median: number, spread: number): number {
    return median * Math.exp(this.normal() * spread);
  }

  /** How many of `n` trials succeed when each succeeds with probability `p` (approximated for large n). */
  binomial(n: number, p: number): number {
    if (n <= 0 || p <= 0) {
      return 0;
    }
    if (n < 40) {
      let count = 0;
      for (let i = 0; i < n; i++) {
        if (this.next() < p) {
          count++;
        }
      }
      return count;
    }
    const mean = n * p;
    const deviation = Math.sqrt(n * p * (1 - p));
    return Math.min(n, Math.max(0, Math.round(mean + deviation * this.normal())));
  }
}
