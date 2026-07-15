import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Barre de filtres comptables : grille responsive (champs à gauche, actions à droite).
 */
@Component({
  selector: 'app-accounting-filter-bar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="accounting-filter-bar" [attr.aria-label]="ariaLabel || null">
      @if (heading) {
        <h2 class="accounting-filter-bar__heading">{{ heading }}</h2>
      }
      <div class="accounting-filter-bar__inner">
        <div class="accounting-filter-bar__fields">
          <ng-content select="[accountingFilterFields]"></ng-content>
        </div>
        <div class="accounting-filter-bar__actions">
          <ng-content select="[accountingFilterActions]"></ng-content>
        </div>
      </div>
    </div>
  `,
  styles: `
    @import './accounting-layout';
    .accounting-filter-bar {
      margin-bottom: var(--spacing-4);
    }
    .accounting-filter-bar__heading {
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
      margin: 0 0 var(--spacing-3);
    }
    .accounting-filter-bar__inner {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: var(--spacing-4);
    }
    .accounting-filter-bar__fields {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: var(--spacing-4);
      flex: 1 1 auto;
      min-width: 0;
    }
    .accounting-filter-bar__actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--spacing-3);
      flex-shrink: 0;
    }
  `
})
export class AccountingFilterBarComponent {
  @Input() heading = '';
  @Input() ariaLabel = '';
}
