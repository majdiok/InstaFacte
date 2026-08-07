import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

// PrimeNG
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { RadioButtonModule } from 'primeng/radiobutton';
import { TooltipModule } from 'primeng/tooltip';
import { CardModule } from 'primeng/card';
import { DividerModule } from 'primeng/divider';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { WarehouseSelectorComponent } from '@shared/components/warehouse-selector/warehouse-selector.component';

// Services & Models
import { InvoiceWizardService } from '../../services/invoice-wizard.service';
import { InvoiceService, InvoiceListItem } from '@core/services/invoice.service';
import { 
  InvoiceType,
  Currency,
  INVOICE_TYPE_OPTIONS,
  CURRENCY_OPTIONS
} from '../../models/invoice-wizard.models';

/**
 * Étape 1 - Type de document & métadonnées
 * 
 * Permet de sélectionner:
 * - Type de facture (Facture / Facture d'avoir)
 * - Date d'émission
 * - Date d'échéance
 * - Devise
 * - Référence interne
 * 
 * Conformité tunisienne:
 * - Numéro auto-généré, séquentiel, non modifiable
 * - Date d'émission obligatoire
 * - TND comme devise par défaut
 */
@Component({
  selector: 'app-step-metadata',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    SelectModule,
    DatePickerModule,
    InputTextModule,
    RadioButtonModule,
    TooltipModule,
    CardModule,
    DividerModule,
    AutoCompleteModule,
    WarehouseSelectorComponent
  ],
  template: `
    <div class="step-metadata">
      <!-- Document Type Selection -->
      <section class="section" aria-labelledby="document-type-heading">
        <h2 id="document-type-heading" class="section-title">
          Type de document
        </h2>
        <p class="section-description">
          Sélectionnez le type de document fiscal à créer
        </p>

        @if (metadata.type === 'CREDIT_NOTE' && metadata.linkedInvoiceId) {
          <div class="credit-note-locked">
            <div class="type-card selected">
              <div class="type-card-icon">
                <i class="pi pi-file-edit"></i>
              </div>
              <div class="type-card-content">
                <span class="type-card-label">Facture d'avoir</span>
                <span class="type-card-description">Liée à une facture existante</span>
              </div>
              <div class="type-card-check">
                <i class="pi pi-check"></i>
              </div>
            </div>
          </div>
        } @else {
          <div class="document-type-cards">
            @for (option of invoiceTypeOptions; track option.value) {
              <div 
                class="type-card"
                [class.selected]="metadata.type === option.value"
                (click)="onTypeChange(option.value)"
                (keydown.enter)="onTypeChange(option.value)"
                (keydown.space)="onTypeChange(option.value)"
                tabindex="0"
                role="radio"
                [attr.aria-checked]="metadata.type === option.value">
                <div class="type-card-icon">
                  <i [class]="option.icon"></i>
                </div>
                <div class="type-card-content">
                  <span class="type-card-label">{{ option.label }}</span>
                  <span class="type-card-description">{{ option.description }}</span>
                </div>
                <div class="type-card-check" *ngIf="metadata.type === option.value">
                  <i class="pi pi-check"></i>
                </div>
              </div>
            }
          </div>
        }

        @if (metadata.type === 'CREDIT_NOTE') {
          <div class="credit-note-section">
            <div class="credit-note-warning">
              <i class="pi pi-info-circle"></i>
              <span>
                Une facture d'avoir doit être liée à une facture existante.
                Sélectionnez la facture d'origine ci-dessous.
              </span>
            </div>

            @if (metadata.linkedInvoiceId) {
              <div class="linked-invoice-readonly">
                <div class="linked-invoice-label">Facture d'origine</div>
                @if (linkedInvoiceDisplay()) {
                  <div class="linked-invoice-card">
                    <span class="linked-invoice-number">{{ linkedInvoiceDisplay() }}</span>
                    <a [routerLink]="['/invoices', metadata.linkedInvoiceId]" 
                       class="linked-invoice-link" 
                       target="_blank"
                       (click)="$event.stopPropagation()">
                      Voir la facture
                      <i class="pi pi-external-link"></i>
                    </a>
                  </div>
                } @else {
                  <div class="linked-invoice-card">
                    <span class="linked-invoice-number">Facture sélectionnée</span>
                  </div>
                }
              </div>
            } @else {
              <div class="form-group linked-invoice-select">
                <label for="linkedInvoice" class="required">Facture d'origine</label>
                <p-autoComplete
                  inputId="linkedInvoice"
                  [(ngModel)]="linkedInvoiceSearch"
                  [suggestions]="invoiceSuggestions"
                  (completeMethod)="searchInvoices($event)"
                  (onSelect)="onLinkedInvoiceSelect($event)"
                  field="number"
                  [dropdown]="false"
                  [minLength]="1"
                  placeholder="Rechercher par numéro ou client..."
                  appendTo="body"
                  [inputStyle]="{ width: '100%' }"
                  [style]="{ width: '100%' }">
                  <ng-template let-invoice pTemplate="item">
                    <div class="invoice-suggestion">
                      <span class="invoice-num">{{ invoice.number }}</span>
                      <span class="invoice-client">{{ invoice.clientName }}</span>
                      <span class="invoice-amount">{{ invoice.totalAmount | number:'1.3-3' }} {{ invoice.currency }}</span>
                    </div>
                  </ng-template>
                  <ng-template pTemplate="empty">
                    <div class="invoice-empty">
                      <span>Aucune facture trouvée</span>
                    </div>
                  </ng-template>
                </p-autoComplete>
              </div>
            }
          </div>
        }
      </section>

      <p-divider></p-divider>

      <!-- Invoice Number (Read-only) -->
      <section class="section">
        <h2 class="section-title">
          Numéro de facture
          <span class="badge-auto">Auto-généré</span>
        </h2>
        <p class="section-description">
          Le numéro est attribué automatiquement selon la séquence légale
        </p>

        <div class="invoice-number-display">
          <div class="number-prefix">
            @if (metadata.type === 'INVOICE') {
              <span class="prefix-badge">FAC</span>
            } @else {
              <span class="prefix-badge avoir">AVO</span>
            }
          </div>
          <div class="number-value">
            @if (metadata.invoiceNumber) {
              {{ metadata.invoiceNumber }}
            } @else {
              <span class="loading">
                <i class="pi pi-spin pi-spinner"></i>
                Attribution en cours...
              </span>
            }
          </div>
          <div 
            class="number-info"
            pTooltip="Conformité légale: numérotation chronologique et inaltérable (Art. 18 Code TVA)"
            tooltipPosition="top">
            <i class="pi pi-lock"></i>
          </div>
        </div>
      </section>

      <p-divider></p-divider>

      <!-- Dates -->
      <section class="section">
        <h2 class="section-title">Dates</h2>
        <p class="section-description">
          Définissez les dates d'émission et d'échéance
        </p>

        <div class="form-grid">
          <div class="form-group">
            <label for="issueDate" class="required">
              Date d'émission
            </label>
            <p-datepicker
              inputId="issueDate"
              [(ngModel)]="issueDate"
              (ngModelChange)="onIssueDateChange($event)"
              [showIcon]="true"
              dateFormat="dd/mm/yy"
              [maxDate]="maxIssueDate"
              [showButtonBar]="true"
              [touchUI]="isMobile"
              placeholder="Sélectionner une date"
              appendTo="body"
              aria-describedby="issueDateHint">
            </p-datepicker>
            <small id="issueDateHint" class="form-hint">
              Date figurant sur la facture (obligatoire)
            </small>
          </div>

          <div class="form-group">
            <label for="dueDate">
              Date d'échéance
            </label>
            <p-datepicker
              inputId="dueDate"
              [(ngModel)]="dueDate"
              (ngModelChange)="onDueDateChange($event)"
              [showIcon]="true"
              dateFormat="dd/mm/yy"
              [minDate]="issueDate"
              [showButtonBar]="true"
              [touchUI]="isMobile"
              placeholder="Sélectionner une date"
              appendTo="body"
              aria-describedby="dueDateHint">
            </p-datepicker>
            <small id="dueDateHint" class="form-hint">
              Date limite de paiement
            </small>

            <!-- Quick selection buttons -->
            <div class="due-date-shortcuts">
              <button 
                type="button" 
                class="shortcut-btn"
                [class.active]="dueDateDays === 0"
                (click)="setDueDateDays(0)">
                Comptant
              </button>
              <button 
                type="button" 
                class="shortcut-btn"
                [class.active]="dueDateDays === 30"
                (click)="setDueDateDays(30)">
                30 jours
              </button>
              <button 
                type="button" 
                class="shortcut-btn"
                [class.active]="dueDateDays === 60"
                (click)="setDueDateDays(60)">
                60 jours
              </button>
              <button 
                type="button" 
                class="shortcut-btn"
                [class.active]="dueDateDays === 90"
                (click)="setDueDateDays(90)">
                90 jours
              </button>
            </div>
          </div>
        </div>
      </section>

      <p-divider></p-divider>

      <!-- Currency & Reference -->
      <section class="section">
        <h2 class="section-title">Devise et référence</h2>

        <div class="form-grid">
          <div class="form-group">
            <label for="currency" class="required">
              Devise
            </label>
            <p-select
              inputId="currency"
              [options]="currencyOptions"
              [(ngModel)]="currency"
              (ngModelChange)="onCurrencyChange($event)"
              optionLabel="label"
              optionValue="value"
              [showClear]="false"
              appendTo="body"
              aria-describedby="currencyHint">
            </p-select>
            <small id="currencyHint" class="form-hint">
              TND obligatoire pour le marché local
            </small>
          </div>

          <div class="form-group">
            <label for="internalRef">
              Référence interne
            </label>
            <input
              pInputText
              type="text"
              id="internalRef"
              [(ngModel)]="internalReference"
              (ngModelChange)="onReferenceChange($event)"
              placeholder="Ex: PROJ-2026-001"
              maxlength="50"
              aria-describedby="internalRefHint">
            <small id="internalRefHint" class="form-hint">
              Référence projet, devis ou bon de commande (optionnel)
            </small>
          </div>

          <div class="form-group">
            <app-warehouse-selector
              inputId="invoice-wizard-warehouse"
              label="Entrepôt"
              placeholder="Entrepôt par défaut"
              [value]="metadata.warehouseId"
              (valueChange)="onWarehouseChange($event)">
            </app-warehouse-selector>
            <small class="form-hint">
              Entrepôt utilisé pour la déduction de stock (optionnel)
            </small>
          </div>
        </div>
      </section>

      <!-- Compliance Summary -->
      <div class="compliance-box">
        <div class="compliance-header">
          <i class="pi pi-shield"></i>
          <span>Conformité légale tunisienne</span>
        </div>
        <ul class="compliance-list">
          <li>
            <i class="pi pi-check-circle"></i>
            Numérotation chronologique et séquentielle
          </li>
          <li>
            <i class="pi pi-check-circle"></i>
            Date d'émission obligatoire
          </li>
          <li>
            <i class="pi pi-check-circle"></i>
            Conservation 10 ans garantie
          </li>
        </ul>
      </div>
    </div>
  `,
  styles: [`
    .step-metadata {
      animation: fadeIn 0.3s ease-out;
    }

    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(10px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .section {
      margin-bottom: var(--spacing-6);
    }

    .section-title {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      margin: 0 0 var(--spacing-2);
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-800);
    }

    .section-description {
      margin: 0 0 var(--spacing-4);
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
    }

    .badge-auto {
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-medium);
      padding: var(--spacing-1) var(--spacing-2);
      background: var(--color-primary-100);
      color: var(--color-primary-700);
      border-radius: var(--radius-full);
    }

    // Document Type Cards
    .document-type-cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: var(--spacing-4);
    }

    .type-card {
      display: flex;
      align-items: center;
      gap: var(--spacing-4);
      padding: var(--spacing-4);
      background: white;
      border: 2px solid var(--color-neutral-200);
      border-radius: var(--radius-xl);
      cursor: pointer;
      transition: all var(--transition-fast);

      &:hover {
        border-color: var(--color-primary-300);
        background: var(--color-primary-50);
      }

      &:focus-visible {
        outline: 2px solid var(--color-primary-500);
        outline-offset: 2px;
      }

      &.selected {
        border-color: var(--color-primary-500);
        background: var(--color-primary-50);
        box-shadow: 0 0 0 4px var(--color-primary-100);
      }
    }

    .type-card-icon {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 48px;
      height: 48px;
      border-radius: var(--radius-lg);
      background: var(--color-neutral-100);
      color: var(--color-neutral-600);
      font-size: var(--font-size-xl);
      flex-shrink: 0;

      .selected & {
        background: var(--color-primary-500);
        color: white;
      }
    }

    .type-card-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);
    }

    .type-card-label {
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-800);
    }

    .type-card-description {
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
    }

    .type-card-check {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      border-radius: var(--radius-full);
      background: var(--color-primary-500);
      color: white;
      font-size: var(--font-size-sm);
    }

    .credit-note-section {
      margin-top: var(--spacing-4);
    }

    .credit-note-locked .type-card {
      max-width: 320px;
    }

    .credit-note-warning {
      display: flex;
      align-items: flex-start;
      gap: var(--spacing-3);
      margin-bottom: var(--spacing-4);
      padding: var(--spacing-4);
      background: var(--color-warning-50);
      border: 1px solid var(--color-warning-200);
      border-radius: var(--radius-lg);
      font-size: var(--font-size-sm);
      color: var(--color-warning-700);

      i {
        flex-shrink: 0;
        font-size: var(--font-size-lg);
      }
    }

    .linked-invoice-readonly {
      margin-top: var(--spacing-2);
    }

    .linked-invoice-label {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-700);
      margin-bottom: var(--spacing-2);
    }

    .linked-invoice-card {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--spacing-3);
      padding: var(--spacing-4);
      background: var(--color-neutral-50);
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-lg);
    }

    .linked-invoice-number {
      font-family: 'JetBrains Mono', monospace;
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-800);
    }

    .linked-invoice-link {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-1);
      font-size: var(--font-size-sm);
      color: var(--color-primary-600);
      text-decoration: none;

      &:hover {
        text-decoration: underline;
      }
    }

    .linked-invoice-select {
      margin-top: var(--spacing-2);
    }

    .invoice-suggestion {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);

      .invoice-num {
        font-weight: var(--font-weight-semibold);
        font-family: 'JetBrains Mono', monospace;
      }

      .invoice-client {
        font-size: var(--font-size-sm);
        color: var(--color-neutral-600);
      }

      .invoice-amount {
        font-size: var(--font-size-sm);
        color: var(--color-neutral-500);
      }
    }

    .invoice-empty {
      padding: var(--spacing-3);
      color: var(--color-neutral-500);
      font-size: var(--font-size-sm);
    }

    // Invoice Number Display
    .invoice-number-display {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-4);
      background: var(--color-neutral-100);
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-lg);
    }

    .number-prefix .prefix-badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: var(--spacing-2) var(--spacing-3);
      background: var(--color-primary-500);
      color: white;
      font-weight: var(--font-weight-bold);
      font-size: var(--font-size-sm);
      border-radius: var(--radius-md);

      &.avoir {
        background: var(--color-warning-500);
      }
    }

    .number-value {
      flex: 1;
      font-family: 'JetBrains Mono', monospace;
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-800);

      .loading {
        display: flex;
        align-items: center;
        gap: var(--spacing-2);
        font-size: var(--font-size-base);
        font-weight: var(--font-weight-normal);
        color: var(--color-neutral-500);
      }
    }

    .number-info {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border-radius: var(--radius-full);
      background: var(--color-neutral-200);
      color: var(--color-neutral-500);
      cursor: help;
    }

    // Form Grid
    .form-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--spacing-6);

      @media (max-width: 768px) {
        grid-template-columns: 1fr;
      }
    }

    .form-group {
      display: flex;
      flex-direction: column;

      label {
        margin-bottom: var(--spacing-2);
        font-weight: var(--font-weight-medium);
        color: var(--color-neutral-700);

        &.required::after {
          content: ' *';
          color: var(--color-error-500);
        }
      }

      .form-hint {
        margin-top: var(--spacing-1);
        font-size: var(--font-size-xs);
        color: var(--color-neutral-500);
      }

      ::ng-deep {
        .p-inputtext,
        .p-select,
        .p-datepicker {
          width: 100%;
        }
      }
    }

    // Due Date Shortcuts
    .due-date-shortcuts {
      display: flex;
      gap: var(--spacing-2);
      margin-top: var(--spacing-2);
      flex-wrap: wrap;
    }

    .shortcut-btn {
      padding: var(--spacing-1) var(--spacing-3);
      border: 1px solid var(--color-neutral-300);
      background: white;
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-600);
      cursor: pointer;
      transition: all var(--transition-fast);

      &:hover {
        border-color: var(--color-primary-400);
        color: var(--color-primary-600);
      }

      &.active {
        background: var(--color-primary-500);
        border-color: var(--color-primary-500);
        color: white;
      }
    }

    // Compliance Box
    .compliance-box {
      margin-top: var(--spacing-6);
      padding: var(--spacing-4);
      background: var(--color-success-50);
      border: 1px solid var(--color-success-200);
      border-radius: var(--radius-lg);
    }

    .compliance-header {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      font-weight: var(--font-weight-semibold);
      color: var(--color-success-700);
      margin-bottom: var(--spacing-3);

      i {
        font-size: var(--font-size-lg);
      }
    }

    .compliance-list {
      list-style: none;
      padding: 0;
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);

      li {
        display: flex;
        align-items: center;
        gap: var(--spacing-2);
        font-size: var(--font-size-sm);
        color: var(--color-success-700);

        i {
          color: var(--color-success-500);
        }
      }
    }
  `]
})
export class StepMetadataComponent implements OnInit {
  readonly wizardService = inject(InvoiceWizardService);
  private readonly invoiceService = inject(InvoiceService);

