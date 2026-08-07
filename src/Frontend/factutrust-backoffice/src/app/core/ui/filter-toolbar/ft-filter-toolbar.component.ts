import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Conteneur de barre de filtres avec deux zones distinctes :
 *  - Zone des filtres (recherche + dropdowns) — slot par défaut
 *  - Zone des actions (Reset, Vues sauvegardées, etc.) — slot `[ftActions]`
 *  - Zone des chips de filtres actifs (sous la toolbar) — slot `[ftChips]`
 *
 * Usage :
 *  ```html
 *  <ft-filter-toolbar>
 *    <input pInputText placeholder="Rechercher…" />
 *    <p-select [options]="plans" placeholder="Plan" />
 *
 *    <ng-container ftActions>
 *      <p-button label="Réinitialiser" [text]="true" />
 *    </ng-container>
 *
 *    <ng-container ftChips>
 *      <ft-chip label="Plan" value="Annuel" (remove)="…" />
 *    </ng-container>
 *  </ft-filter-toolbar>
 *  ```
 */
@Component({
  selector: 'ft-filter-toolbar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toolbar" role="search" aria-label="Filtres">
      <div class="toolbar__main">
        <div class="toolbar__filters">
          <ng-content />
        </div>
        <div class="toolbar__actions">
          <ng-content select="[ftActions]" />
        </div>
      </div>
      <div class="toolbar__chips" role="region" aria-label="Filtres actifs">
        <ng-content select="[ftChips]" />
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        margin-bottom: var(--gap-md);
      }

      .toolbar {
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: var(--gap-sm) var(--gap-md);
        display: flex;
        flex-direction: column;
        gap: var(--gap-sm);
      }

      .toolbar__main {
        display: flex;
        flex-wrap: wrap;
        gap: var(--gap-sm) var(--gap-md);
        align-items: center;
        justify-content: space-between;
      }

      .toolbar__filters {
        display: flex;
        flex-wrap: wrap;
        gap: var(--gap-xs);
        flex: 1 1 auto;
        min-width: 0;
        align-items: center;
      }

      .toolbar__actions {
        display: flex;
        gap: var(--gap-xs);
        flex-wrap: wrap;
        align-items: center;
      }

      .toolbar__actions:empty {
        display: none;
      }

      .toolbar__chips {
        display: flex;
        flex-wrap: wrap;
        gap: var(--gap-xs);
        align-items: center;
      }

      .toolbar__chips:empty {
        display: none;
      }
    `
  ]
})
export class FtFilterToolbarComponent {}
