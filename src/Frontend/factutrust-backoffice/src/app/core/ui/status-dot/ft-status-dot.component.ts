import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import type { FtTone } from '../badge/ft-badge.component';

/**
 * Indicateur de statut : un point coloré 8px + label.
 *
 * Usage :
 *  ```html
 *  <ft-status-dot tone="success" label="Actif" />
 *  <ft-status-dot tone="warning" label="Impayé" />
 *  ```
 */
@Component({
  selector: 'ft-status-dot',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="status"
      [attr.aria-label]="ariaLabel ?? ('Statut : ' + label)"
      [class.status--success]="tone === 'success'"
      [class.status--warning]="tone === 'warning'"
      [class.status--danger]="tone === 'danger'"
      [class.status--info]="tone === 'info'"
      [class.status--neutral]="tone === 'neutral'"
      [class.status--accent]="tone === 'accent'"
    >
      <span class="dot" aria-hidden="true"></span>
      <span class="label">{{ label }}</span>
    </span>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
      }

      .status {
        display: inline-flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 0.875rem;
        font-weight: 500;
        line-height: 1.2;
      }

      .dot {
        width: 0.5rem;
        height: 0.5rem;
        border-radius: var(--ft-radius-pill);
        background: currentColor;
        flex-shrink: 0;
      }

      .status--success {
        color: var(--ft-success-text);
      }
      .status--warning {
        color: var(--ft-warning-text);
      }
      .status--danger {
        color: var(--ft-danger-text);
      }
      .status--info {
        color: var(--ft-info-text);
      }
      .status--neutral {
        color: var(--ft-neutral-text);
      }
      .status--accent {
        color: var(--ft-accent);
      }

      .label {
        color: var(--ft-text);
      }
    `
  ]
})
export class FtStatusDotComponent {
  @Input({ required: true }) label = '';
  @Input() tone: FtTone = 'neutral';
  @Input() ariaLabel: string | null = null;
}
