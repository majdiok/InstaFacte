import { Component, DestroyRef, OnInit, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { Textarea } from 'primeng/textarea';
import { CheckboxModule } from 'primeng/checkbox';
import { TableModule } from 'primeng/table';
import { InputNumberModule } from 'primeng/inputnumber';
import { ButtonComponent } from '@shared/components/button/button.component';
import { CurrencyPipe } from '@angular/common';
import {
  SupplierInvoicePrefill,
  SupplierInvoicePrefillLine
} from '@core/services/purchase-order.service';
import { SupplierInvoiceService } from '@core/services/supplier-invoice.service';
import { AccountingFeatureFlagsService } from '@features/accounting/shared/accounting-feature-flags.service';
import {
  DepreciationRateCategoryDto,
  FixedAssetsService
} from '@features/accounting/services/fixed-assets.service';

export interface SupplierInvoiceLineAssetClassification {
  lineNumber: number;
  isFixedAsset: boolean;
  depreciationRateCategoryId?: string;
  assetAccountNumber?: string;
}

export interface CreateSupplierInvoiceRequest {
  invoiceNumber: string;
  invoiceDate: Date;
  paymentTermDays: number;
  externalReference?: string;
  notes?: string;
  sendEmail?: boolean;
  lines?: { sourceLineId: string; quantityToInvoice: number }[];
  lineAssetClassifications?: SupplierInvoiceLineAssetClassification[];
  /** Mode de paiement prévu (informatif), ex. « Effet de commerce ». */
  paymentMethod?: string;
  useSuggestedNumber?: boolean;
}

interface InvoiceLineRow {
  sourceLineId: string;
  lineNumber: number;
  productCode: string;
  productName: string;
  maxQuantity: number;
  quantity: number;
  unitPriceHT: number;
  subTotalHT: number;
}

interface LineClassificationRow {
  lineNumber: number;
  productName: string;
  subTotal: number;
  isFixedAsset: boolean;
  depreciationRateCategoryId?: string;
  assetAccountNumber?: string;
}

@Component({
  selector: 'app-create-supplier-invoice-modal',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DialogModule, InputTextModule,
    DatePickerModule, SelectModule, Textarea, CheckboxModule,
    TableModule, InputNumberModule, ButtonComponent
  ],
  template: `
    <p-dialog 
      [header]="modalHeader"
      [(visible)]="visible" 
      [modal]="true" 
      [style]="{ width: showAssetClassification ? '900px' : invoiceLines.length > 0 ? '760px' : '500px' }"
      (onHide)="onHide()">
      
      <div class="modal-content">
        <!-- PO Info -->
        <div class="po-info">
          <div class="po-info-item">
            <span class="label">{{ sourceTypeLabel }}</span>
            <span class="value">{{ sourceDocumentNumber }}</span>
          </div>
          <div class="po-info-item po-info-item--right">
            <span class="label">Montant TTC sélectionné</span>
            <span class="value amount">{{ selectedTotalTTC | currency:'TND':'symbol':'1.3-3' }}</span>
          </div>
        </div>

        @if (invoiceLines.length > 0) {
          <div class="form-group">
            <label>Quantités à facturer</label>
            <p-table [value]="invoiceLines" styleClass="p-datatable-sm invoice-lines-table">
              <ng-template pTemplate="header">
                <tr>
                  <th>#</th>
                  <th>Article</th>
                  <th class="text-right">Dispo.</th>
                  <th class="text-right">Qté à facturer</th>
                  <th class="text-right">HT ligne</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td>{{ row.lineNumber }}</td>
                  <td>
                    <div class="product-cell">
                      <span>{{ row.productName }}</span>
                      <small>{{ row.productCode }}</small>
                    </div>
                  </td>
                  <td class="text-right">{{ row.maxQuantity }}</td>
                  <td class="text-right qty-cell">
                    <p-inputNumber
                      [(ngModel)]="row.quantity"
                      [name]="'qty-' + row.sourceLineId"
                      [min]="0"
                      [max]="row.maxQuantity"
                      [minFractionDigits]="0"
                      [maxFractionDigits]="3"
                      (onInput)="onQuantityChanged(row)">
                    </p-inputNumber>
                  </td>
                  <td class="text-right">{{ lineTotalHT(row) | currency:'TND':'symbol':'1.3-3' }}</td>
                </tr>
              </ng-template>
            </p-table>
            <div class="qty-actions">
              <app-button type="button" variant="outline" size="sm" (click)="resetQuantitiesToMax()">
                Tout facturer
              </app-button>
            </div>
          </div>
        }

        <!-- Info Message -->
        <div class="info-message">
          <div class="info-icon-wrap">
            <i class="pi pi-info-circle"></i>
          </div>
          <div class="info-content">
            <strong>Information importante</strong>
            <p>Seules les quantités reçues et non encore facturées peuvent être imputées sur cette facture fournisseur.</p>
          </div>
        </div>

        <form #form="ngForm" (ngSubmit)="onSubmit()">
          <!-- Invoice Number -->
          <div class="form-group">
            <label for="invoiceNumber">Numéro de facture <span class="required">*</span></label>
            <div class="input-group">
              <input
                pInputText
                id="invoiceNumber"
                [(ngModel)]="request.invoiceNumber"
                name="invoiceNumber"
                required
                placeholder="FS-2026-000001"
                class="w-full"
                (ngModelChange)="onInvoiceNumberEdited()">
              <app-button
                type="button"
                variant="outline"
                size="sm"
                [icon]="regenerating ? 'pi-spin pi-spinner' : 'pi-refresh'"
                iconPos="left"
                [disabled]="regenerating"
                (click)="generateInvoiceNumber()"
                class="generate-btn">
                Générer
              </app-button>
            </div>
          </div>

          <!-- Invoice Date -->
          <div class="form-group">
            <label for="invoiceDate">Date de facture <span class="required">*</span></label>
            <p-datepicker
              id="invoiceDate"
              [(ngModel)]="request.invoiceDate"
              name="invoiceDate"
              required
              dateFormat="dd/mm/yy"
              [showIcon]="true"
              class="w-full">
            </p-datepicker>
          </div>

          <!-- Payment Terms -->
          <div class="form-group">
            <label for="paymentTermDays">Conditions de paiement</label>
            <p-select
              id="paymentTermDays"
              [(ngModel)]="request.paymentTermDays"
              name="paymentTermDays"
              [options]="paymentTermOptions"
              optionLabel="label"
              optionValue="value"
              class="w-full">
            </p-select>
          </div>

          <!-- Payment Method (intended) -->
          <div class="form-group">
            <label for="paymentMethod">Mode de paiement prévu</label>
            <p-select
              id="paymentMethod"
              [(ngModel)]="request.paymentMethod"
              name="paymentMethod"
              [options]="paymentMethodOptions"
              optionLabel="label"
              optionValue="value"
              [showClear]="true"
              placeholder="Optionnel"
              class="w-full">
            </p-select>
          </div>

          <!-- External Reference -->
          <div class="form-group">
            <label for="externalReference">Référence externe</label>
            <input 
              pInputText 
              id="externalReference"
              [(ngModel)]="request.externalReference"
              name="externalReference"
              placeholder="Référence du fournisseur..."
              class="w-full">
          </div>

          <!-- Notes -->
          <div class="form-group">
            <label for="notes">Notes</label>
            <textarea
              pTextarea
              id="notes"
              [(ngModel)]="request.notes"
              name="notes"
              placeholder="Notes internes..."
              [rows]="3"
              class="w-full">
            </textarea>
          </div>

          <!-- Send Email Option -->
          <div class="form-group">
            <div class="option-tile">
              <p-checkbox 
                [(ngModel)]="request.sendEmail"
                name="sendEmail"
                binary="true"
                label="Envoyer par email après création">
              </p-checkbox>
              <i class="pi pi-envelope option-tile-icon"></i>
            </div>
          </div>

          <div class="form-group" *ngIf="showAssetClassification">
            <label>Classification immobilisations (lignes reçues)</label>
            <p class="asset-hint">
              Cochez les lignes à comptabiliser en 21x / 43662. Un brouillon sera créé dans le registre immobilisations.
            </p>
            <p-table [value]="lineClassifications" styleClass="p-datatable-sm asset-lines-table">
              <ng-template pTemplate="header">
                <tr>
                  <th>Ligne</th>
                  <th>Article</th>
                  <th class="text-right">HT</th>
                  <th>Immo</th>
                  <th>Catégorie (NC 22-5)</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td>{{ row.lineNumber }}</td>
                  <td>{{ row.productName }}</td>
                  <td class="text-right">{{ row.subTotal | currency:'TND':'symbol':'1.3-3' }}</td>
                  <td>
                    <p-checkbox [(ngModel)]="row.isFixedAsset" [name]="'asset-' + row.lineNumber" binary="true"
                      (onChange)="onAssetToggle(row)"></p-checkbox>
                  </td>
                  <td>
                    <p-select
                      *ngIf="row.isFixedAsset"
                      [(ngModel)]="row.depreciationRateCategoryId"
                      [name]="'cat-' + row.lineNumber"
                      [options]="rateCategories"
                      optionLabel="label"
                      optionValue="id"
                      placeholder="Catégorie"
                      [style]="{ width: '100%' }"
                      (onChange)="onCategorySelected(row)">
                      <ng-template pTemplate="item" let-cat>
                        {{ cat.label }} ({{ cat.legalRatePercent | number:'1.2-2' }} %)
                      </ng-template>
                      <ng-template pTemplate="selectedItem" let-cat>
                        <span *ngIf="cat">{{ cat.label }}</span>
                      </ng-template>
                    </p-select>
                  </td>
                </tr>
              </ng-template>
            </p-table>
          </div>
        </form>
      </div>

      <ng-template pTemplate="footer">
        <div class="dialog-footer">
          <app-button 
            variant="outline" 
            icon="pi-times"
            iconPos="left"
            (click)="onHide()">
            Annuler
          </app-button>
          <app-button
            variant="primary"
            icon="pi-check"
            iconPos="left"
            [disabled]="!isFormValid() || submitting || regenerating || !hasSelectedQuantity()"
            (click)="onSubmit()">
            {{ submitting ? 'Création...' : regenerating ? 'Génération...' : 'Créer la facture' }}
          </app-button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .modal-content {
      padding: var(--spacing-5) var(--spacing-6);
    }

    /* ── PO Info banner ── */
    .po-info {
      background: var(--color-background-elevated, #fff);
      border: 1px solid var(--color-border-subtle);
      border-left: 4px solid var(--color-primary-500);
      border-radius: var(--radius-xl);
      padding: var(--spacing-4) var(--spacing-5);
      margin-bottom: var(--spacing-5);
      display: flex;
      justify-content: space-between;
      align-items: center;
      box-shadow: var(--shadow-sm);
    }

    .po-info-item {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);
    }

    .po-info-item .label {
      font-size: var(--font-size-xs);
      color: var(--color-text-tertiary);
      font-weight: var(--font-weight-semibold);
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .po-info-item .value {
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
    }

    .po-info-item--right {
      text-align: right;
    }

    .po-info-item .amount {
      font-family: 'JetBrains Mono', 'SF Mono', 'Consolas', monospace;
      font-variant-numeric: tabular-nums;
      font-size: var(--font-size-lg);
      color: var(--color-primary-700);
    }

    /* ── Info message ── */
    .info-message {
      background: var(--color-primary-50, #eff6ff);
      border: 1px solid var(--color-primary-200);
      border-radius: var(--radius-xl);
      padding: var(--spacing-4);
      margin-bottom: var(--spacing-5);
      display: flex;
      align-items: flex-start;
      gap: var(--spacing-3);
    }

    .info-icon-wrap {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      min-width: 32px;
      border-radius: var(--radius-lg);
      background: var(--color-primary-100);

      i {
        color: var(--color-primary-600);
        font-size: 1rem;
      }
    }

    .info-content {
      flex: 1;
      min-width: 0;

      strong {
        display: block;
        font-size: var(--font-size-sm);
        font-weight: var(--font-weight-semibold);
        color: var(--color-primary-800);
        margin-bottom: var(--spacing-1);
      }

      p {
        margin: 0;
        font-size: var(--font-size-sm);
        color: var(--color-primary-700);
        line-height: var(--line-height-normal);
      }
    }

    /* ── Form fields ── */
    .form-group {
      margin-bottom: var(--spacing-5);
    }

    .form-group:last-child {
      margin-bottom: 0;
    }

    .form-group label {
      display: block;
      margin-bottom: var(--spacing-2);
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-xs);
      color: var(--color-text-secondary);
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .required {
      color: var(--color-error-600);
      margin-left: var(--spacing-1);
    }

    .input-group {
      display: flex;
      gap: var(--spacing-2);
    }

    .input-group input {
      flex: 1;
    }

    .generate-btn {
      flex-shrink: 0;
      transition: transform var(--transition-fast);
    }

    .generate-btn:hover {
      transform: scale(1.04);
    }

    /* ── Option tile (checkbox) ── */
    .option-tile {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: var(--color-background-subtle);
      border: 1px solid var(--color-border-subtle);
      border-radius: var(--radius-lg);
      padding: var(--spacing-3) var(--spacing-4);
      transition: all var(--transition-fast);
    }

    .option-tile:hover {
      border-color: var(--color-primary-300);
      background: var(--color-primary-50);
    }

    .option-tile-icon {
      color: var(--color-text-tertiary);
      font-size: var(--font-size-base);
    }

    /* ── Dialog footer ── */
    .dialog-footer {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3);
    }

    .asset-hint {
      margin: 0 0 var(--spacing-3);
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
      line-height: var(--line-height-normal);
    }

    .text-right {
      text-align: right;
    }

    .product-cell {
      display: flex;
      flex-direction: column;
      gap: 2px;

      small {
        color: var(--color-text-tertiary);
        font-size: var(--font-size-xs);
      }
    }

    .qty-cell :host ::ng-deep .p-inputnumber,
    .qty-cell ::ng-deep .p-inputnumber-input {
      width: 110px;
    }

    .qty-actions {
      margin-top: var(--spacing-2);
      display: flex;
      justify-content: flex-end;
    }

    :host ::ng-deep .invoice-lines-table .p-inputnumber-input {
      text-align: right;
    }

    :host ::ng-deep .asset-lines-table .p-select {
      min-width: 220px;
    }

    /* ── Dialog overrides ── */
    :host ::ng-deep {
      .p-dialog {
        border-radius: var(--radius-xl);
        box-shadow: var(--shadow-xl);
        overflow: hidden;
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
    }
  `]
})
export class CreateSupplierInvoiceModalComponent implements OnInit {
  @ViewChild('form') formRef?: NgForm;

