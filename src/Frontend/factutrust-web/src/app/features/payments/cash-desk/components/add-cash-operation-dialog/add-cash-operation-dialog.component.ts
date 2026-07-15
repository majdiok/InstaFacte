import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ButtonComponent } from '@shared/components/button/button.component';
import { ToastService } from '@core/services/toast.service';
import { formatLocalDate } from '@core/utils/date.util';
import {
  CashDeskService,
  CashOperationType,
  CreateCashOperationPayload,
  CashOperationListItem,
  CASH_EXPENSE_CATEGORY_GROUPS,
  CASH_REVENUE_CATEGORY_GROUPS,
  getCashExpenseCategoryLabel,
  getCashRevenueCategoryLabel
} from '@core/services/cash-desk.service';

const PAYMENT_METHOD_OPTIONS: { label: string; value: number; icon?: string }[] = [
  { label: 'Espèces', value: 0, icon: 'pi pi-money-bill' },
  { label: 'Virement bancaire', value: 1, icon: 'pi pi-building' },
  { label: 'Chèque', value: 2, icon: 'pi pi-file' },
  { label: 'Carte bancaire', value: 3, icon: 'pi pi-credit-card' },
  { label: 'Paiement mobile', value: 4, icon: 'pi pi-mobile' },
  { label: 'Autre', value: 99, icon: 'pi pi-question' }
];

