import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Flex wrapper for primary consultation action + IA analyze button (consistent gap/wrap).
 */
@Component({
  selector: 'app-accounting-toolbar-actions',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="accounting-toolbar-actions" role="group">
      <ng-content></ng-content>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .accounting-toolbar-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--spacing-2);
      flex-shrink: 0;
    }
  `
})
export class AccountingToolbarActionsComponent {}