  private readonly fixedAssetsApi = inject(FixedAssetsService);
  private readonly accountingFlags = inject(AccountingFeatureFlagsService);
  private readonly supplierInvoiceService = inject(SupplierInvoiceService);
  private readonly destroyRef = inject(DestroyRef);

  visible = false;
  submitting = false;
  regenerating = false;
  invoiceNumberManuallyEdited = false;
  sourceDocumentNumber = '';
  sourceTypeLabel = 'Document';
  selectedTotalTTC = 0;
  showAssetClassification = false;
  invoiceLines: InvoiceLineRow[] = [];
  lineClassifications: LineClassificationRow[] = [];
  rateCategories: DepreciationRateCategoryDto[] = [];

  request: CreateSupplierInvoiceRequest = {
    invoiceNumber: '',
    invoiceDate: new Date(),
    paymentTermDays: 30,
    externalReference: '',
    notes: '',
    sendEmail: false
  };

  paymentTermOptions = [
    { label: '30 jours', value: 30 },
    { label: '60 jours', value: 60 },
    { label: '90 jours', value: 90 }
  ];

  paymentMethodOptions = [
    { label: 'Effet de commerce', value: 'Effet de commerce' },
    { label: 'Virement bancaire', value: 'Virement bancaire' },
    { label: 'Espèces', value: 'Espèces' },
    { label: 'Chèque', value: 'Chèque' },
    { label: 'Carte bancaire', value: 'Carte bancaire' }
  ];

