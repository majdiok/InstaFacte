import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { CheckboxModule } from 'primeng/checkbox';
import { TableModule } from 'primeng/table';
import { ButtonComponent } from '@shared/components/button/button.component';
import { CurrencyPipe } from '@angular/common';
import { PurchaseOrderLine } from '@core/services/purchase-order.service';
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
  lineAssetClassifications?: SupplierInvoiceLineAssetClassification[];
  /** Mode de paiement prévu (informatif), ex. « Effet de commerce ». */
  paymentMethod?: string;
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
    CalendarModule, DropdownModule, InputTextareaModule, CheckboxModule,
    TableModule, ButtonComponent
  ],
  template: `
    <p-dialog 
      [header]="modalHeader"
      [(visible)]="visible" 
      [modal]="true" 
      [style]="{ width: showAssetClassification ? '760px' : '500px' }"
      (onHide)="onHide()">
      
      <div class="modal-content">
        <!-- PO Info -->
        <div class="po-info">
          <div class="po-info-item">
            <span class="label">Commande</span>
            <span class="value">{{ purchaseOrderNumber }}</span>
          </div>
          <div class="po-info-item po-info-item--right">
            <span class="label">Montant TTC</span>
            <span class="value amount">{{ totalAmount | currency:'TND':'symbol':'1.3-3' }}</span>
          </div>
        </div>

        <!-- Info Message -->
        <div class="info-message">
          <div class="info-icon-wrap">
            <i class="pi pi-info-circle"></i>
          </div>
          <div class="info-content">
            <strong>Information importante</strong>
            <p>Cette facture sera créée à partir des articles déjà reçus dans la commande fournisseur. Seules les marchandises réceptionnées seront facturées.</p>
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
                placeholder="FS-2026-00001"
                class="w-full">
              <app-button 
                type="button"
                variant="outline"
                size="sm"
                icon="pi-refresh"
                iconPos="left"
                (click)="generateInvoiceNumber()"
                class="generate-btn">
                Générer
              </app-button>
            </div>
          </div>

          <!-- Invoice Date -->
          <div class="form-group">
            <label for="invoiceDate">Date de facture <span class="required">*</span></label>
            <p-calendar
              id="invoiceDate"
              [(ngModel)]="request.invoiceDate"
              name="invoiceDate"
              required
              dateFormat="dd/mm/yy"
              [showIcon]="true"
              class="w-full">
            </p-calendar>
          </div>

          <!-- Payment Terms -->
          <div class="form-group">
            <label for="paymentTermDays">Conditions de paiement</label>
            <p-dropdown
              id="paymentTermDays"
              [(ngModel)]="request.paymentTermDays"
              name="paymentTermDays"
              [options]="paymentTermOptions"
              optionLabel="label"
              optionValue="value"
              class="w-full">
            </p-dropdown>
          </div>

          <!-- Payment Method (intended) -->
          <div class="form-group">
            <label for="paymentMethod">Mode de paiement prévu</label>
            <p-dropdown
              id="paymentMethod"
              [(ngModel)]="request.paymentMethod"
              name="paymentMethod"
              [options]="paymentMethodOptions"
              optionLabel="label"
              optionValue="value"
              [showClear]="true"
              placeholder="Optionnel"
              class="w-full">
            </p-dropdown>
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
              pInputTextarea
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
                    <p-dropdown
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
                    </p-dropdown>
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
            [disabled]="!isFormValid() || submitting"
            (click)="onSubmit()">
            {{ submitting ? 'Création...' : 'Créer la facture' }}
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

    :host ::ng-deep .asset-lines-table .p-dropdown {
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

  visible = false;
  submitting = false;
  purchaseOrderNumber = '';
  totalAmount = 0;
  showAssetClassification = false;
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
    purchaseOrderNumber: string;
    totalAmount: number;
    lines?: PurchaseOrderLine[];
    onClose?: () => void;
    onConfirm?: (request: CreateSupplierInvoiceRequest) => void;
  }): void {
    this.purchaseOrderNumber = config.purchaseOrderNumber;
    this.totalAmount = config.totalAmount;
    this.onClose = config.onClose;
    this.onConfirm = config.onConfirm;
    this.initLineClassifications(config.lines ?? []);
    this.generateInvoiceNumber();
    this.visible = true;
  }

  onHide(): void {
    this.visible = false;
    this.onClose?.();
    this.resetForm();
  }

  generateInvoiceNumber(): void {
    const now = new Date();
    const year = now.getFullYear();
    const timestamp = now.getTime().toString(36).toUpperCase().slice(-5);
    this.request.invoiceNumber = `FS-${year}-${timestamp}`;
  }

  onSubmit(): void {
    if (this.submitting || !this.request.invoiceNumber.trim()) {
      return;
    }

    this.submitting = true;
    const payload: CreateSupplierInvoiceRequest = {
      ...this.request,
      lineAssetClassifications: this.buildLineAssetClassifications()
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
    this.showAssetClassification = false;
    this.submitting = false;
  }

  private initLineClassifications(lines: PurchaseOrderLine[]): void {
    const receivedLines = lines.filter(l => l.receivedQuantity > 0);
    this.lineClassifications = receivedLines.map(l => ({
      lineNumber: l.lineNumber,
      productName: l.productName,
      subTotal: l.subTotal,
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
    return `Créer facture fournisseur - ${this.purchaseOrderNumber}`;
  }
}
