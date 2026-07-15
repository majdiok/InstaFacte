import { Injectable, signal, computed } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class PosOfflineService {
  private readonly online = signal(typeof navigator !== 'undefined' ? navigator.onLine : true);
  private readonly pendingCount = signal(0);

  readonly isOnline = computed(() => this.online());
  readonly pendingSyncCount = computed(() => this.pendingCount());

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('online', () => this.online.set(true));
      window.addEventListener('offline', () => this.online.set(false));
    }
  }

  setPendingCount(count: number): void {
    this.pendingCount.set(count);
  }
}