  private onClose?: () => void;
  private onConfirm?: (request: CreateSupplierInvoiceRequest) => void;

  ngOnInit(): void {
    // Initialize with current date
    this.request.invoiceDate = new Date();
  }

  open(config: {
    prefill: SupplierInvoicePrefill;
    sourceType?: 'po' | 'pr';
    onClose?: () => void;
    onConfirm?: (request: CreateSupplierInvoiceRequest) => void;
  }): void {
    const prefill = config.prefill;
    this.sourceDocumentNumber = prefill.purchaseReceiptNumber ?? prefill.purchaseOrderNumber ?? '—';
    this.sourceTypeLabel = config.sourceType === 'pr' ? 'Bon de réception' : 'Bon de commande';
    this.request.paymentTermDays = prefill.paymentTermDays ?? 30;
    this.onClose = config.onClose;
    this.onConfirm = config.onConfirm;
    this.initInvoiceLines(prefill.lines);
    this.initLineClassifications(prefill.lines);
    this.recalculateSelectedTotal(prefill);
    this.applySuggestedInvoiceNumber(prefill.suggestedInvoiceNumber);
    this.visible = true;
  }

  onInvoiceNumberEdited(): void {
    this.invoiceNumberManuallyEdited = true;
  }

  lineTotalHT(row: InvoiceLineRow): number {
    if (row.maxQuantity <= 0) return 0;
    return (row.subTotalHT / row.maxQuantity) * row.quantity;
  }