  // Options
  invoiceTypeOptions = INVOICE_TYPE_OPTIONS;
  currencyOptions = CURRENCY_OPTIONS;

  // Form bindings
  issueDate: Date = new Date();
  dueDate: Date | null = null;
  currency: Currency = Currency.TND;
  internalReference: string = '';
  dueDateDays: number | null = 30;

  // Linked invoice (credit note)
  linkedInvoiceDisplay = signal<string | null>(null);
  linkedInvoiceSearch = '';
  invoiceSuggestions: InvoiceListItem[] = [];

  // Constraints
  maxIssueDate = new Date();
  isMobile = window.innerWidth < 768;

  get metadata() {
    return this.wizardService.metadata();
  }

  ngOnInit(): void {
    // Initialize from service state
    const meta = this.metadata;
    this.issueDate = new Date(meta.issueDate);
    this.dueDate = meta.dueDate ? new Date(meta.dueDate) : null;
    this.currency = meta.currency;
    this.internalReference = meta.internalReference || '';

    // Calculate due date days
    if (this.dueDate && this.issueDate) {
      const diff = this.dueDate.getTime() - this.issueDate.getTime();
      this.dueDateDays = Math.round(diff / (1000 * 60 * 60 * 24));
    }

    // Load linked invoice display when linkedInvoiceId is set
    const linkedId = meta.linkedInvoiceId;
    if (linkedId) {
      this.invoiceService.getInvoice(linkedId).subscribe({
        next: (res) => {
          if (res?.success && res.data) {
            this.linkedInvoiceDisplay.set(`${res.data.number} – ${res.data.client?.name || ''}`);
          } else {
            this.linkedInvoiceDisplay.set('Facture sélectionnée');
          }
        },
        error: () => this.linkedInvoiceDisplay.set('Facture sélectionnée')
      });
    }
  }

