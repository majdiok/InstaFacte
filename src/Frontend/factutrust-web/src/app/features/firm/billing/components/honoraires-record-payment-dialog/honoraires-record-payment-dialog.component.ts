import {
  Component,
  EventEmitter,
  HostListener,
  Input,
  OnDestroy,
  Output,
  inject,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { Textarea } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { OverlayOptions } from 'primeng/api';
import { formatLocalDate } from '@core/utils/date.util';
import { ToastService } from '@core/services/toast.service';
import { DrawerOverlayService } from '@core/services/drawer-overlay.service';
import {
  HonorairesService,
  RecordHonorairesPayment
} from '../../services/honoraires.service';
import {
  suggestHonorairesPaymentAmounts,
  validateHonorairesPaymentDraft
} from '../../models/honoraires-invoice-status';

/** Aligned with FactuTrust.Domain.Enums.PaymentMethod (no traite for honoraires). */
const PAYMENT_METHOD_OPTIONS = [
  { label: 'Espèces', value: 0 },
  { label: 'Virement bancaire', value: 1 },
  { label: 'Chèque', value: 2 },
  { label: 'Carte bancaire', value: 3 },
  { label: 'Paiement mobile', value: 4 },
  { label: 'Autre', value: 99 }
];

@Component({
  selector: 'app-honoraires-record-payment-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    InputTextModule,
    InputNumberModule,
    DatePickerModule,
    SelectModule,
    Textarea,
    ButtonModule
  ],
  template: `
    <ng-template #formContent>
      <div class="modal-content">
        @if (invoiceNumber) {
          <p class="dialog-intro">
            Facture {{ invoiceNumber }} — Total TTC : {{ totalAmount | number:'1.3-3' }} {{ currency }}
            @if (amountDue >= 0) {
              <br />
              <span class="remaining-info">Reste dû : {{ amountDue | number:'1.3-3' }} {{ currency }}</span>
            }
          </p>
        }

        <div class="form-fields">
          <div class="form-group">
            <label for="hon-pay-date">Date de paiement <span class="required">*</span></label>
            <p-datepicker
              inputId="hon-pay-date"
              [(ngModel)]="paymentDate"
              [showIcon]="true"
              dateFormat="dd/mm/yy"
              [maxDate]="maxDate"
              appendTo="body"
              [baseZIndex]="drawerPrimeBaseZIndex"
              styleClass="w-full">
            </p-datepicker>
          </div>

          <div class="form-group">
            <label for="hon-pay-amount">Montant net encaissé ({{ currency }}) <span class="required">*</span></label>
            <p-inputNumber
              inputId="hon-pay-amount"
              [(ngModel)]="amount"
              mode="decimal"
              [min]="0"
              [maxFractionDigits]="3"
              [minFractionDigits]="3"
              styleClass="w-full">
            </p-inputNumber>
            <small class="field-hint">Net reçu (hors retenue). Net + RS ≤ reste dû.</small>
          </div>

          <div class="form-group">
            <label for="hon-pay-rs">Retenue à la source ({{ currency }})</label>
            <p-inputNumber
              inputId="hon-pay-rs"
              [(ngModel)]="clientWithholdingAmount"
              mode="decimal"
              [min]="0"
              [maxFractionDigits]="3"
              [minFractionDigits]="3"
              styleClass="w-full">
            </p-inputNumber>
          </div>

          <div class="form-group">
            <label for="hon-pay-method">Mode de paiement <span class="required">*</span></label>
            <p-select
              inputId="hon-pay-method"
              [options]="paymentMethodOptions"
              [(ngModel)]="method"
              optionLabel="label"
              optionValue="value"
              placeholder="Sélectionnez un mode"
              styleClass="w-full"
              appendTo="body"
              [overlayOptions]="drawerOverlayOptions">
            </p-select>
          </div>

          <div class="form-group">
            <label for="hon-pay-ref">Référence</label>
            <input pInputText id="hon-pay-ref" [(ngModel)]="reference" class="w-full" placeholder="N° chèque, virement…" />
          </div>

          <div class="form-group">
            <label for="hon-pay-bank">Compte / banque</label>
            <input pInputText id="hon-pay-bank" [(ngModel)]="bankAccountLabel" class="w-full" placeholder="Optionnel" />
          </div>

          <div class="form-group">
            <label for="hon-pay-notes">Notes</label>
            <textarea pTextarea id="hon-pay-notes" [(ngModel)]="notes" rows="2" class="w-full" placeholder="Notes internes"></textarea>
          </div>
        </div>

        @if (errorMessage()) {
          <div class="error-message" role="alert">
            <i class="pi pi-exclamation-triangle"></i>
            {{ errorMessage() }}
          </div>
        }
      </div>
    </ng-template>

    <ng-template #footerActions>
      <button
        pButton
        type="button"
        label="Annuler"
        class="p-button-text"
        (click)="close()"
        [disabled]="submitting()">
      </button>
      <button
        pButton
        type="button"
        label="Enregistrer l'encaissement"
        icon="pi pi-check"
        [disabled]="submitting()"
        (click)="submit()">
      </button>
    </ng-template>

    @if (visible) {
      <div class="panel-overlay" (click)="close()" role="presentation">
        <div
          class="panel-content"
          (click)="$event.stopPropagation()"
          role="dialog"
          aria-modal="true"
          aria-labelledby="hon-pay-panel-title">
          <div class="panel-header">
            <h2 class="panel-title" id="hon-pay-panel-title">Encaissement honoraires</h2>
            <button
              type="button"
              class="panel-close"
              (click)="close()"
              aria-label="Fermer le panneau">
              <i class="pi pi-times"></i>
            </button>
          </div>
          <div class="panel-body">
            <ng-container *ngTemplateOutlet="formContent"></ng-container>
          </div>
          <div class="panel-footer">
            <ng-container *ngTemplateOutlet="footerActions"></ng-container>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .modal-content {
      padding: 0;
    }

    .dialog-intro {
      font-size: var(--font-size-sm, 0.875rem);
      color: var(--color-text-secondary, #64748b);
      margin: 0 0 var(--spacing-4, 1rem);
      padding: var(--spacing-3, 0.75rem) var(--spacing-4, 1rem);
      background: var(--color-background-elevated, #f8fafc);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-left: 4px solid var(--color-primary-500, #3b82f6);
      border-radius: var(--radius-xl, 0.5rem);
    }

    .remaining-info {
      font-weight: var(--font-weight-semibold, 600);
      color: var(--color-primary-600, #2563eb);
    }

    .form-fields {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-4, 0.875rem);
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2, 0.35rem);
    }

    .form-group label {
      font-size: var(--font-size-sm, 0.875rem);
      font-weight: var(--font-weight-medium, 500);
      color: var(--color-text-primary, #0f172a);
    }

    .required {
      color: var(--color-error-600, #dc2626);
    }

    .field-hint {
      font-size: var(--font-size-xs, 0.75rem);
      color: var(--color-text-tertiary, #94a3b8);
    }

    .w-full {
      width: 100%;
    }

    .error-message {
      display: flex;
      align-items: center;
      gap: var(--spacing-2, 0.5rem);
      margin-top: var(--spacing-4, 1rem);
      padding: var(--spacing-3, 0.75rem);
      background: var(--color-error-50, #fef2f2);
      border: 1px solid var(--color-error-200, #fecaca);
      border-radius: var(--radius-md, 0.5rem);
      color: var(--color-error-700, #b91c1c);
      font-size: var(--font-size-sm, 0.875rem);
    }

    .panel-overlay {
      position: fixed;
      inset: 0;
      z-index: var(--z-drawer-overlay);
      display: flex;
      justify-content: flex-end;
      align-items: stretch;
      background: rgba(15, 23, 42, 0.25);
      backdrop-filter: blur(4px);
      animation: panelFadeIn 200ms ease-out;
    }

    .panel-content {
      position: relative;
      z-index: var(--z-drawer-panel);
      width: min(520px, 100vw);
      max-height: 100dvh;
      height: 100%;
      background: var(--color-white, #fff);
      box-shadow: -8px 0 24px rgba(0, 0, 0, 0.12);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      animation: panelSlideInRight 250ms cubic-bezier(0.4, 0, 0.2, 1);
    }

    .panel-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: var(--spacing-5, 1.25rem);
      border-bottom: 1px solid var(--color-border-subtle, #e2e8f0);
      flex-shrink: 0;
    }

    .panel-title {
      font-size: var(--font-size-lg, 1.125rem);
      font-weight: var(--font-weight-bold, 700);
      margin: 0;
      color: var(--color-text-primary, #0f172a);
    }

    .panel-close {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border: none;
      border-radius: var(--radius-lg, 0.5rem);
      background: var(--color-neutral-100, #f1f5f9);
      color: var(--color-text-tertiary, #94a3b8);
      cursor: pointer;
      transition: all 200ms ease;
    }

    .panel-close:hover {
      background: var(--color-neutral-200, #e2e8f0);
      color: var(--color-text-primary, #0f172a);
    }

    .panel-body {
      flex: 1;
      overflow-y: auto;
      padding: var(--spacing-5, 1.25rem);
      min-height: 0;
    }

    .panel-footer {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3, 0.75rem);
      padding: var(--spacing-4, 1rem) var(--spacing-5, 1.25rem);
      padding-bottom: calc(var(--spacing-4, 1rem) + env(safe-area-inset-bottom, 0px));
      border-top: 1px solid var(--color-border-subtle, #e2e8f0);
      background: var(--color-background-subtle, #f8fafc);
      flex-shrink: 0;
    }

    @keyframes panelFadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes panelSlideInRight {
      from { transform: translateX(100%); }
      to { transform: translateX(0); }
    }

    :host ::ng-deep {
      .p-datepicker,
      .p-select,
      .p-inputnumber {
        width: 100%;
      }
    }
  `]
})
export class HonorairesRecordPaymentDialogComponent implements OnDestroy {
  private readonly api = inject(HonorairesService);
  private readonly toast = inject(ToastService);
  private readonly drawerOverlay = inject(DrawerOverlayService);