  onQuantityChanged(row: InvoiceLineRow): void {
    if (row.quantity > row.maxQuantity) row.quantity = row.maxQuantity;
    if (row.quantity < 0) row.quantity = 0;
    this.recalculateSelectedTotal();
  }

  resetQuantitiesToMax(): void {
    this.invoiceLines.forEach(l => l.quantity = l.maxQuantity);
    this.recalculateSelectedTotal();
  }

  hasSelectedQuantity(): boolean {
    return this.invoiceLines.some(l => l.quantity > 0);
  }

  onHide(): void {
    this.visible = false;
    this.onClose?.();
    this.resetForm();
  }

  /**
   * Fetches a fresh suggested number from the server and patches the form.
   * Returns the new number (or null if the request failed). Callers can await
   * it to safely resubmit without racing the async preview call.
   */
  async generateInvoiceNumber(): Promise<string | null> {
    this.regenerating = true;
    try {
      const res = await firstValueFrom(
        this.supplierInvoiceService.previewNumber(this.request.invoiceDate)
          .pipe(takeUntilDestroyed(this.destroyRef))
      );
      if (res?.success && res.data) {
        this.request.invoiceNumber = res.data;
        this.invoiceNumberManuallyEdited = false;
        return res.data;
      }
      return null;
    } catch {
      return null;
    } finally {
      this.regenerating = false;
    }
  }

