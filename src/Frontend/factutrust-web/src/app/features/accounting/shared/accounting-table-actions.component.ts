import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Conteneur flex pour actions icon-only dans les tables comptables.
 */
@Component({
  selector: 'app-accounting-table-actions',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="accounting-table-actions" role="group">
      <ng-content></ng-content>
    </div>
  `,
  styles: `
    :host {
      display: inline-block;
    }
    .accounting-table-actions {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-1);
      flex-wrap: nowrap;
    }
  `
})
export class AccountingTableActionsComponent {}
