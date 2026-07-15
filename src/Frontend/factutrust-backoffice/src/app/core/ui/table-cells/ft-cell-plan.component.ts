import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FtBadgeComponent } from '../badge/ft-badge.component';
import type { FtTone } from '../badge/ft-badge.component';

/**
 * Cellule "Plan" : badge avec icône :
 *  - couronne (👑 pi-crown) pour Annual
 *  - éclair pour Monthly
 *  - étoile pour Free / défaut
 *
 * Le tone du badge est déterminé par le code plan :
 *  - Annual → accent (bleu vif)
 *  - Monthly → info (bleu doux)
 *  - Free → neutral
 *
 * Usage :
 *  ```html
 *  <ft-cell-plan [planCode]="row.subscriptionPlan"
 *                [planDisplay]="row.subscriptionPlanDisplay" />
 *  ```
 */
@Component({
  selector: 'ft-cell-plan',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FtBadgeComponent],
  template: `
    @if (planCode || planDisplay) {
      <ft-badge [tone]="resolveTone()">
        <i class="pi {{ resolveIcon() }} icon" aria-hidden="true"></i>
        {{ planDisplay ?? planCode }}
      </ft-badge>
    } @else {
      <span class="cell-empty">—</span>
    }
  `,
  styles: [
    `
      :host {
        display: inline-block;
      }

      .icon {
        font-size: 0.78rem;
        margin-right: 0.1rem;
      }

      .cell-empty {
        color: var(--ft-text-subtle);
      }
    `
  ]
})
export class FtCellPlanComponent {
  @Input() planCode: string | null = null;
  @Input() planDisplay: string | null = null;

  resolveTone(): FtTone {
    switch ((this.planCode ?? '').toLowerCase()) {
      case 'annual':
        return 'accent';
      case 'monthly':
        return 'info';
      case 'free':
        return 'neutral';
      default:
        return 'neutral';
    }
  }

  resolveIcon(): string {
    switch ((this.planCode ?? '').toLowerCase()) {
      case 'annual':
        return 'pi-crown';
      case 'monthly':
        return 'pi-bolt';
      case 'free':
        return 'pi-star';
      default:
        return 'pi-tag';
    }
  }
}
