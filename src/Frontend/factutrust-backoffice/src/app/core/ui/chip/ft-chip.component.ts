import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';

/**
 * Chip de filtre actif. Affiche une paire "label : valeur" avec un bouton de retrait.
 *
 * Usage :
 *  ```html
 *  <ft-chip label="Plan" value="Annuel" (remove)="onRemove('plan')" />
 *  ```
 */
@Component({
  selector: 'ft-chip',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="chip" role="listitem">
      <span class="chip__label">{{ label }}</span>
      <span class="chip__sep" aria-hidden="true">·</span>
      <span class="chip__value">{{ value }}</span>
      <button
        type="button"
        class="chip__remove"
        [attr.aria-label]="'Retirer le filtre ' + label"
        (click)="remove.emit()"
      >
        <span aria-hidden="true">✕</span>
      </button>
    </span>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
      }

      .chip {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.22rem 0.35rem 0.22rem 0.65rem;
        background: var(--ft-accent-surface);
        border: 1px solid var(--ft-accent-border);
        border-radius: var(--ft-radius-pill);
        color: var(--ft-text);
        font-size: 0.78rem;
        line-height: 1.2;
      }

      .chip__label {
        color: var(--ft-text-muted);
        text-transform: uppercase;
        font-size: 0.68rem;
        letter-spacing: 0.04em;
        font-weight: 600;
      }

      .chip__sep {
        color: var(--ft-text-subtle);
      }

      .chip__value {
        color: var(--ft-text);
        font-weight: 500;
      }

      .chip__remove {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 1.1rem;
        height: 1.1rem;
        border: none;
        background: transparent;
        color: var(--ft-text-muted);
        border-radius: var(--ft-radius-pill);
        cursor: pointer;
        font-size: 0.7rem;
        transition: background var(--duration-fast) var(--easing-standard),
          color var(--duration-fast) var(--easing-standard);
      }

      .chip__remove:hover {
        background: var(--ft-accent-muted);
        color: var(--ft-accent);
      }

      .chip__remove:focus-visible {
        outline: 2px solid var(--ft-accent);
        outline-offset: 1px;
      }
    `
  ]
})
export class FtChipComponent {
  @Input({ required: true }) label = '';
  @Input({ required: true }) value: string | number = '';
  @Output() remove = new EventEmitter<void>();
}
