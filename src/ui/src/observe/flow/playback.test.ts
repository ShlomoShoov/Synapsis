import { describe, expect, it } from 'vitest';
import { Playback } from './playback';

/** A playback on two hand-moved clocks: real milliseconds and wall time. */
function clocks() {
  const time = { real: 0, wall: 1_000_000 };
  const playback = new Playback(
    () => time.real,
    () => time.wall,
  );
  return { time, playback };
}

describe('Playback', () => {
  it('follows the wall clock while live', () => {
    const { time, playback } = clocks();
    time.wall += 5_000;
    expect(playback.now()).toBe(1_005_000);
  });

  it('replays from a moment at its speed, and holds still when paused', () => {
    const { time, playback } = clocks();
    playback.setSpeed(60);
    playback.seek(500_000);
    time.real += 1_000;
    expect(playback.now()).toBe(560_000);
    playback.pause();
    time.real += 1_000;
    expect(playback.now()).toBe(560_000);
    playback.play();
    time.real += 500;
    expect(playback.now()).toBe(590_000);
  });

  it('keeps the scene time when the speed changes mid-replay', () => {
    const { time, playback } = clocks();
    playback.setSpeed(10);
    playback.seek(500_000);
    time.real += 1_000;
    playback.setSpeed(100);
    expect(playback.now()).toBe(510_000);
    time.real += 1_000;
    expect(playback.now()).toBe(610_000);
  });

  it('becomes live again when a replay reaches the present, or when asked for a moment after now', () => {
    const { time, playback } = clocks();
    playback.setSpeed(3600);
    playback.seek(990_000);
    time.real += 10;
    playback.catchUp();
    expect(playback.getSnapshot().mode).toBe('live');
    playback.seek(time.wall + 10_000);
    expect(playback.getSnapshot().mode).toBe('live');
  });

  it('freezes the present when paused live', () => {
    const { time, playback } = clocks();
    playback.pause();
    time.wall += 5_000;
    expect(playback.getSnapshot()).toMatchObject({ mode: 'replay', playing: false });
    expect(playback.now()).toBe(1_000_000);
  });
});
