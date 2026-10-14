import { describe, expect, it } from 'vitest';
import { ingestBody, isLive, servicesBody, type Stage } from './pipeline';
import { simulatedPipeline } from './simulated';
import { backlogDots, dotsPerSecond, drop, motionOf, pointAt, trackOf } from './tracks';

describe('the pipes', () => {
  it('measure their length and find a point along it', () => {
    const track = trackOf([
      { x: 0, y: 0 },
      { x: 0, y: 30 },
      { x: 40, y: 30 },
    ]);
    expect(track.total).toBe(70);
    expect(pointAt(track, 15)).toEqual({ x: 0, y: 15 });
    expect(pointAt(track, 50)).toEqual({ x: 20, y: 30 });
    expect(pointAt(track, 500)).toEqual({ x: 40, y: 30 });
  });

  it('drop from one card to the next, ending where they should', () => {
    const points = drop({ x: 10, y: 0 }, { x: 90, y: 100 });
    expect(points[0]).toEqual({ x: 10, y: 0 });
    expect(points[points.length - 1]).toEqual({ x: 90, y: 100 });
  });

  it('carry more dots for more records, none for none', () => {
    expect(dotsPerSecond(null)).toBe(0);
    expect(dotsPerSecond(0)).toBe(0);
    expect(dotsPerSecond(60)).toBeGreaterThan(dotsPerSecond(5));
    expect(dotsPerSecond(1e9)).toBeLessThanOrEqual(14);
    expect(backlogDots(18_500_000)).toBeGreaterThan(backlogDots(1_000));
  });

  it('stand still while the playback is paused and run faster with a fast replay', () => {
    expect(motionOf({ mode: 'live', playing: true, speed: 60 })).toBe(1);
    expect(motionOf({ mode: 'replay', playing: false, speed: 60 })).toBe(0);
    expect(motionOf({ mode: 'replay', playing: true, speed: 3600 })).toBeGreaterThan(
      motionOf({ mode: 'replay', playing: true, speed: 60 }),
    );
  });
});

describe('the pipeline at a moment', () => {
  const at = Date.parse('2026-10-13T12:00:00Z');

  it('reads the logs up to a replayed moment, and the present without a bound', () => {
    expect(ingestBody(at)).not.toHaveProperty('query');
    expect(servicesBody(at)).not.toHaveProperty('query');
    expect(ingestBody(at, true).query).toEqual({
      range: { timestamp: { lte: '2026-10-13T12:00:00.000Z' } },
    });
    expect(servicesBody(at, true).query).toEqual({
      range: { at: { lte: '2026-10-13T12:00:00.000Z' } },
    });
  });

  it('counts a stage as at work only around the moment shown', () => {
    const stage = { lastAt: at } as Stage;
    expect(isLive(stage, at + 60_000)).toBe(true);
    expect(isLive(stage, at + 180_000)).toBe(false);
    expect(isLive(stage, at - 3_600_000)).toBe(false);
  });

  it('is the same in the simulation whenever a replay comes back to it', () => {
    const api = { rate: 40, handled: 3_000_000, refused: 12, lastAt: at };
    expect(simulatedPipeline(at, api)).toEqual(simulatedPipeline(at, api));
    expect(simulatedPipeline(at, api).stages.map((stage) => stage.id)).toEqual([
      'api',
      'relay',
      'indexer',
      'projector',
      'engine',
    ]);
  });
});