@Component({
  selector: 'app-add-cash-operation-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    InputNumberModule,
    CalendarModule,
    DropdownModule,
    InputTextModule,
    InputTextareaModule,
    ButtonComponent
  ],
  template: `
    <p-dialog
      header="Enregistrer l'opération"
      [(visible)]="visible"
      [modal]="true"
      [style]="{ width: '580px' }"
      [draggable]="false"
      [closable]="true"
      (onHide)="onHide()"
      [contentStyle]="{ overflow: 'visible', padding: 0 }">

      <div class="dialog-content">
        @if (errorMessage()) {
          <div class="error-message" role="alert">
            <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
            <span>{{ errorMessage() }}</span>
          </div>
        }

        <!-- Operation Type Toggle -->
        <div class="form-group">
          <label>Type de paiement <span class="required">*</span></label>
          <div class="type-toggle" role="radiogroup" aria-label="Type d'opération">
            <div
              class="type-card"
              [class.selected]="operationType === CashOperationType.Debit"
              (click)="onTypeChange(CashOperationType.Debit)"
              (keydown.enter)="onTypeChange(CashOperationType.Debit)"
              (keydown.space)="onTypeChange(CashOperationType.Debit); $event.preventDefault()"
              tabindex="0"
              role="radio"
              [attr.aria-checked]="operationType === CashOperationType.Debit">
              <span class="type-label">Débit</span>
            </div>
            <div
              class="type-card type-card-credit"
              [class.selected]="operationType === CashOperationType.Credit"
              (click)="onTypeChange(CashOperationType.Credit)"
              (keydown.enter)="onTypeChange(CashOperationType.Credit)"
              (keydown.space)="onTypeChange(CashOperationType.Credit); $event.preventDefault()"
              tabindex="0"
              role="radio"
              [attr.aria-checked]="operationType === CashOperationType.Credit">
              <span class="type-label">Crédit</span>
            </div>
          </div>
        </div>

        <!-- Category (changes based on operation type) -->
        <div class="form-group">
          <label [for]="operationType === CashOperationType.Debit ? 'expenseCategory' : 'revenueCategory'">
            {{ operationType === CashOperationType.Debit ? 'Catégorie de dépense' : 'Catégorie de revenu' }}
            <span class="required">*</span>
          </label>
          @if (operationType === CashOperationType.Debit) {
            <p-dropdown
              inputId="expenseCategory"
              [options]="expenseCategoryGroups"
              [(ngModel)]="selectedExpenseCategory"
              [group]="true"
              optionLabel="label"
              optionGroupLabel="label"
              optionGroupChildren="items"
              optionValue="value"
              placeholder="Sélectionnez une catégorie"
              [filter]="true"
              filterBy="label"
              [showClear]="false"
              filterPlaceholder="Rechercher…"
              appendTo="body"
              styleClass="w-full category-dropdown">
              <ng-template let-item pTemplate="item">
                <span [class]="item.icon + ' category-option-icon'" aria-hidden="true"></span>
                <span class="category-option-label">{{ item.label }}</span>
              </ng-template>
            </p-dropdown>
          } @else {
            <p-dropdown
              inputId="revenueCategory"
              [options]="revenueCategoryGroups"
              [(ngModel)]="selectedRevenueCategory"
              [group]="true"
              optionLabel="label"
              optionGroupLabel="label"
              optionGroupChildren="items"
              optionValue="value"
              placeholder="Sélectionnez une catégorie"
              [filter]="false"
              [showClear]="false"
              appendTo="body"
              styleClass="w-full category-dropdown">
              <ng-template let-item pTemplate="item">
                <span [class]="item.icon + ' category-option-icon'" aria-hidden="true"></span>
                <span class="category-option-label">{{ item.label }}</span>
              </ng-template>
            </p-dropdown>
          }
        </div>

        <!-- Payment Method -->
        <div class="form-group">
          <label for="method">Méthode de paiement <span class="required">*</span></label>
          <div class="method-toggle" role="radiogroup" aria-label="Méthode de paiement">
            @for (method of paymentMethodOptions; track method.value) {
              <div
                class="method-card"
                [class.selected]="selectedMethod === method.value"
                (click)="selectedMethod = method.value"
                (keydown.enter)="selectedMethod = method.value"
                (keydown.space)="selectedMethod = method.value; $event.preventDefault()"
                tabindex="0"
                role="radio"
                [attr.aria-checked]="selectedMethod === method.value"
                [attr.aria-label]="method.label">
                <i [class]="method.icon" aria-hidden="true"></i>
                <span class="method-label">{{ method.label }}</span>
              </div>
            }
          </div>
        </div>

        <!-- Amount + Date row -->
        <div class="form-row">
          <div class="form-group">
            <label for="amount">Montant <span class="required">*</span></label>
            <div class="amount-input-wrap">
              <span class="amount-currency">TND</span>
              <p-inputNumber
                id="amount"
                [(ngModel)]="amount"
                [min]="0.001"
                [maxFractionDigits]="3"
                [minFractionDigits]="3"
                mode="decimal"
                placeholder="0,000"
                styleClass="w-full">
              </p-inputNumber>
            </div>
          </div>

          <div class="form-group">
            <label for="operationDate">Date de règlement</label>
            <p-calendar
              id="operationDate"
              [(ngModel)]="operationDate"
              [showIcon]="true"
              dateFormat="dd/mm/yy"
              [maxDate]="maxDate"
              placeholder="Sélectionnez la date"
              appendTo="body"
              styleClass="w-full">
            </p-calendar>
          </div>
        </div>

        <!-- Label -->
        <div class="form-group">
          <label for="label">Libellé <span class="required">*</span></label>
          <input
            id="label"
            pInputText
            [(ngModel)]="label"
            placeholder="Ex: Achat fournitures"
            class="w-full"
            [maxLength]="500"
            aria-label="Libellé de l'opération" />
        </div>

        <!-- Reference -->
        <div class="form-group">
          <label for="reference">Référence <span class="optional-tag">(optionnel)</span></label>
          <input
            id="reference"
            pInputText
            [(ngModel)]="reference"
            placeholder="Ex: N° chèque, virement n°…"
            class="w-full"
            [maxLength]="100"
            aria-label="Référence" />
        </div>

        <!-- Notes -->
        <div class="form-group">
          <label for="notes">Note <span class="optional-tag">(optionnel)</span></label>
          <textarea
            id="notes"
            pInputTextarea
            [(ngModel)]="notes"
            placeholder="Notes internes"
            [rows]="3"
            class="w-full"
            [maxLength]="500"
            aria-label="Notes">
          </textarea>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <div class="dialog-footer">
          <app-button
            variant="outline"
            [disabled]="submitting()"
            (click)="onHide()"
            ariaLabel="Annuler">
            Annuler
          </app-button>
          <app-button
            variant="primary"
            [disabled]="submitting()"
            icon="pi pi-check"
            iconPos="right"
            (click)="submit()"
            ariaLabel="Valider l'opération">
            {{ submitting() ? 'Enregistrement...' : 'Valider' }}
          </app-button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .dialog-content {
      padding: var(--spacing-5) var(--spacing-6) var(--spacing-4);
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      margin-bottom: var(--spacing-3);
    }

    .form-group:last-child {
      margin-bottom: 0;
    }

    .form-group label {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      color: var(--color-text-primary);
    }

    .required { color: var(--color-error-600); }

    .optional-tag {
      font-weight: var(--font-weight-normal);
      color: var(--color-text-tertiary);
      font-size: var(--font-size-xs);
    }

    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--spacing-4);
    }

    .amount-input-wrap {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
    }

    .amount-currency {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-secondary);
      font-family: 'JetBrains Mono', monospace;
      flex-shrink: 0;
    }

    /* Type toggle */
    .type-toggle {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--spacing-3);
    }

    .type-card {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-2);
      padding: var(--spacing-3) var(--spacing-4);
      border: 2px solid var(--color-border-subtle);
      border-radius: var(--radius-lg);
      cursor: pointer;
      transition: all var(--transition-fast);
      background: var(--color-background-elevated);
      user-select: none;
    }

    .type-card:hover {
      border-color: var(--color-primary-300);
      background: var(--color-primary-50);
    }

    .type-card:focus-visible {
      outline: 2px solid var(--color-primary-500);
      outline-offset: 2px;
    }

    .type-card.selected {
      border-color: var(--color-primary-500);
      background: var(--color-primary-50);
      box-shadow: 0 0 0 1px var(--color-primary-500);
    }

    .type-label {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
    }

    .type-card.selected .type-label {
      color: var(--color-primary-700);
    }

    /* Method toggle */
    .method-toggle {
      display: flex;
      flex-wrap: wrap;
      gap: var(--spacing-2);
    }

    .method-card {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-3);
      border: 1.5px solid var(--color-border-subtle);
      border-radius: var(--radius-md);
      cursor: pointer;
      transition: all var(--transition-fast);
      background: var(--color-background-elevated);
      font-size: var(--font-size-sm);
      user-select: none;
    }

    .method-card i {
      font-size: var(--font-size-sm);
      color: var(--color-text-tertiary);
    }

    .method-card:hover {
      border-color: var(--color-primary-300);
    }

    .method-card:focus-visible {
      outline: 2px solid var(--color-primary-500);
      outline-offset: 2px;
    }

    .method-card.selected {
      border-color: var(--color-primary-500);
      background: var(--color-primary-50);
    }

    .method-card.selected i {
      color: var(--color-primary-600);
    }

    .method-card.selected .method-label {
      color: var(--color-primary-700);
      font-weight: var(--font-weight-medium);
    }

    .method-label {
      color: var(--color-text-secondary);
    }

    .error-message {
      display: flex;
      align-items: flex-start;
      gap: var(--spacing-2);
      margin-bottom: var(--spacing-3);
      padding: var(--spacing-3);
      background: var(--color-error-50);
      border: 1px solid var(--color-error-200);
      border-radius: var(--radius-md);
      color: var(--color-error-700);
      font-size: var(--font-size-sm);
    }

    .error-message i {
      flex-shrink: 0;
      margin-top: 0.1rem;
    }

    :host ::ng-deep .category-dropdown .p-dropdown-label {
      display: flex;
      align-items: flex-start;
      gap: var(--spacing-2);
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .category-option-icon {
      flex-shrink: 0;
      margin-top: 0.1rem;
      opacity: 0.85;
    }

    .category-option-label {
      min-width: 0;
      line-height: 1.35;
    }

    .w-full { width: 100%; }

    .dialog-footer {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3);
    }

    @media (max-width: 640px) {
      .form-row {
        grid-template-columns: 1fr;
      }
      .dialog-content {
        padding: var(--spacing-4);
      }
    }

    :host ::ng-deep {
      .p-dialog {
        border-radius: var(--radius-xl);
        box-shadow: var(--shadow-xl);
      }
      .p-dialog-content {
        border-radius: 0;
        padding: 0;
      }
      .p-dialog-header {
        padding: var(--spacing-4) var(--spacing-6);
        border-bottom: 1px solid var(--color-border-subtle);
        background: var(--color-background-elevated);
      }
      .p-dialog-header .p-dialog-title {
        font-size: var(--font-size-lg);
        font-weight: var(--font-weight-semibold);
        color: var(--color-text-primary);
      }
      .p-dialog-footer {
        padding: var(--spacing-4) var(--spacing-6);
        border-top: 1px solid var(--color-border-subtle);
        background: var(--color-background-subtle);
      }
      .p-calendar,
      .p-dropdown,
      .p-inputnumber {
        width: 100%;
      }
    }
  `]
})
export class AddCashOperationDialogComponent implements OnChanges {
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() operationCreated = new EventEmitter<CashOperationListItem>();

