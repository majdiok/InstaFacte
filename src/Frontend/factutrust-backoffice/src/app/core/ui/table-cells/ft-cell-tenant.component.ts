import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FtAvatarComponent } from '../avatar/ft-avatar.component';

/**
 * Cellule "Entreprise" : avatar + raison sociale (en gras) + email muted en dessous.
 *
 * Usage :
 *  ```html
 *  <ft-cell-tenant
 *    [name]="row.companyName"
 *    [email]="row.companyEmail"
 *    [seed]="row.tenantId" />
 *  ```
 */
@Component({
  selector: 'ft-cell-tenant',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FtAvatarComponent],
  template: `
    <span class="cell">
      <ft-avatar [name]="name" [seed]="seed ?? name" size="sm" />
      <span class="stack">
        <strong class="name">{{ name }}</strong>
        @if (email) {
          <span class="email">{{ email }}</span>
        }
      </span>
    </span>
  `,
  styles: [
    `
      :host {
        display: block;
        min-width: 0;
      }

      .cell {
        display: inline-flex;
        align-items: center;
        gap: var(--gap-sm);
        min-width: 0;
        max-width: 100%;
      }

      .stack {
        display: flex;
        flex-direction: column;
        gap: 0.1rem;
        min-width: 0;
        line-height: 1.25;
      }

      .name {
        color: var(--ft-text);
        font-weight: 600;
        font-size: 0.92rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .email {
        color: var(--ft-text-muted);
        font-size: 0.78rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
    `
  ]
})
export class FtCellTenantComponent {
  @Input({ required: true }) name = '';
  @Input() email: string | null = null;
  @Input() seed: string | null = null;
}
