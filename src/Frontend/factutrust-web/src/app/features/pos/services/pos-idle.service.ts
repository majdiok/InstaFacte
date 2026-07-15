import { Injectable, inject, signal, OnDestroy } from '@angular/core';

const DEFAULT_IDLE_MS = 5 * 60 * 1000;
const STORAGE_KEY = 'factutrust_pos_idle_minutes';

@Injectable({
  providedIn: 'root'
})
export class PosIdleService implements OnDestroy {
  readonly locked = signal(false);
  readonly idleMinutes = signal(5);

  private lastActivity = 0;
  private timerId: ReturnType<typeof setInterval> | null = null;
  private checkIntervalMs = 30_000;

  constructor() {
    try {
      const m = localStorage.getItem(STORAGE_KEY);
      if (m) this.idleMinutes.set(Math.max(1, Math.min(30, parseInt(m, 10))));
    } catch {
      // ignore
    }
  }

  setIdleMinutes(minutes: number): void {
    this.idleMinutes.set(Math.max(1, Math.min(30, minutes)));
    try {
      localStorage.setItem(STORAGE_KEY, String(this.idleMinutes()));
    } catch {
      // ignore
    }
  }

  touch(): void {
    if (this.locked()) return;
    this.lastActivity = Date.now();
  }

  startWatching(): void {
    this.lastActivity = Date.now();
    this.stopWatching();
    const idleMs = this.idleMinutes() * 60 * 1000;
    this.timerId = setInterval(() => {
      if (this.locked()) return;
      if (Date.now() - this.lastActivity >= idleMs) {
        this.locked.set(true);
        this.stopWatching();
      }
    }, this.checkIntervalMs);
  }

  stopWatching(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }

  unlock(): void {
    this.locked.set(false);
    this.lastActivity = Date.now();
    this.startWatching();
  }

  ngOnDestroy(): void {
    this.stopWatching();
  }
}
