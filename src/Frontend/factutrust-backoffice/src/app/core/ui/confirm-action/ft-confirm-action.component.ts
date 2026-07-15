import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';

export type FtConfirmVariant = 'soft' | 'destructive';

/**
 * Dialogue de confirmation, en deux modes :
 *  - `soft` : titre + description + boutons Annuler / Confirmer (couleur accent).
 *  - `destructive` : titre rouge orangé + description + zone de conséquences via slot,
 *    + input texte requis ("Tapez le mot magique pour confirmer") + bouton confirmer
 *    désactivé tant que la saisie ne correspond pas exactement à `confirmKeyword`.
 *
 * Usage :
 *  ```html
 *  <ft-confirm-action
 *    [(visible)]="open"
 *    title="Suspendre l'entreprise"
 *    description="Toutes les sessions actives seront révoquées."
 *    variant="destructive"
 *    confirmKeyword="SUSPENDRE"
 *    confirmLabel="Suspendre maintenant"
 *    [busy]="busy()"
 *    (confirmed)="onSuspendConfirmed()"
 *  >
 *    <ul>
 *      <li>Les utilisateurs ne pourront plus se connecter.</li>
 *      <li>Aucune donnée n'est supprimée.</li>
 *    </ul>
 *  </ft-confirm-action>
 *  ```
 */
@Component({
  selector: 'ft-confirm-action',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DialogModule, ButtonModule, InputTextModule],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [closable]="!busy"
      [closeOnEscape]="!busy"
      [dismissableMask]="false"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '32rem', maxWidth: '95vw' }"
      [header]="title"
      styleClass="ft-confirm-host"
    >
      @if (description) {
        <p class="confirm__desc">{{ description }}</p>
      }
      <div class="confirm__slot">
        <ng-content />
      </div>
      @if (variant === 'destructive') {
        <div class="confirm__keyword">
          <label [attr.for]="inputId">
            Pour confirmer, tapez <code class="kw">{{ confirmKeyword }}</code>
          </label>
          <input
            type="text"
            pInputText
            [id]="inputId"
            [(ngModel)]="typed"
            (ngModelChange)="onTypedChange($event)"
            [disabled]="busy"
            autocomplete="off"
            spellcheck="false"
          />
        </div>
      }
      <ng-template pTemplate="footer">
        <p-button
          label="Annuler"
          [text]="true"
          severity="secondary"
          [disabled]="busy"
          (onClick)="onCancel()"
        />
        <p-button
          [label]="confirmLabel"
          [icon]="busy ? 'pi pi-spin pi-spinner' : confirmIcon"
          [severity]="variant === 'destructive' ? 'danger' : 'primary'"
          [disabled]="busy || !canConfirm()"
          (onClick)="onConfirm()"
        />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      .confirm__desc {
        margin: 0 0 var(--gap-md);
        color: var(--ft-text-muted);
        font-size: 0.9rem;
        line-height: 1.55;
      }

      .confirm__slot {
        margin-bottom: var(--gap-md);
        color: var(--ft-text);
        font-size: 0.9rem;
      }

      .confirm__slot:empty {
        display: none;
      }

      .confirm__keyword {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        padding: var(--gap-sm) var(--gap-md);
        border-radius: var(--ft-radius);
        background: var(--ft-danger-surface);
        border: 1px solid var(--ft-danger-border);
      }

      .confirm__keyword label {
        font-size: 0.85rem;
        color: var(--ft-text);
      }

      .kw {
        background: var(--ft-surface-2);
        border: 1px solid var(--ft-danger-border);
        border-radius: var(--ft-radius-sm);
        padding: 0.05rem 0.35rem;
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.85em;
        color: var(--ft-danger-text);
      }

      :host ::ng-deep .ft-confirm-host .p-dialog-header {
        padding-bottom: var(--gap-xs);
      }
    `
  ]
})
export class FtConfirmActionComponent {
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Input({ required: true }) title = '';
  @Input() description: string | null = null;
  @Input() variant: FtConfirmVariant = 'soft';
  @Input() confirmLabel = 'Confirmer';
  @Input() confirmIcon = 'pi pi-check';
  @Input() confirmKeyword = 'CONFIRMER';
  @Input() busy = false;

  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  protected readonly inputId = 'ft-confirm-' + Math.random().toString(36).slice(2, 8);
  private readonly typedSig = signal('');
  protected typed = '';

  protected readonly canConfirm = computed(() => {
    if (this.variant !== 'destructive') {
      return true;
    }
    return this.typedSig().trim() === this.confirmKeyword;
  });

  onTypedChange(value: string): void {
    this.typed = value;
    this.typedSig.set(value);
  }

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.typed = '';
      this.typedSig.set('');
    }
  }

  onConfirm(): void {
    if (this.canConfirm() && !this.busy) {
      this.confirmed.emit();
    }
  }

  onCancel(): void {
    this.cancelled.emit();
    this.onVisibleChange(false);
  }
}