  searchInvoices(event: { query: string }): void {
    const query = event.query?.trim() || '';
    if (query.length < 1) {
      this.invoiceSuggestions = [];
      return;
    }
    this.invoiceService.getInvoices({
      search: query,
      pageSize: 20
    }).subscribe({
      next: (res) => {
        if (res?.success && res.data?.items) {
          this.invoiceSuggestions = res.data.items.filter(
            (inv: InvoiceListItem) => inv.status !== 'Annulée' && inv.status !== 'Brouillon'
          );
        } else {
          this.invoiceSuggestions = [];
        }
      },
      error: () => { this.invoiceSuggestions = []; }
    });
  }

  onLinkedInvoiceSelect(event: { value: InvoiceListItem }): void {
    const invoice = event?.value;
    if (invoice?.id) {
      this.wizardService.updateMetadata({ linkedInvoiceId: invoice.id });
      this.linkedInvoiceSearch = '';
      this.linkedInvoiceDisplay.set(`${invoice.number} – ${invoice.clientName}`);
      this.wizardService.loadClientFromLinkedInvoice(invoice.id);
    }
  }

  onTypeChange(type: InvoiceType): void {
    this.wizardService.updateMetadata({ type });
    this.wizardService.fetchNextInvoiceNumber().subscribe({
      next: () => {},
      error: () => {}
    });
  }

