import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  computed,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { RadioButtonModule } from 'primeng/radiobutton';
import { InputTextarea } from 'primeng/inputtextarea';
import type { PlatformStorefrontProfileDto } from '@core/services/platform-storefront.service';
import { STOREFRONTS_FR } from './storefronts.i18n.fr';

type RejectReasonCode = 'logo' | 'name' | 'contact' | 'category' | 'other';

/**
 * Modal de refus de vitrine (Lot A4).
 *
 * 4 motifs prédéfinis (radio) + une option "Autre" + textarea complémentaire
 * obligatoire. Le motif final envoyé au backend est composé du libellé prédéfini
 * + détails saisis (séparés par " — ").
 */
@Component({
  selector: 'app-storefront-reject-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DialogModule, ButtonModule, RadioButtonModule, InputTextarea],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [closable]="!busy"
      [closeOnEscape]="!busy"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '32rem', maxWidth: '95vw' }"
      [header]="t('reject.title')"
    >
      @if (data) {
        <p class="intro">
          <strong>{{ data.displayName }}</strong>
          <br />
          {{ t('reject.intro') }}
        </p>

        <fieldset class="reasons">
          <legend>{{ t('reject.reason.label') }}</legend>
          @for (option of options; track option.value) {
            <label class="reason">
              <p-radioButton
                name="rejectReason"
                [value]="option.value"
                [(ngModel)]="reasonCodeModel"
                (ngModelChange)="reasonCodeSig.set($event)"
                [inputId]="'reject-reason-' + option.value"
                [disabled]="busy"
              />
              <span>{{ option.label }}</span>
            </label>
          }
        </fieldset>

        <div class="field">
          <label for="rejectDetails">{{ t('reject.details.label') }}</label>
          <textarea
            id="rejectDetails"
            pInputTextarea
            [(ngModel)]="detailsModel"
            (ngModelChange)="detailsSig.set($event)"
            [placeholder]="t('reject.details.placeholder')"
            rows="4"
            [disabled]="busy"
            class="w-full"
          ></textarea>
        </div>
      }

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy" (onClick)="onCancel()" />
        <p-button
          [label]="t('reject.confirmLabel')"
          icon="pi pi-times"
          severity="danger"
          [disabled]="busy || !canConfirm()"
          [loading]="busy"
          (onClick)="onConfirm()"
        />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      .intro {
        margin: 0 0 var(--gap-md);
        color: var(--ft-text-muted);
        font-size: 0.9rem;
        line-height: 1.5;
      }

      .intro strong {
        color: var(--ft-text);
      }

      .reasons {
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: var(--gap-sm) var(--gap-md);
        margin: 0 0 var(--gap-md);
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }

      .reasons legend {
        padding: 0 0.5rem;
        color: var(--ft-text-muted);
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        font-weight: 600;
      }

      .reason {
        display: flex;
        align-items: center;
        gap: 0.65rem;
        cursor: pointer;
        color: var(--ft-text);
        font-size: 0.9rem;
      }

      .field {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
      }

      .field label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }

      .w-full {
        width: 100%;
        background: var(--ft-surface-2);
        color: var(--ft-text);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: var(--gap-xs) var(--gap-sm);
        font-family: inherit;
        font-size: 0.9rem;
        resize: vertical;
        min-height: 5rem;
      }
    `
  ]
})
export class StorefrontRejectDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() data: PlatformStorefrontProfileDto | null = null;
  @Input() busy = false;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() confirmed = new EventEmitter<{ data: PlatformStorefrontProfileDto; reason: string }>();
  @Output() cancelled = new EventEmitter<void>();

  protected readonly options: { value: RejectReasonCode; label: string }[] = [
    { value: 'logo', label: STOREFRONTS_FR['reject.reason.logo'] },
    { value: 'name', label: STOREFRONTS_FR['reject.reason.name'] },
    { value: 'contact', label: STOREFRONTS_FR['reject.reason.contact'] },
    { value: 'category', label: STOREFRONTS_FR['reject.reason.category'] },
    { value: 'other', label: STOREFRONTS_FR['reject.reason.other'] }
  ];

  protected reasonCodeSig = signal<RejectReasonCode | null>(null);
  protected detailsSig = signal('');
  protected reasonCodeModel: RejectReasonCode | null = null;
  protected detailsModel = '';

  protected readonly canConfirm = computed(
    () => this.reasonCodeSig() !== null && this.detailsSig().trim().length >= 5
  );

  protected t(key: keyof typeof STOREFRONTS_FR): string {
    return STOREFRONTS_FR[key];
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && changes['visible'].currentValue === true) {
      this.resetState();
    }
  }

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.resetState();
    }
  }

  onCancel(): void {
    this.cancelled.emit();
    this.onVisibleChange(false);
  }

  onConfirm(): void {
    if (!this.canConfirm() || !this.data || this.busy) return;
    const code = this.reasonCodeSig()!;
    const codeLabel = this.options.find((o) => o.value === code)?.label ?? '';
    const composed = `${codeLabel} — ${this.detailsSig().trim()}`;
    this.confirmed.emit({ data: this.data, reason: composed });
  }

  private resetState(): void {
    this.reasonCodeModel = null;
    this.reasonCodeSig.set(null);
    this.detailsModel = '';
    this.detailsSig.set('');
  }
}
