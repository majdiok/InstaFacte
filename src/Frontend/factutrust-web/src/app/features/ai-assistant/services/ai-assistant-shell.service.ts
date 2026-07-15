import { Injectable, signal } from '@angular/core';

/**
 * Lets feature pages request the main shell to open the AI chat panel (same as the FAB).
 * MainLayoutComponent reacts to {@link openPanelTick} increments.
 */
@Injectable({ providedIn: 'root' })
export class AiAssistantShellService {
  readonly openPanelTick = signal(0);

  requestOpenPanel(): void {
    this.openPanelTick.update(n => n + 1);
  }
}
