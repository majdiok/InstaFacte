import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

/**
 * Header de page standardisé : titre + sous-titre + slot actions.
 *
 * Slots (via attribute selectors) :
 *  - `[ftBreadcrumb]` — fil d'Ariane optionnel au-dessus du titre
 *  - `[ftActions]` — boutons d'action à droite du titre
 *
 * Usage :
 *  ```html
 *  <ft-page-header title="Entreprises"
 *                  subtitle="Annuaire des entreprises onboardées.">
 *    <ft-breadcrumb ftBreadcrumb [items]="breadcrumb()" />
 *    <ng-container ftActions>
 *      <p-button label="Exporter" icon="pi pi-download" [outlined]="true" />
 *      <p-button label="Nouvelle entreprise" icon="pi pi-plus" />
 *    </ng-container>
 *  </ft-page-header>
 *  ```
 */
@Component({
  selector: 'ft-page-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-head">
      <div class="page-head__top">
        <ng-content select="[ftBreadcrumb]" />
      </div>
      <div class="page-head__row">
        <div class="page-head__titles">
          <h1 class="page-head__title">{{ title }}</h1>
          @if (subtitle) {
            <p class="page-head__subtitle">{{ subtitle }}</p>
          }
        </div>
        <div class="page-head__actions">
          <ng-content select="[ftActions]" />
        </div>
      </div>
    </header>
  `,
  styles: [
    `
      :host {
        display: block;
        margin-bottom: var(--gap-section);
      }

      .page-head {
        display: flex;
        flex-direction: column;
        gap: var(--gap-sm);
      }

      .page-head__top:empty {
        display: none;
      }

      .page-head__row {
        display: flex;
        flex-wrap: wrap;
        gap: var(--gap-md) var(--gap-lg);
        align-items: flex-end;
        justify-content: space-between;
      }

      .page-head__titles {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        min-width: 0;
        flex: 1 1 auto;
      }

      .page-head__title {
        margin: 0;
        font-size: 1.6rem;
        font-weight: 650;
        line-height: 1.2;
        letter-spacing: -0.02em;
        color: var(--ft-text);
      }

      .page-head__subtitle {
        margin: 0;
        font-size: 0.9rem;
        color: var(--ft-text-muted);
        line-height: 1.5;
        max-width: 64ch;
      }

      .page-head__actions {
        display: flex;
        gap: var(--gap-xs);
        flex-wrap: wrap;
        align-items: center;
      }

      .page-head__actions:empty {
        display: none;
      }

      @media (max-width: 640px) {
        .page-head__title {
          font-size: 1.35rem;
        }
      }
    `
  ]
})
export class FtPageHeaderComponent {
  @Input({ required: true }) title = '';
  @Input() subtitle: string | null = null;
}
