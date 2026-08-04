import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  inject,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextarea } from 'primeng/inputtextarea';
import { ButtonModule } from 'primeng/button';
import { formatLocalDate } from '@core/utils/date.util';
import { ToastService } from '@core/services/toast.service';
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
    DialogModule,
    InputTextModule,
    InputNumberModule,
    CalendarModule,
    DropdownModule,
    InputTextarea,
    ButtonModule
  ],
  template: `
    <p-dialog
      header="Encaissement honoraires"
      [(visible)]="visible"
      [modal]="true"
      [style]="{ width: '480px' }"
      [draggable]="false"
      [closable]="true"
      (onHide)="onHide()"
      [contentStyle]="{ overflow: 'visible' }">
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
            <p-calendar
              inputId="hon-pay-date"
              [(ngModel)]="paymentDate"
              [showIcon]="true"
              dateFormat="dd/mm/yy"
              [maxDate]="maxDate"
              styleClass="w-full">
            </p-calendar>
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
            <p-dropdown
              inputId="hon-pay-method"
              [options]="paymentMethodOptions"
              [(ngModel)]="method"
              optionLabel="label"
              optionValue="value"
              placeholder="Sélectionnez un mode"
              styleClass="w-full"
              appendTo="body">
            </p-dropdown>
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
            <textarea pInputTextarea id="hon-pay-notes" [(ngModel)]="notes" rows="2" class="w-full" placeholder="Notes internes"></textarea>
          </div>
        </div>

        @if (errorMessage()) {
          <div class="error-message" role="alert">
            <i class="pi pi-exclamation-triangle"></i>
            {{ errorMessage() }}
          </div>
        }
      </div>

      <ng-template pTemplate="footer">
        <button pButton type="button" label="Annuler" class="p-button-text" (click)="close()" [disabled]="submitting()"></button>
        <button
          pButton
          type="button"
          label="Enregistrer l'encaissement"
          icon="pi pi-check"
          [disabled]="submitting()"
          (click)="submit()">
        </button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .modal-content { padding: 0.25rem 0; }
    .dialog-intro {
      font-size: 0.875rem;
      color: #64748b;
      margin: 0 0 1rem;
      padding: 0.75rem 1rem;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-left: 4px solid #3b82f6;
      border-radius: 0.5rem;
    }
    .remaining-info { font-weight: 600; color: #2563eb; }
    .form-fields { display: flex; flex-direction: column; gap: 0.875rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.35rem; }
    .form-group label { font-size: 0.875rem; font-weight: 500; color: #0f172a; }
    .required { color: #dc2626; }
    .field-hint { font-size: 0.75rem; color: #94a3b8; }
    .w-full { width: 100%; }
    .error-message {
      display: flex; align-items: center; gap: 0.5rem;
      margin-top: 1rem; padding: 0.75rem;
      background: #fef2f2; border: 1px solid #fecaca; border-radius: 0.5rem;
      color: #b91c1c; font-size: 0.875rem;
    }
  `]
})
export class HonorairesRecordPaymentDialogComponent implements OnChanges {
  private readonly api = inject(HonorairesService);
  private readonly toast = inject(ToastService);

  @Input() visible = false;
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

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.resetForm();
    }
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  onHide(): void {
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
