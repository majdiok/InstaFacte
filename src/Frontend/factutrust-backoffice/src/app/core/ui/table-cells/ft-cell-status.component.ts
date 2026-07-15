import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FtStatusDotComponent } from '../status-dot/ft-status-dot.component';
import type { FtTone } from '../badge/ft-badge.component';

/**
 * Cellule de statut : `<ft-status-dot>` avec mapping Subscription/Tenant.
 *
 * Le mapping `subscriptionStatus` (Active / Trial / Expired / Cancelled / PastDue / Suspended)
 * vers tone se fait ici pour centraliser la logique. La table appelante n'a qu'à
 * passer la chaîne brute renvoyée par l'API.
 *
 * Usage :
 *  ```html
 *  <ft-cell-status [status]="row.subscriptionStatus"
 *                  [statusDisplay]="row.subscriptionStatusDisplay" />
 *  ```
 */
@Component({
  selector: 'ft-cell-status',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FtStatusDotComponent],
  template: `
    @if (status) {
      <ft-status-dot [tone]="resolveTone()" [label]="statusDisplay ?? status" />
    } @else {
      <span class="cell-empty">—</span>
    }
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .cell-empty {
        color: var(--ft-text-subtle);
      }
    `
  ]
})
export class FtCellStatusComponent {
  /** Code statut brut (ex: "Active", "Trial", "PastDue", "Suspended", "Cancelled", "Expired"). */
  @Input() status: string | null = null;
  /** Libellé localisé (français) — affiché tel quel si présent. */
  @Input() statusDisplay: string | null = null;
  /** Override forcé du tone (utile pour les statuts custom). */
  @Input() tone: FtTone | null = null;

  resolveTone(): FtTone {
    if (this.tone) {
      return this.tone;
    }
    switch ((this.status ?? '').toLowerCase()) {
      case 'active':
        return 'success';
      case 'trial':
        return 'info';
      case 'pastdue':
      case 'past_due':
        return 'warning';
      case 'suspended':
        return 'danger';
      case 'cancelled':
      case 'canceled':
        return 'neutral';
      case 'expired':
        return 'neutral';
      default:
        return 'neutral';
    }
  }
}