  /**
   * Handles a 409 duplicate response from create-supplier-invoice.
   * If the server included a `suggestedInvoiceNumber` in the error payload, patches
   * the form directly (no round-trip). Otherwise falls back to a fresh preview call.
   * Returns the new number now shown in the field, or null on failure.
   */
  async handleConflict(suggestedFromServer?: string | null): Promise<string | null> {
    const trimmed = suggestedFromServer?.trim();
    if (trimmed) {
      this.request.invoiceNumber = trimmed;
      this.invoiceNumberManuallyEdited = false;
      return trimmed;
    }
    return this.generateInvoiceNumber();
  }

  onSubmit(): void {
    if (this.submitting || !this.request.invoiceNumber.trim()) {
      return;
    }

    this.submitting = true;
    const selectedLines = this.invoiceLines
      .filter(l => l.quantity > 0)
      .map(l => ({ sourceLineId: l.sourceLineId, quantityToInvoice: l.quantity }));
    const allMax = this.invoiceLines.every(l => l.quantity === l.maxQuantity || l.quantity === 0);
    const payload: CreateSupplierInvoiceRequest = {
      ...this.request,
      lines: allMax && selectedLines.length === this.invoiceLines.length ? undefined : selectedLines,
      lineAssetClassifications: this.buildLineAssetClassifications(),
      useSuggestedNumber: !this.invoiceNumberManuallyEdited
    };
    this.onConfirm?.(payload);
  }

