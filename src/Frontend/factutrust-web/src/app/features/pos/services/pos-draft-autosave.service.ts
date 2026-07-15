import { Injectable, inject, signal, OnDestroy } from '@angular/core';
import { PosStateService, PosState } from './pos-state.service';

const STORAGE_KEY = 'factutrust_pos_draft';
const SAVE_INTERVAL_MS = 2 * 60 * 1000;

@Injectable({
  providedIn: 'root'
})
export class PosDraftAutosaveService implements OnDestroy {
  private readonly posState = inject(PosStateService);
  readonly hasStoredDraft = signal(false);
  private intervalId: ReturnType<typeof setInterval> | null = null;

  start(): void {
    this.checkStored();
    this.stop();
    this.intervalId = setInterval(() => this.saveIfDirty(), SAVE_INTERVAL_MS);
  }

  stop(): void {
    if (this.intervalId) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }

  private saveIfDirty(): void {
    if (!this.posState.isDirty() || this.posState.lines().length === 0) return;
    try {
      const snapshot = this.posState.getSnapshot();
      localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot));
      this.hasStoredDraft.set(true);
    } catch {
      // ignore
    }
  }

  checkStored(): void {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      this.hasStoredDraft.set(!!raw && raw.length > 2);
    } catch {
      this.hasStoredDraft.set(false);
    }
  }

  getStored(): PosState | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      return JSON.parse(raw) as PosState;
    } catch {
      return null;
    }
  }

  restore(): boolean {
    const stored = this.getStored();
    if (!stored || !stored.lines?.length) return false;
    this.posState.restoreSnapshot(stored);
    this.clear();
    return true;
  }

  clear(): void {
    try {
      localStorage.removeItem(STORAGE_KEY);
      this.hasStoredDraft.set(false);
    } catch {
      // ignore
    }
  }

  ngOnDestroy(): void {
    this.stop();
  }
}
