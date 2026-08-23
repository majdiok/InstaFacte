import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DatePickerModule } from 'primeng/datepicker';
import { Textarea } from 'primeng/textarea';
import { AutoCompleteModule, AutoCompleteCompleteEvent, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { CheckboxModule } from 'primeng/checkbox';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { WarehouseSelectorComponent } from '@shared/components/warehouse-selector/warehouse-selector.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { QuickCreateProductDialogComponent } from '@shared/components/quick-create-product-dialog/quick-create-product-dialog.component';
import { formatLocalDate } from '@core/utils/date.util';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { SupplierService, SupplierListItem } from '@core/services/supplier.service';
import { ProductService, ProductListItem } from '@core/services/product.service';
import {
  CreateStandaloneSupplierInvoiceRequest,
  SupplierInvoiceService
} from '@core/services/supplier-invoice.service';
import { AccountingFeatureFlagsService } from '@features/accounting/shared/accounting-feature-flags.service';

interface InvoiceLineRow {
  product: ProductListItem | null;
  productId: string | null;
  quantity: number;
  unitPriceHT: number;
  discountPercent: number;
  vatRate: number;
  isFixedAsset: boolean;
  assetAccountNumber: string;
}

@Component({
  selector: 'app-supplier-invoice-form',
  standalone: true,
  imports: [
    CommonModule, RouterModule, FormsModule, CurrencyPipe, DatePipe,
    SelectModule, InputTextModule, InputNumberModule, DatePickerModule,
    Textarea, AutoCompleteModule, CheckboxModule,
    PageHeaderComponent, BreadcrumbComponent, FormSectionComponent,
    WarehouseSelectorComponent, ButtonComponent, QuickCreateProductDialogComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <app-page-header
      title="Nouvelle facture fournisseur"
      subtitle="Saisissez une facture de charges ou de services. Le stock n'est pas mis à jour.">
      <app-button variant="outline" icon="pi-times" iconPos="left" (click)="cancel()">Annuler</app-button>
      <app-button
        variant="primary"
        [icon]="submitting() ? 'pi-spin pi-spinner' : 'pi-check'"
        iconPos="left"
        [disabled]="submitting() || !canSubmit()"
        (click)="submit()">
        Créer la facture
      </app-button>
    </app-page-header>

    <div class="info-banner" role="status">
      <i class="pi pi-info-circle"></i>
      <p>Cette facture ne met pas à jour le stock. Pour les marchandises gérées en stock, passez par un bon de réception.</p>
    </div>

    <div class="form-layout">
      <app-form-section title="Informations générales" icon="pi-building" [number]="1">
        <div class="form-grid">
          <div class="form-group">
            <label for="supplier">Fournisseur <span class="required">*</span></label>
            <p-select
              id="supplier"
              [options]="suppliers()"
              [(ngModel)]="selectedSupplierId"
              name="supplierId"
              optionLabel="name"
              optionValue="id"
              placeholder="Sélectionnez un fournisseur"
              [filter]="true"
              filterBy="name"
              [showClear]="true"
              (onChange)="onSupplierChange()"
              styleClass="w-full">
            </p-select>
          </div>

          <div class="form-group">
            <label for="invoiceDate">Date de facture <span class="required">*</span></label>
            <p-datepicker
              id="invoiceDate"
              [(ngModel)]="invoiceDate"
              name="invoiceDate"
              dateFormat="dd/mm/yy"
              [showIcon]="true"
              (ngModelChange)="onInvoiceDateChange()"
              styleClass="w-full">
            </p-datepicker>
          </div>

          <div class="form-group">
            <label for="paymentTermDays">Conditions de paiement</label>
            <p-select
              id="paymentTermDays"
              [options]="paymentTermOptions"
              [(ngModel)]="paymentTermDays"
              name="paymentTermDays"
              optionLabel="label"
              optionValue="value"
              styleClass="w-full">
            </p-select>
            <small class="hint">Échéance : {{ dueDate() | date:'dd/MM/yyyy' }}</small>
          </div>

          <div class="form-group">
            <label for="invoiceNumber">N° interne</label>
            <div class="input-group">
              <input
                pInputText
                id="invoiceNumber"
                [(ngModel)]="invoiceNumber"
                name="invoiceNumber"
                placeholder="FS-2026-000001"
                class="w-full"
                [disabled]="useSuggestedNumber"
                (ngModelChange)="onInvoiceNumberEdited()">
              <app-button
                type="button"
                variant="outline"
                size="sm"
                [icon]="regenerating() ? 'pi-spin pi-spinner' : 'pi-refresh'"
                iconPos="left"
                [disabled]="regenerating()"
                (click)="generateInvoiceNumber()">
                Générer
              </app-button>
            </div>
            <label class="hint-check">
              <p-checkbox
                [(ngModel)]="useSuggestedNumber"
                name="useSuggestedNumber"
                [binary]="true"
                inputId="useSuggestedNumber"
                (ngModelChange)="onUseSuggestedChange()">
              </p-checkbox>
              Utiliser le n° suggéré
            </label>
          </div>

          <div class="form-group">
            <label for="externalReference">N° facture fournisseur</label>
            <input
              pInputText
              id="externalReference"
              [(ngModel)]="externalReference"
              name="externalReference"
              placeholder="N° papier du fournisseur"
              class="w-full">
          </div>

          <div class="form-group">
            <label for="paymentMethod">Mode de paiement prévu</label>
            <p-select
              id="paymentMethod"
              [options]="paymentMethodOptions"
              [(ngModel)]="paymentMethod"
              name="paymentMethod"
              optionLabel="label"
              optionValue="value"
              [showClear]="true"
              placeholder="Optionnel"
              styleClass="w-full">
            </p-select>
          </div>

          <div class="form-group">
            <app-warehouse-selector
              inputId="si-warehouse"
              label="Entrepôt"
              [required]="false"
              [value]="selectedWarehouseId"
              (valueChange)="selectedWarehouseId = $event">
            </app-warehouse-selector>
          </div>
        </div>

        <div class="form-group">
          <label for="notes">Notes</label>
          <textarea pTextarea id="notes" [(ngModel)]="notes" name="notes" rows="2" class="w-full"></textarea>
        </div>
      </app-form-section>

      <app-form-section title="Lignes" icon="pi-list" [number]="2">
        <div class="lines-toolbar">
          <app-button type="button" variant="outline" size="sm" icon="pi-plus" iconPos="left" (click)="addLine()">
            Ajouter une ligne
          </app-button>
          <app-button type="button" variant="ghost" size="sm" icon="pi-plus-circle" iconPos="left" (click)="quickCreateVisible = true">
            Créer un article / service
          </app-button>
        </div>

        <table class="lines-table">
          <thead>
            <tr>
              <th>Article</th>
              <th class="col-qty">Qté</th>
              <th class="col-price">PU HT</th>
              <th class="col-disc">Remise %</th>
              <th class="col-vat">TVA</th>
              <th class="col-ht">HT</th>
              @if (fixedAssetsEnabled()) {
                <th class="col-immo">Immo</th>
              }
              <th class="col-actions"></th>
            </tr>
          </thead>
          <tbody>
            @for (line of lines; track $index; let i = $index) {
              <tr>
                <td>
                  <p-autoComplete
                    [(ngModel)]="line.product"
                    [ngModelOptions]="{ standalone: true }"
                    [suggestions]="productSuggestions()"
                    (completeMethod)="searchProducts($event)"
                    (onSelect)="onProductSelect(line, $event)"
                    field="name"
                    [dropdown]="true"
                    [forceSelection]="true"
                    [minLength]="0"
                    appendTo="body"
                    placeholder="Service ou charge (hors stock)"
                    styleClass="w-full"
                    inputStyleClass="w-full">
                    <ng-template let-product pTemplate="item">
                      <div class="product-item">
                        <span class="product-item__code">{{ product.code }}</span>
                        <span class="product-item__name">{{ product.name }}</span>
                      </div>
                    </ng-template>
                  </p-autoComplete>
                </td>
                <td>
                  <p-inputNumber [(ngModel)]="line.quantity" [ngModelOptions]="{ standalone: true }" [min]="0" [minFractionDigits]="0" [maxFractionDigits]="3" (ngModelChange)="recalculateTotals()" styleClass="w-full"></p-inputNumber>
                </td>
                <td>
                  <p-inputNumber [(ngModel)]="line.unitPriceHT" [ngModelOptions]="{ standalone: true }" [min]="0" [minFractionDigits]="3" [maxFractionDigits]="3" mode="decimal" (ngModelChange)="recalculateTotals()" styleClass="w-full"></p-inputNumber>
                </td>
                <td>
                  <p-inputNumber [(ngModel)]="line.discountPercent" [ngModelOptions]="{ standalone: true }" [min]="0" [max]="100" [minFractionDigits]="0" [maxFractionDigits]="2" (ngModelChange)="recalculateTotals()" styleClass="w-full"></p-inputNumber>
                </td>
                <td class="muted">{{ line.vatRate }} %</td>
                <td class="amount">{{ lineTotalHT(line) | currency:'TND':'symbol':'1.3-3' }}</td>
                @if (fixedAssetsEnabled()) {
                  <td>
                    <p-checkbox [(ngModel)]="line.isFixedAsset" [ngModelOptions]="{ standalone: true }" [binary]="true"></p-checkbox>
                  </td>
                }
                <td>
                  <app-button type="button" variant="ghost" size="sm" icon="pi-trash" [iconOnly]="true" ariaLabel="Supprimer" (click)="removeLine(i)"></app-button>
                </td>
              </tr>
            }
          </tbody>
        </table>

        <div class="totals">
          <div><span>Total HT</span><strong>{{ totalHT() | currency:'TND':'symbol':'1.3-3' }}</strong></div>
          <div><span>TVA</span><strong>{{ totalVat() | currency:'TND':'symbol':'1.3-3' }}</strong></div>
          <div class="ttc"><span>Total TTC</span><strong>{{ totalTTC() | currency:'TND':'symbol':'1.3-3' }}</strong></div>
        </div>
      </app-form-section>
    </div>

    <app-quick-create-product-dialog
      [(visible)]="quickCreateVisible"
      (productCreated)="onQuickProductCreated($event)">
    </app-quick-create-product-dialog>
  `,
  styles: [`
    .info-banner {
      display: flex;
      gap: var(--spacing-3);
      align-items: flex-start;
      padding: var(--spacing-3) var(--spacing-4);
      margin-bottom: var(--spacing-5);
      border-radius: var(--radius-lg);
      background: var(--color-primary-50);
      border: 1px solid var(--color-primary-200);
      color: var(--color-text-secondary);
    }
    .info-banner i { color: var(--color-primary-600); margin-top: 2px; }
    .info-banner p { margin: 0; font-size: var(--font-size-sm); }
    .form-layout { max-width: 1100px; }
    .form-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: var(--spacing-4);
    }
    .form-group { display: flex; flex-direction: column; gap: var(--spacing-2); }
    .form-group label {
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-xs);
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--color-text-secondary);
    }
    .required { color: var(--color-error-600); }
    .hint { color: var(--color-text-tertiary); font-size: var(--font-size-xs); }
    .hint-check { display: inline-flex; align-items: center; gap: var(--spacing-2); margin-top: var(--spacing-2); font-size: var(--font-size-xs); color: var(--color-text-secondary); font-weight: var(--font-weight-medium); text-transform: none; letter-spacing: 0; }
    .input-group { display: flex; gap: var(--spacing-2); }
    .input-group input { flex: 1; }
    .lines-toolbar { display: flex; gap: var(--spacing-2); margin-bottom: var(--spacing-3); }
    .lines-table { width: 100%; border-collapse: collapse; }
    .lines-table th {
      text-align: left;
      font-size: var(--font-size-xs);
      text-transform: uppercase;
      color: var(--color-text-secondary);
      padding: var(--spacing-2);
    }
    .lines-table td { padding: var(--spacing-2); vertical-align: middle; }
    .col-qty, .col-price, .col-disc { width: 110px; }
    .col-vat, .col-ht { width: 100px; }
    .col-immo, .col-actions { width: 56px; text-align: center; }
    .muted { color: var(--color-text-tertiary); font-size: var(--font-size-sm); }
    .amount { font-family: var(--font-family-mono, monospace); font-weight: 600; }
    .product-item { display: flex; gap: var(--spacing-2); }
    .product-item__code { font-family: monospace; color: var(--color-text-tertiary); }
    .totals {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-6);
      margin-top: var(--spacing-4);
      padding-top: var(--spacing-3);
      border-top: 1px solid var(--color-border-subtle);
    }
    .totals div { display: flex; flex-direction: column; align-items: flex-end; gap: 2px; }
    .totals .ttc strong { font-size: var(--font-size-lg); }
  `]
})
export class SupplierInvoiceFormComponent implements OnInit, OnDestroy {
  private router = inject(Router);
  private service = inject(SupplierInvoiceService);
  private supplierService = inject(SupplierService);
  private productService = inject(ProductService);
  private toast = inject(ToastService);
  private errors = inject(ErrorHandlerService);
  private warehouseContext = inject(WarehouseContextService);
  private accountingFlags = inject(AccountingFeatureFlagsService);
  private readonly destroy$ = new Subject<void>();
  private readonly search$ = new Subject<string>();

  breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Accueil', route: '/', icon: 'pi-home' },
    { label: 'Achats' },
    { label: 'Factures fournisseurs', route: '/supplier-invoices' },
    { label: 'Nouvelle facture' }
  ];

  suppliers = signal<SupplierListItem[]>([]);
  productSuggestions = signal<ProductListItem[]>([]);
  submitting = signal(false);
  regenerating = signal(false);
  totalHT = signal(0);
  totalVat = signal(0);
  totalTTC = signal(0);

  selectedSupplierId: string | null = null;
  selectedWarehouseId: string | null = null;
  invoiceDate: Date = new Date();
  paymentTermDays = 30;
  invoiceNumber = '';
  invoiceNumberManuallyEdited = false;
  useSuggestedNumber = true;
  externalReference = '';
  paymentMethod: string | null = null;
  notes = '';
  lines: InvoiceLineRow[] = [];
  quickCreateVisible = false;

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

  fixedAssetsEnabled = computed(() => this.accountingFlags.isEnabled('fixedAssetsEnabled'));

  dueDate(): Date {
    const d = new Date(this.invoiceDate ?? new Date());
    d.setDate(d.getDate() + (this.paymentTermDays || 0));
    return d;
  }

  ngOnInit(): void {
    this.selectedWarehouseId = this.warehouseContext.selectedWarehouseId();
    this.addLine();
    this.loadSuppliers();
    this.generateInvoiceNumber();
    this.search$.pipe(debounceTime(250), distinctUntilChanged(), takeUntil(this.destroy$)).subscribe(q => this.runProductSearch(q));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  canSubmit(): boolean {
    return !!this.selectedSupplierId
      && this.lines.some(l => !!l.productId && l.quantity > 0)
      && (this.useSuggestedNumber || !!this.invoiceNumber.trim());
  }

  cancel(): void {
    void this.router.navigate(['/supplier-invoices']);
  }

  onSupplierChange(): void {
    const supplier = this.suppliers().find(s => s.id === this.selectedSupplierId);
    if (!supplier?.paymentTermDays) return;
    this.ensurePaymentTermOption(supplier.paymentTermDays);
    this.paymentTermDays = supplier.paymentTermDays;
  }

  onInvoiceDateChange(): void {
    if (this.useSuggestedNumber) {
      this.generateInvoiceNumber();
    }
  }

  onInvoiceNumberEdited(): void {
    this.invoiceNumberManuallyEdited = true;
    this.useSuggestedNumber = false;
  }

  onUseSuggestedChange(): void {
    if (this.useSuggestedNumber) {
      this.generateInvoiceNumber();
    }
  }

  generateInvoiceNumber(): void {
    this.regenerating.set(true);
    this.service.previewNumber(this.invoiceDate ?? new Date()).subscribe({
      next: res => {
        if (res.success && res.data) {
          this.invoiceNumber = res.data;
          this.invoiceNumberManuallyEdited = false;
          this.useSuggestedNumber = true;
        }
        this.regenerating.set(false);
      },
      error: () => this.regenerating.set(false)
    });
  }

  addLine(): void {
    this.lines = [...this.lines, {
      product: null,
      productId: null,
      quantity: 1,
      unitPriceHT: 0,
      discountPercent: 0,
      vatRate: 19,
      isFixedAsset: false,
      assetAccountNumber: ''
    }];
  }

  removeLine(index: number): void {
    this.lines = this.lines.filter((_, i) => i !== index);
    if (this.lines.length === 0) this.addLine();
    this.recalculateTotals();
  }

  searchProducts(event: AutoCompleteCompleteEvent): void {
    this.search$.next(event.query ?? '');
  }

  onProductSelect(line: InvoiceLineRow, event: AutoCompleteSelectEvent): void {
    const product = event.value as ProductListItem;
    if (product.isStockManaged) {
      this.toast.add({
        severity: 'warn',
        summary: 'Stock',
        detail: 'Cet article est géré en stock. Utilisez un bon de réception.'
      });
      line.product = null;
      line.productId = null;
      return;
    }
    line.product = product;
    line.productId = product.id;
    line.unitPriceHT = product.purchasePrice ?? product.unitPrice ?? 0;
    line.vatRate = product.vatRate ?? 19;
    this.recalculateTotals();
  }

  onQuickProductCreated(product: ProductListItem): void {
    if (product.isStockManaged) {
      this.toast.add({
        severity: 'warn',
        summary: 'Stock',
        detail: 'L’article créé est géré en stock et ne peut pas figurer sur une facture directe.'
      });
      return;
    }
    const empty = this.lines.find(l => !l.productId) ?? this.addEmptyAndReturn();
    empty.product = product;
    empty.productId = product.id;
    empty.unitPriceHT = product.purchasePrice ?? product.unitPrice ?? 0;
    empty.vatRate = product.vatRate ?? 19;
    this.recalculateTotals();
  }

  lineTotalHT(line: InvoiceLineRow): number {
    const discount = (line.discountPercent ?? 0) / 100;
    return line.quantity * line.unitPriceHT * (1 - discount);
  }

  recalculateTotals(): void {
    let ht = 0;
    let vat = 0;
    for (const line of this.lines) {
      const lineHt = this.lineTotalHT(line);
      ht += lineHt;
      vat += lineHt * ((line.vatRate ?? 0) / 100);
    }
    this.totalHT.set(ht);
    this.totalVat.set(vat);
    this.totalTTC.set(ht + vat);
  }

  submit(): void {
    if (!this.canSubmit() || !this.selectedSupplierId || !this.invoiceDate) return;
    this.submitting.set(true);

    const payload: CreateStandaloneSupplierInvoiceRequest = {
      supplierId: this.selectedSupplierId,
      invoiceNumber: this.invoiceNumber.trim() || undefined,
      invoiceDate: formatLocalDate(this.invoiceDate),
      paymentTermDays: this.paymentTermDays,
      externalReference: this.externalReference.trim() || undefined,
      notes: this.notes.trim() || undefined,
      paymentMethod: this.paymentMethod || undefined,
      warehouseId: this.selectedWarehouseId || undefined,
      useSuggestedNumber: this.useSuggestedNumber,
      lines: this.lines
        .filter(l => !!l.productId && l.quantity > 0)
        .map(l => ({
          productId: l.productId!,
          quantity: l.quantity,
          unitPriceHt: l.unitPriceHT,
          discountPercent: l.discountPercent || undefined,
          isFixedAsset: l.isFixedAsset || undefined,
          assetAccountNumber: l.isFixedAsset && l.assetAccountNumber.trim()
            ? l.assetAccountNumber.trim()
            : undefined
        }))
    };

    this.service.create(payload).subscribe({
      next: res => {
        this.submitting.set(false);
        if (!res.success || !res.data) {
          this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Création impossible.' });
          return;
        }
        this.toast.add({
          severity: 'success',
          summary: 'Facture créée',
          detail: `${res.data.invoiceNumber} — joignez le scan à l’écriture comptable d’achat.`
        });
        void this.router.navigate(['/supplier-invoices', res.data.id]);
      },
      error: err => {
        this.submitting.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: this.errors.extractErrorMessage(err)
        });
      }
    });
  }

  private loadSuppliers(): void {
    this.supplierService.getActiveSuppliers().subscribe({
      next: res => {
        if (res.success && res.data) this.suppliers.set(res.data.items);
      }
    });
  }

  private runProductSearch(query: string): void {
    this.productService.getProducts({ search: query || undefined, isActive: true, page: 1, pageSize: 50 }).subscribe({
      next: res => {
        const items = res.success && res.data ? res.data.items.filter(p => !p.isStockManaged) : [];
        this.productSuggestions.set(items);
      },
      error: () => this.productSuggestions.set([])
    });
  }

  private addEmptyAndReturn(): InvoiceLineRow {
    this.addLine();
    return this.lines[this.lines.length - 1];
  }

  private ensurePaymentTermOption(days: number): void {
    if (this.paymentTermOptions.some(o => o.value === days)) return;
    this.paymentTermOptions = [
      ...this.paymentTermOptions,
      { label: `${days} jours`, value: days }
    ];
  }
}
