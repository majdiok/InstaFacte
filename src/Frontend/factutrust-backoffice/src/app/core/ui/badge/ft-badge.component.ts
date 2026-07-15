import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

export type FtTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'accent';
export type FtBadgeSize = 'sm' | 'md';

/**
 * Badge sémantique colorisé pour étiqueter des statuts, tags, etc.
 *
 * Usage :
 *  ```html
 *  <ft-badge tone="success">Actif</ft-badge>
 *  <ft-badge tone="warning" [withDot]="true">Impayé</ft-badge>
 *  <ft-badge tone="info" size="sm">Essai</ft-badge>
 *  ```
 */
@Component({
  selector: 'ft-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="badge"
      [class.badge--sm]="size === 'sm'"
      [class.badge--success]="tone === 'success'"
      [class.badge--warning]="tone === 'warning'"
      [class.badge--danger]="tone === 'danger'"
      [class.badge--info]="tone === 'info'"
      [class.badge--neutral]="tone === 'neutral'"
      [class.badge--accent]="tone === 'accent'"
    >
      @if (withDot) {
        <span class="dot" aria-hidden="true"></span>
      }
      <ng-content />
    </span>
  `,
  styles: [
    `
      :host {
        display: inline-block;
      }

      .badge {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        padding: 0.22rem 0.6rem;
        font-size: 0.8rem;
        font-weight: 600;
        line-height: 1.1;
        border-radius: var(--ft-radius-pill);
        border: 1px solid transparent;
        white-space: nowrap;
      }

      .badge--sm {
        padding: 0.12rem 0.45rem;
        font-size: 0.72rem;
      }

      .dot {
        width: 0.45rem;
        height: 0.45rem;
        border-radius: var(--ft-radius-pill);
        background: currentColor;
      }

      .badge--success {
        color: var(--ft-success-text);
        background: var(--ft-success-surface);
        border-color: var(--ft-success-border);
      }

      .badge--warning {
        color: var(--ft-warning-text);
        background: var(--ft-warning-surface);
        border-color: var(--ft-warning-border);
      }

      .badge--danger {
        color: var(--ft-danger-text);
        background: var(--ft-danger-surface);
        border-color: var(--ft-danger-border);
      }

      .badge--info {
        color: var(--ft-info-text);
        background: var(--ft-info-surface);
        border-color: var(--ft-info-border);
      }

      .badge--neutral {
        color: var(--ft-neutral-text);
        background: var(--ft-neutral-surface);
        border-color: var(--ft-neutral-border);
      }

      .badge--accent {
        color: var(--ft-accent);
        background: var(--ft-accent-surface);
        border-color: var(--ft-accent-border);
      }
    `
  ],
})
export class FtBadgeComponent {
  @Input() tone: FtTone = 'neutral';
  @Input() size: FtBadgeSize = 'md';
  @Input() withDot = false;
}