  private cashDeskService = inject(CashDeskService);
  private toastService = inject(ToastService);

  readonly CashOperationType = CashOperationType;
  readonly paymentMethodOptions = PAYMENT_METHOD_OPTIONS;
  readonly expenseCategoryGroups = CASH_EXPENSE_CATEGORY_GROUPS;
  readonly revenueCategoryGroups = CASH_REVENUE_CATEGORY_GROUPS;
  readonly maxDate = new Date();

  operationType: CashOperationType = CashOperationType.Credit;
  selectedExpenseCategory: number | null = null;
  selectedRevenueCategory: number | null = null;
  selectedMethod: number | null = 0;
  amount = 0;
  operationDate: Date = new Date();
  label = '';
  reference = '';
  notes = '';

  submitting = signal(false);
  errorMessage = signal('');

  onHide(): void {
    this.visibleChange.emit(false);
    this.resetForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible']?.currentValue) {
      this.resetForm();
    }
  }

  onTypeChange(type: CashOperationType): void {
    this.operationType = type;
    this.errorMessage.set('');
  }

  private resetForm(): void {
    this.operationType = CashOperationType.Credit;
    this.selectedExpenseCategory = null;
    this.selectedRevenueCategory = null;
    this.selectedMethod = 0;
    this.amount = 0;
    this.operationDate = new Date();
    this.label = '';
    this.reference = '';
    this.notes = '';
    this.errorMessage.set('');
    this.submitting.set(false);
  }

  private validate(): string | null {
    if (this.operationType === CashOperationType.Debit && this.selectedExpenseCategory === null)
      return 'Sélectionnez une catégorie de dépense.';

    if (this.operationType === CashOperationType.Credit && this.selectedRevenueCategory === null)
      return 'Catégorie de revenu est obligatoire.';

    if (this.selectedMethod === null)
      return 'Sélectionnez une méthode de paiement.';

    if (this.amount <= 0)
      return 'Le montant doit être strictement positif.';

    if (!this.operationDate)
      return 'La date de règlement est obligatoire.';

    if (!this.label.trim())
      return 'Le libellé est obligatoire.';

    if (this.label.trim().length > 500)
      return 'Le libellé ne peut pas dépasser 500 caractères.';

    if (this.reference?.trim() && this.reference.trim().length > 100)
      return 'La référence ne peut pas dépasser 100 caractères.';

    if (this.notes?.trim() && this.notes.trim().length > 500)
      return 'Les notes ne peuvent pas dépasser 500 caractères.';

    return null;
  }

  submit(): void {
    const validation = this.validate();
    if (validation) {
      this.errorMessage.set(validation);
      return;
    }

    if (this.submitting()) return;

    this.submitting.set(true);
    this.errorMessage.set('');

    const payload: CreateCashOperationPayload = {
      operationType: this.operationType,
      operationDate: formatLocalDate(this.operationDate),
      amount: this.amount,
      method: this.selectedMethod!,
      label: this.label.trim(),
      category: this.operationType === CashOperationType.Debit ? this.selectedExpenseCategory : null,
      revenueCategory: this.operationType === CashOperationType.Credit ? this.selectedRevenueCategory : null,
      reference: this.reference?.trim() ? this.reference.trim() : null,
      notes: this.notes?.trim() ? this.notes.trim() : null
    };

    this.cashDeskService.createOperation(payload).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          const typeLabel = this.operationType === CashOperationType.Credit ? 'Encaissement' : 'Décaissement';
          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: `${typeLabel} enregistré avec succès`
          });
          this.operationCreated.emit(res.data);
          this.onHide();
        } else {
          const msg = res.message ?? 'Impossible d\u2019enregistrer l\u2019opération.';
          this.errorMessage.set(msg);
        }
        this.submitting.set(false);
      },
      error: (err) => {
        const msg =
          err?.error?.errors?.[0] ??
          err?.error?.message ??
          err?.error?.error?.description ??
          err?.message ??
          'Erreur lors de l\u2019enregistrement de l\u2019opération.';
        this.errorMessage.set(msg);
        this.submitting.set(false);
      }
    });
  }
}
