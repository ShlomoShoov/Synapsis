/**
 * The playback of the live flow: the clock the scene follows. Live, the scene's time is now. Replaying, it starts at
 * a moment of the past and runs at a chosen speed (1x is real time, 3600x is an hour a second), playing or paused.
 * A replay that catches up with the present becomes live again.
 *
 * A small store outside React: views subscribe with useSyncExternalStore and read the time when they draw, so the
 * clock costs nothing between frames.
 */

/** The state of the playback. */
export interface PlaybackState {
  mode: 'live' | 'replay';
  playing: boolean;
  /** Milliseconds of the log per millisecond of real time. */
  speed: number;
  /** The scene's time at `anchorReal`. */
  anchorTime: number;
  /** performance.now() when the state last changed. */
  anchorReal: number;
  /** Changes on every seek, so readers can tell a jump from a steady run. */
  seekCount: number;
  /** Where the current replay began (the moment of the last jump, or of the pause that froze the present). */
  origin: number;
}

/** The speeds offered, in log milliseconds per real millisecond. */
export const speeds = [1, 10, 60, 600, 3600, 21600] as const;

/** A speed as people say it: 1x, 60x, 1 h/s. */
export function speedLabel(speed: number): string {
  if (speed >= 3600) {
    return `${speed / 3600} h/s`;
  }
  if (speed >= 60) {
    return `${speed / 60} min/s`;
  }
  return `${speed}×`;
}

/** The clock of the scene. */
export class Playback {
  private state: PlaybackState;
  private readonly listeners = new Set<() => void>();
  private readonly realNow: () => number;
  private readonly wallNow: () => number;

  constructor(realNow: () => number = () => performance.now(), wallNow: () => number = Date.now) {
    this.realNow = realNow;
    this.wallNow = wallNow;
    this.state = {
      mode: 'live',
      playing: true,
      speed: 60,
      anchorTime: wallNow(),
      anchorReal: realNow(),
      seekCount: 0,
      origin: wallNow(),
    };
  }

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  getSnapshot = (): PlaybackState => this.state;

  /** The scene's time now. */
  now(): number {
    const { mode, playing, speed, anchorTime, anchorReal } = this.state;
    if (mode === 'live') {
      return this.wallNow();
    }
    if (!playing) {
      return anchorTime;
    }
    return anchorTime + (this.realNow() - anchorReal) * speed;
  }

  /** Back to the present, following the log as it is written. */
  goLive() {
    if (this.state.mode !== 'live') {
      this.set({ mode: 'live', playing: true, seekCount: this.state.seekCount + 1 });
    }
  }

  /** Jumps to a moment of the past (or to live, for a moment at or after now). */
  seek(time: number) {
    if (time >= this.wallNow() - 1_000) {
      this.goLive();
      return;
    }
    this.set({
      mode: 'replay',
      anchorTime: time,
      origin: time,
      seekCount: this.state.seekCount + 1,
    });
  }

  play() {
    if (this.state.mode === 'replay' && !this.state.playing) {
      this.set({ playing: true });
    }
  }

  pause() {
    if (this.state.mode === 'live') {
      // Pausing the present freezes it: a replay that waits at this moment.
      const now = this.wallNow();
      this.set({ mode: 'replay', playing: false, anchorTime: now, origin: now });
    } else if (this.state.playing) {
      this.set({ playing: false });
    }
  }

  toggle() {
    if (this.state.mode === 'live' || this.state.playing) {
      this.pause();
    } else {
      this.play();
    }
  }

  setSpeed(speed: number) {
    this.set({ speed });
  }

  /** Becomes live again once a replay reaches the present; call it now and then. */
  catchUp() {
    if (this.state.mode === 'replay' && this.state.playing && this.now() >= this.wallNow()) {
      this.goLive();
    }
  }

  /** Changes the state, re-anchoring the clock so the scene time does not jump. */
  private set(change: Partial<PlaybackState>) {
    const time = this.now();
    this.state = { ...this.state, anchorTime: time, anchorReal: this.realNow(), ...change };
    for (const listener of this.listeners) {
      listener();
    }
  }
}
