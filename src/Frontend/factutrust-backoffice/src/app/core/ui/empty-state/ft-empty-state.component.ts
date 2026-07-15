import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

export type FtEmptyVariant = 'table-empty' | 'search-no-result' | 'all-clear' | 'error';

/**
 * État vide riche avec illustration SVG inline + titre + description + slot action.
 *
 * Usage :
 *  ```html
 *  <ft-empty-state variant="search-no-result"
 *                  title="Aucune entreprise ne correspond"
 *                  description="Essayez de retirer un filtre.">
 *    <button (click)="reset()">Réinitialiser</button>
 *  </ft-empty-state>
 *  ```
 */
@Component({
  selector: 'ft-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty" role="status" aria-live="polite">
      <div class="empty__illustration" aria-hidden="true">
        @switch (variant) {
          @case ('table-empty') {
            <svg viewBox="0 0 96 96" fill="none" stroke="currentColor" stroke-width="1.5">
              <rect x="14" y="22" width="68" height="52" rx="6" />
              <line x1="14" y1="36" x2="82" y2="36" />
              <line x1="32" y1="36" x2="32" y2="74" />
              <line x1="14" y1="50" x2="82" y2="50" />
              <line x1="14" y1="62" x2="82" y2="62" />
            </svg>
          }
          @case ('search-no-result') {
            <svg viewBox="0 0 96 96" fill="none" stroke="currentColor" stroke-width="1.5">
              <circle cx="42" cy="42" r="22" />
              <line x1="60" y1="60" x2="78" y2="78" stroke-linecap="round" stroke-width="2.5" />
              <line x1="34" y1="42" x2="50" y2="42" stroke-linecap="round" />
            </svg>
          }
          @case ('all-clear') {
            <svg viewBox="0 0 96 96" fill="none" stroke="currentColor" stroke-width="1.5">
              <circle cx="48" cy="48" r="32" />
              <path d="M34 48 L44 58 L62 38" stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" />
            </svg>
          }
          @case ('error') {
            <svg viewBox="0 0 96 96" fill="none" stroke="currentColor" stroke-width="1.5">
              <path d="M48 16 L84 76 L12 76 Z" stroke-linejoin="round" />
              <line x1="48" y1="40" x2="48" y2="56" stroke-linecap="round" stroke-width="2.5" />
              <circle cx="48" cy="66" r="2" fill="currentColor" />
            </svg>
          }
        }
      </div>
      <h3 class="empty__title">{{ title }}</h3>
      @if (description) {
        <p class="empty__desc">{{ description }}</p>
      }
      <div class="empty__action">
        <ng-content />
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .empty {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        text-align: center;
        padding: 3rem 1.5rem;
        gap: var(--gap-sm);
      }

      .empty__illustration {
        width: 6rem;
        height: 6rem;
        color: var(--ft-text-subtle);
        opacity: 0.85;
        margin-bottom: var(--gap-xs);
      }

      .empty__illustration svg {
        width: 100%;
        height: 100%;
      }

      :host ::ng-deep .empty__illustration[data-variant='all-clear'] {
        color: var(--ft-success-text);
      }

      .empty__title {
        margin: 0;
        color: var(--ft-text);
        font-size: 1.05rem;
        font-weight: 600;
        letter-spacing: -0.01em;
      }

      .empty__desc {
        margin: 0;
        color: var(--ft-text-muted);
        font-size: 0.875rem;
        max-width: 28rem;
        line-height: 1.5;
      }

      .empty__action {
        margin-top: var(--gap-md);
        display: flex;
        gap: var(--gap-sm);
        justify-content: center;
        flex-wrap: wrap;
      }

      .empty__action:empty {
        display: none;
      }
    `
  ]
})
export class FtEmptyStateComponent {
  @Input({ required: true }) title = '';
  @Input() description: string | null = null;
  @Input() variant: FtEmptyVariant = 'table-empty';
}