  onAssetToggle(row: LineClassificationRow): void {
    if (!row.isFixedAsset) {
      row.depreciationRateCategoryId = undefined;
      row.assetAccountNumber = undefined;
    }
  }

  onCategorySelected(row: LineClassificationRow): void {
    const cat = this.rateCategories.find(c => c.id === row.depreciationRateCategoryId);
    row.assetAccountNumber = cat?.defaultAssetAccount;
  }

  closeAfterSuccess(): void {
    this.visible = false;
    this.resetForm();
  }

  setSubmitting(value: boolean): void {
    this.submitting = value;
  }

  isFormValid(): boolean {
    return this.formRef?.valid ?? false;
  }

  private resetForm(): void {
    this.request = {
      invoiceNumber: '',
      invoiceDate: new Date(),
      paymentTermDays: 30,
      externalReference: '',
      notes: '',
      sendEmail: false
    };
    this.lineClassifications = [];
    this.invoiceLines = [];
    this.showAssetClassification = false;
    this.selectedTotalTTC = 0;
    this.submitting = false;
    this.regenerating = false;
    this.invoiceNumberManuallyEdited = false;
  }

  private applySuggestedInvoiceNumber(suggested?: string | null): void {
    if (suggested?.trim()) {
      this.request.invoiceNumber = suggested.trim();
      this.invoiceNumberManuallyEdited = false;
    } else {
      void this.generateInvoiceNumber();
    }
  }

  private initInvoiceLines(lines: SupplierInvoicePrefillLine[]): void {
    this.invoiceLines = lines
      .filter(l => l.maxQuantityToInvoice > 0)
      .map(l => ({
        sourceLineId: l.sourceLineId,
        lineNumber: l.lineNumber,
        productCode: l.productCode,
        productName: l.productName,
        maxQuantity: l.maxQuantityToInvoice,
        quantity: l.quantityToInvoice > 0 ? l.quantityToInvoice : l.maxQuantityToInvoice,
        unitPriceHT: l.unitPriceHT,
        subTotalHT: l.subTotalHT
      }));
  }

  private recalculateSelectedTotal(prefill?: SupplierInvoicePrefill): void {
    if (this.invoiceLines.length === 0) {
      this.selectedTotalTTC = prefill?.totalTTC ?? 0;
      return;
    }
    const ht = this.invoiceLines.reduce((sum, row) => sum + this.lineTotalHT(row), 0);
    const ratio = prefill && prefill.subTotalHT > 0 ? prefill.totalTTC / prefill.subTotalHT : 1.19;
    this.selectedTotalTTC = ht * ratio;
  }

  private initLineClassifications(lines: SupplierInvoicePrefillLine[]): void {
    const billable = lines.filter(l => l.maxQuantityToInvoice > 0);
    this.lineClassifications = billable.map(l => ({
      lineNumber: l.lineNumber,
      productName: l.productName,
      subTotal: l.subTotalHT,
      isFixedAsset: false
    }));

    const fixedAssetsEnabled = this.accountingFlags.isEnabled('fixedAssetsEnabled');
    this.showAssetClassification = fixedAssetsEnabled && this.lineClassifications.length > 0;

    if (this.showAssetClassification && this.rateCategories.length === 0) {
      this.fixedAssetsApi.getRateCategories().subscribe(res => {
        this.rateCategories = res.data ?? [];
      });
    }
  }

  private buildLineAssetClassifications(): SupplierInvoiceLineAssetClassification[] | undefined {
    if (!this.showAssetClassification) return undefined;
    const rows = this.lineClassifications
      .filter(r => r.isFixedAsset)
      .map(r => ({
        lineNumber: r.lineNumber,
        isFixedAsset: true,
        depreciationRateCategoryId: r.depreciationRateCategoryId,
        assetAccountNumber: r.assetAccountNumber
      }));
    return rows.length > 0 ? rows : undefined;
  }

  get modalHeader(): string {
    return `Créer facture fournisseur - ${this.sourceDocumentNumber}`;
  }
}
