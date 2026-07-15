import { Injectable, computed, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class DrawerOverlayService {
  private readonly openCount = signal(0);

  readonly isOpen = computed(() => this.openCount() > 0);

  registerOpen(): void {
    this.openCount.update((count) => count + 1);
  }

  registerClose(): void {
    this.openCount.update((count) => Math.max(0, count - 1));
  }
}