  onIssueDateChange(date: Date): void {
    this.issueDate = date;
    this.wizardService.updateMetadata({ issueDate: date });
    
    // Update due date if days are set
    if (this.dueDateDays !== null) {
      this.setDueDateDays(this.dueDateDays);
    }
  }

  onDueDateChange(date: Date | null): void {
    this.dueDate = date;
    this.wizardService.updateMetadata({ dueDate: date });
    
    // Calculate days difference
    if (date && this.issueDate) {
      const diff = date.getTime() - this.issueDate.getTime();
      this.dueDateDays = Math.round(diff / (1000 * 60 * 60 * 24));
    } else {
      this.dueDateDays = null;
    }
  }

  setDueDateDays(days: number): void {
    this.dueDateDays = days;
    if (this.issueDate) {
      const newDueDate = new Date(this.issueDate);
      newDueDate.setDate(newDueDate.getDate() + days);
      this.dueDate = newDueDate;
      this.wizardService.updateMetadata({ dueDate: newDueDate });
    }
  }

  onCurrencyChange(currency: Currency): void {
    this.currency = currency;
    this.wizardService.updateMetadata({ currency });
  }

  onReferenceChange(reference: string): void {
    this.internalReference = reference;
    this.wizardService.updateMetadata({ 
      internalReference: reference || null 
    });
  }

  onWarehouseChange(warehouseId: string | null): void {
    this.wizardService.updateMetadata({ warehouseId });
  }
}