  readonly drawerOverlayOptions: OverlayOptions = { baseZIndex: 1200 };
  readonly drawerPrimeBaseZIndex = 1200;

  @Input() set visible(value: boolean) {
    const wasVisible = this._visible;
    this._visible = value;
    if (value && !wasVisible) {
      this.drawerOverlay.registerOpen();
      this.resetForm();
    } else if (!value && wasVisible) {
      this.drawerOverlay.registerClose();
    }
  }
  get visible(): boolean {
    return this._visible;
  }
  private _visible = false;

  @Input() invoiceId: string | null = null;
  @Input() invoiceNumber = '';
  @Input() currency = 'TND';
  @Input() totalAmount = 0;
  @Input() amountDue = 0;
  @Input() invoiceWithholdingAmount = 0;
  @Input() paymentsClientWithholdingTotal = 0;
  @Input() defaultBankAccountLabel = '';

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() paymentRecorded = new EventEmitter<void>();

  readonly paymentMethodOptions = PAYMENT_METHOD_OPTIONS;
  readonly maxDate = (() => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d;
  })();

  paymentDate: Date | null = new Date();
  amount = 0;
  clientWithholdingAmount = 0;
  method: number = 1;
  reference = '';
  notes = '';
  bankAccountLabel = '';

  submitting = signal(false);
  errorMessage = signal<string | null>(null);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.visible) {
      this.close();
    }
  }

  ngOnDestroy(): void {
    if (this._visible) {
      this.drawerOverlay.registerClose();
      this._visible = false;
    }
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  submit(): void {
    if (!this.invoiceId) return;
    this.errorMessage.set(null);

    const paymentDate = this.paymentDate ? formatLocalDate(this.paymentDate) : '';
    const validationError = validateHonorairesPaymentDraft({
      amount: this.amount || 0,
      clientWithholdingAmount: this.clientWithholdingAmount || 0,
      amountDue: this.amountDue,
      paymentDate,
      method: this.method
    });
    if (validationError) {
      this.errorMessage.set(validationError);
      return;
    }

    const dto: RecordHonorairesPayment = {
      paymentDate,
      amount: this.amount || 0,
      clientWithholdingAmount: this.clientWithholdingAmount || 0,
      method: this.method,
      reference: this.reference || undefined,
      notes: this.notes || undefined,
      bankAccountLabel: this.bankAccountLabel || undefined
    };

    this.submitting.set(true);
    this.api.recordPayment(this.invoiceId, dto).subscribe({
      next: () => {
        this.submitting.set(false);
        this.toast.add({ severity: 'success', summary: 'OK', detail: 'Encaissement enregistré' });
        this.paymentRecorded.emit();
        this.close();
      },
      error: (err) => {
        this.submitting.set(false);
        const detail =
          err?.error?.error?.message ||
          err?.error?.message ||
          'Encaissement refusé';
        this.errorMessage.set(detail);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail });
      }
    });
  }

  private resetForm(): void {
    this.errorMessage.set(null);
    this.paymentDate = new Date();
    this.method = 1;
    this.reference = '';
    this.notes = '';
    this.bankAccountLabel = this.defaultBankAccountLabel || '';
    const suggested = suggestHonorairesPaymentAmounts({
      amountDue: this.amountDue,
      invoiceWithholdingAmount: this.invoiceWithholdingAmount,
      paymentsClientWithholdingTotal: this.paymentsClientWithholdingTotal
    });
    this.amount = suggested.amount;
    this.clientWithholdingAmount = suggested.clientWithholdingAmount;
  }
}
