import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, forkJoin, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map, switchMap } from 'rxjs/operators';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { Textarea } from 'primeng/textarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { WarehouseSelectorComponent } from '@shared/components/warehouse-selector/warehouse-selector.component';
import { StockAllocationEditorComponent } from '@shared/components/stock-allocation-editor/stock-allocation-editor.component';
import {
  AllocationRow,
  buildAllocationPayload,
  showLotSection,
  showSerialSection,
  createDefaultLotRow,
  createDefaultSerialRow,
  TRACKING_MODE_SERIAL
} from '@shared/utils/stock-traceability.utils';
import { StockFeatures } from '@core/services/stock.service';
import { ProductListItem, ProductService } from '@core/services/product.service';
import { StockService } from '@core/services/stock.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { formatLocalDate } from '@core/utils/date.util';
import {
  CreateStockVoucherRequest,
  StockVoucherKindName,
  StockVoucherLineAllocations,
  StockVoucherLineDto,
  StockVoucherService,
  UpdateStockVoucherRequest,
  isStockVoucherDraft,
  movementReasonName
} from '@core/services/stock-voucher.service';

interface VoucherLine {
  id: string | null;
  productId: string;
  productCode: string;
  productName: string;
  unit: string;
  quantity: number;
  unitCost: number;
  notes: string;
  lotAllocations: AllocationRow[];
  trackingMode?: number;
  pickingPolicy?: number;
}

@Component({
  selector: 'app-stock-voucher-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    CurrencyPipe,
    DatePickerModule,
    InputTextModule,
    Textarea,
    InputNumberModule,
    SelectModule,
    AutoCompleteModule,
    ToastModule,
    PageHeaderComponent,
    FormSectionComponent,
    ButtonComponent,
    BreadcrumbComponent,
    WarehouseSelectorComponent,
    StockAllocationEditorComponent
  ],
  providers: [MessageService],
  template: `
    <p-toast></p-toast>
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>
    <div class="page-container">
      <app-page-header [title]="pageTitle" [subtitle]="pageSubtitle">
        <app-button variant="outline" icon="pi-times" iconPos="left" [routerLink]="listRoute">
          Retour
        </app-button>
      </app-page-header>

      <app-form-section title="En-tête" icon="pi-info-circle" [number]="1">
        <div class="form-row">
          <div class="form-group">
            <app-warehouse-selector
              inputId="voucher-warehouse"
              label="Dépôt"
              [required]="true"
              [activeOnly]="true"
              [value]="warehouseId"
              (valueChange)="onWarehouseChange($event)">
            </app-warehouse-selector>
          </div>
          <div class="form-group">
            <label for="voucher-date">Date <span class="required">*</span></label>
            <p-datepicker
              inputId="voucher-date"
              [(ngModel)]="voucherDate"
              dateFormat="dd/mm/yy"
              [showIcon]="true"
              styleClass="w-full">
            </p-datepicker>
          </div>
          <div class="form-group">
            <label for="voucher-reason">Motif <span class="required">*</span></label>
            <p-select
              inputId="voucher-reason"
              [options]="reasonOptions"
              [(ngModel)]="reason"
              optionLabel="label"
              optionValue="value"
              [style]="{ width: '100%' }"
              appendTo="body">
            </p-select>
          </div>
        </div>
        @if (isEntry && reason === 'Purchase') {
          <div class="alert-banner" role="note">
            <i class="pi pi-info-circle"></i>
            <p>
              Pour un achat fournisseur, utilisez <strong>Achats &gt; Bons de réception</strong>.
              Ce motif enregistre une entrée de stock sans BR, sans commande et sans facture.
            </p>
          </div>
        }
        <div class="form-row">
          <div class="form-group">
            <label for="voucher-ref">Référence externe</label>
            <input id="voucher-ref" type="text" pInputText [(ngModel)]="externalReference" maxlength="100" class="w-full" />
          </div>
        </div>
        <div class="form-group">
          <label for="voucher-notes">Notes</label>
          <textarea id="voucher-notes" pTextarea [(ngModel)]="notes" [rows]="3" class="w-full"></textarea>
        </div>
      </app-form-section>

      <app-form-section title="Lignes" icon="pi-list" [number]="2">
        <div class="line-add">
          <div class="form-group product-search-wrap">
            <label for="voucher-product">Produit (géré en stock)</label>
            <p-autoComplete
              inputId="voucher-product"
              [(ngModel)]="selectedProduct"
              [suggestions]="productSuggestions()"
              (completeMethod)="onProductSearch($event)"
              field="name"
              [dropdown]="true"
              [forceSelection]="true"
              [minLength]="0"
              placeholder="Rechercher par nom ou code…"
              appendTo="body"
              [style]="{ width: '100%' }">
              <ng-template let-product pTemplate="item">
                <div class="product-suggestion">
                  <span class="product-code">{{ product.code }}</span>
                  <span>{{ product.name }}</span>
                  @if (!product.isStockManaged) {
                    <span class="badge-non-stock">Non géré en stock</span>
                  }
                </div>
              </ng-template>
            </p-autoComplete>
          </div>
          <div class="form-group">
            <label for="voucher-qty">Quantité</label>
            <p-inputNumber
              inputId="voucher-qty"
              [(ngModel)]="selectedQuantity"
              [min]="0.001"
              [minFractionDigits]="0"
              [maxFractionDigits]="3"
              mode="decimal"
              [style]="{ width: '120px' }">
            </p-inputNumber>
          </div>
          @if (isEntry) {
            <div class="form-group">
              <label for="voucher-cost">Coût unitaire</label>
              <p-inputNumber
                inputId="voucher-cost"
                [(ngModel)]="selectedUnitCost"
                [min]="0"
                mode="decimal"
                [minFractionDigits]="3"
                [maxFractionDigits]="3"
                [style]="{ width: '140px' }">
              </p-inputNumber>
            </div>
          }
          <div class="btn-add-wrap">
            <app-button type="button" variant="primary" icon="pi-plus" iconPos="left"
                        (click)="addLine()" [disabled]="!selectedProduct || selectedQuantity <= 0">
              Ajouter
            </app-button>
          </div>
        </div>

        @if (lines.length === 0) {
          <div class="empty-lines">
            <i class="pi pi-box"></i>
            <p>Ajoutez au moins un produit géré en stock.</p>
          </div>
        } @else {
          <div class="lines-table-wrap">
            <table class="lines-table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Produit</th>
                  <th>Unité</th>
                  @if (!isEntry) {
                    <th>Dispo</th>
                    <th>Coût unitaire</th>
                  }
                  <th>Qté</th>
                  @if (isEntry) {
                    <th>Coût</th>
                  }
                  <th>Valorisation</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (line of lines; track line.productId; let i = $index) {
                  <tr>
                    <td>{{ line.productCode }}</td>
                    <td>{{ line.productName }}</td>
                    <td>{{ line.unit }}</td>
                    @if (!isEntry) {
                      <td>{{ formatAvailability(line.productId) }}</td>
                      <td>{{ formatAverageCost(line.productId) | currency:'TND':'symbol':'1.3-3' }}</td>
                    }
                    <td>
                      <p-inputNumber
                        [(ngModel)]="line.quantity"
                        [min]="0.001"
                        [minFractionDigits]="0"
                        [maxFractionDigits]="3"
                        mode="decimal">
                      </p-inputNumber>
                      @if (!isEntry && qtyExceedsAvailable(line)) {
                        <span class="qty-warning"><i class="pi pi-exclamation-triangle"></i> Supérieur au disponible</span>
                      }
                    </td>
                    @if (isEntry) {
                      <td>
                        <p-inputNumber
                          [(ngModel)]="line.unitCost"
                          [min]="0"
                          mode="decimal"
                          [minFractionDigits]="3"
                          [maxFractionDigits]="3">
                        </p-inputNumber>
                      </td>
                    }
                    <td>{{ (line.quantity * line.unitCost) | currency:'TND':'symbol':'1.3-3' }}</td>
                    <td>
                      <button type="button" class="btn-icon" (click)="removeLine(i)" aria-label="Retirer la ligne">
                        <i class="pi pi-trash"></i>
                      </button>
                    </td>
                  </tr>
                  @if (showLineTraceability(line)) {
                    <tr>
                      <td [attr.colspan]="isEntry ? 7 : 8">
                        <app-stock-allocation-editor
                          [mode]="isEntry ? 'entry' : 'exit'"
                          [productId]="line.productId"
                          [warehouseId]="warehouseId!"
                          [lineQuantity]="line.quantity"
                          [trackingMode]="line.trackingMode ?? 0"
                          [pickingPolicy]="line.pickingPolicy ?? 0"
                          [features]="stockFeatures()"
                          [allocations]="line.lotAllocations"
                          [optionalOverride]="!isEntry">
                        </app-stock-allocation-editor>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        }

        <div class="totals">
          <span>Quantité : <strong>{{ totalQuantity | number:'1.0-3' }}</strong></span>
          <span>Valorisation : <strong>{{ totalValue | currency:'TND':'symbol':'1.3-3' }}</strong></span>
        </div>
      </app-form-section>

      <div class="form-actions">
        <app-button type="button" variant="outline" (click)="cancel()">Annuler</app-button>
        <app-button type="button" variant="secondary" icon="pi-save" iconPos="left"
                    (click)="save(false)" [disabled]="!canSubmit()">
          Enregistrer brouillon
        </app-button>
        <app-button type="button" variant="primary" icon="pi-check" iconPos="left"
                    (click)="save(true)" [disabled]="!canSubmit()">
          Enregistrer et valider
        </app-button>
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 0 0 2rem; }
    .form-row { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1rem; }
    @media (max-width: 768px) { .form-row { grid-template-columns: 1fr; } }
    .form-group { margin-bottom: 1rem; }
    .form-group label { display: block; font-size: 0.875rem; font-weight: 500; margin-bottom: 0.375rem; }
    .required { color: #dc2626; }
    .alert-banner {
      display: flex; gap: 0.75rem; padding: 0.75rem 1rem; background: #eff6ff;
      border: 1px solid #bfdbfe; border-radius: 0.5rem; margin-bottom: 1rem; color: #1e3a8a;
    }
    .alert-banner p { margin: 0; font-size: 0.875rem; }
    .line-add { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: end; margin-bottom: 1rem; }
    .product-search-wrap { flex: 1; min-width: 240px; }
    .btn-add-wrap { padding-bottom: 0.25rem; }
    .lines-table-wrap { overflow-x: auto; }
    .lines-table { width: 100%; border-collapse: collapse; min-width: 720px; }
    .lines-table th, .lines-table td { padding: 0.625rem; border-bottom: 1px solid #e2e8f0; text-align: left; vertical-align: middle; }
    .lines-table th { font-size: 0.75rem; text-transform: uppercase; color: #64748b; }
    .empty-lines { text-align: center; padding: 2rem; color: #64748b; }
    .product-suggestion { display: flex; gap: 0.5rem; align-items: center; }
    .product-code { font-weight: 600; }
    .badge-non-stock { background: #fef3c7; color: #92400e; padding: 0.125rem 0.375rem; border-radius: 0.25rem; font-size: 0.75rem; }
    .qty-warning { display: block; color: #b45309; font-size: 0.75rem; margin-top: 0.25rem; }
    .btn-icon { background: none; border: none; color: #64748b; cursor: pointer; }
    .lot-alloc {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: center;
      padding: 0.5rem 0;
    }
    .lot-alloc-label { font-size: 0.75rem; font-weight: 600; color: #64748b; margin-right: 0.5rem; }
    .totals { display: flex; gap: 1.5rem; justify-content: flex-end; margin-top: 1rem; font-size: 0.95rem; }
    .form-actions { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.5rem; flex-wrap: wrap; }
    :host ::ng-deep .p-datepicker { display: flex; width: 100%; }
  `]
})
export class StockVoucherFormComponent implements OnInit {
  private voucherService = inject(StockVoucherService);
  private productService = inject(ProductService);
  private stockService = inject(StockService);
  private warehouseContext = inject(WarehouseContextService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private messageService = inject(MessageService);
  private errorHandler = inject(ErrorHandlerService);
  private destroyRef = inject(DestroyRef);
  private readonly productSearch$ = new Subject<string>();

  readonly isEntry = (this.route.snapshot.data['kind'] as StockVoucherKindName) !== 'Issue';
  readonly kind: StockVoucherKindName = this.isEntry ? 'Entry' : 'Issue';
  readonly listRoute = this.isEntry ? '/stock/entries' : '/stock/issues';
  readonly pageTitle = this.route.snapshot.paramMap.get('id')
    ? (this.isEntry ? 'Modifier le bon d’entrée' : 'Modifier le bon de sortie')
    : (this.isEntry ? 'Nouveau bon d’entrée' : 'Nouveau bon de sortie');
  readonly pageSubtitle = this.isEntry
    ? 'Le stock n’est mis à jour qu’à la validation.'
    : 'La sortie est refusée si la quantité dépasse le disponible.';

  breadcrumbItems: BreadcrumbItem[] = [];
  editId: string | null = null;
  warehouseId: string | null = null;
  voucherDate: Date = new Date();
  reason = this.isEntry ? 'InitialStock' : 'Damage';
  externalReference = '';
  notes = '';
  lines: VoucherLine[] = [];
  selectedProduct: ProductListItem | null = null;
  selectedQuantity = 1;
  selectedUnitCost = 0;
  submitting = signal(false);
  productSuggestions = signal<ProductListItem[]>([]);
  stockByProductId = signal<Map<string, { qty: number; avgCost: number }>>(new Map());
  stockFeatures = signal<StockFeatures | null>(null);
  traceabilityByProduct = signal<Map<string, { trackingMode: number; pickingPolicy: number }>>(new Map());

  readonly reasonOptions = this.isEntry
    ? [
        { label: 'Stock initial', value: 'InitialStock' },
        { label: 'Retour client (hors BRT / avoir)', value: 'CustomerReturn' },
        { label: 'Achat hors réception', value: 'Purchase' },
        { label: 'Trouvé / autre', value: 'FoundOrOther' }
      ]
    : [
        { label: 'Dommage / perte', value: 'Damage' },
        { label: 'Retour fournisseur (hors flux achat)', value: 'SupplierReturn' },
        { label: 'Consommation interne', value: 'InternalUse' },
        { label: 'Don / échantillon', value: 'GiftOrSample' }
      ];

  get totalQuantity(): number {
    return this.lines.reduce((acc, l) => acc + (l.quantity || 0), 0);
  }

  get totalValue(): number {
    return this.lines.reduce((acc, l) => acc + (l.quantity || 0) * (l.unitCost || 0), 0);
  }

  ngOnInit(): void {
    this.editId = this.route.snapshot.paramMap.get('id');
    this.breadcrumbItems = [
      { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
      { label: 'Stock', route: '/stock' },
      { label: this.isEntry ? "Bons d'entrée" : 'Bons de sortie', route: this.listRoute },
      { label: this.editId ? 'Modifier' : 'Nouveau' }
    ];

    this.stockService.getFeatures().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        if (res.success && res.data) this.stockFeatures.set(res.data);
      }
    });

    this.productSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((query) =>
          this.productService.getProducts({
            search: query.trim() || undefined,
            isActive: true,
            page: 1,
            pageSize: 50,
            warehouseId: this.warehouseId ?? undefined
          }).pipe(catchError(() => of({ success: false as const, data: undefined })))
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((res) => {
        const items = res.success && res.data?.items ? res.data.items.filter(p => p.isStockManaged) : [];
        this.productSuggestions.set(items);
      });

    if (this.editId) {
      this.loadExisting(this.editId);
    } else {
      this.warehouseId = this.warehouseContext.selectedWarehouseId();
      if (this.warehouseId) this.loadStock();
    }
  }

  onWarehouseChange(id: string | null): void {
    this.warehouseId = id;
    this.loadStock();
    this.loadTraceabilityContext();
  }

  showLineTraceability(line: VoucherLine): boolean {
    const f = this.stockFeatures();
    if (!f || !this.warehouseId) return false;
    const mode = line.trackingMode ?? 0;
    return showLotSection(mode, f) || showSerialSection(mode, f);
  }

  private loadTraceabilityContext(): void {
    if (!this.warehouseId || this.lines.length === 0) return;
    const productIds = this.lines.map(l => l.productId);
    this.stockService.getTraceabilityContext(productIds, this.warehouseId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(res => {
        if (!res.success || !res.data) return;
        const map = new Map(this.traceabilityByProduct());
        for (const ctx of res.data) {
          map.set(ctx.productId, { trackingMode: ctx.trackingMode, pickingPolicy: ctx.pickingPolicy });
        }
        this.traceabilityByProduct.set(map);
        for (const line of this.lines) {
          const ctx = map.get(line.productId);
          if (ctx) {
            line.trackingMode = ctx.trackingMode;
            line.pickingPolicy = ctx.pickingPolicy;
          }
        }
      });
  }

  private defaultAllocationsForLine(product: ProductListItem, quantity: number): AllocationRow[] {
    const mode = product.trackingMode ?? 0;
    if (mode === TRACKING_MODE_SERIAL) {
      const count = Math.max(1, Math.round(quantity));
      return Array.from({ length: count }, () => createDefaultSerialRow());
    }
    return [createDefaultLotRow(quantity)];
  }

  onProductSearch(event: AutoCompleteCompleteEvent): void {
    this.productSearch$.next(event.query ?? '');
  }

  addLine(): void {
    const product = this.selectedProduct;
    if (!product || this.selectedQuantity <= 0) return;
    if (!product.isStockManaged) {
      this.messageService.add({ severity: 'warn', summary: 'Produit', detail: 'Seuls les produits gérés en stock sont autorisés.' });
      return;
    }
    if (this.lines.some(l => l.productId === product.id)) {
      this.messageService.add({ severity: 'warn', summary: 'Attention', detail: 'Ce produit est déjà sur le bon.' });
      return;
    }
    const stock = this.stockByProductId().get(product.id);
    const unitCost = this.isEntry
      ? (this.selectedUnitCost || product.lastPurchasePrice || product.purchasePrice || product.weightedAverageCost || 0)
      : (stock?.avgCost ?? product.weightedAverageCost ?? product.lastPurchasePrice ?? 0);
    this.lines.push({
      id: null,
      productId: product.id,
      productCode: product.code,
      productName: product.name,
      unit: product.unit || 'Unité',
      quantity: this.selectedQuantity,
      unitCost,
      notes: '',
      trackingMode: product.trackingMode ?? 0,
      pickingPolicy: this.traceabilityByProduct().get(product.id)?.pickingPolicy ?? 0,
      lotAllocations: this.defaultAllocationsForLine(product, this.selectedQuantity)
    });
    this.loadTraceabilityContext();
    this.selectedProduct = null;
    this.selectedQuantity = 1;
    this.selectedUnitCost = 0;
  }

  removeLine(index: number): void {
    this.lines.splice(index, 1);
  }

  addLotAllocation(line: VoucherLine): void {
    line.lotAllocations.push({ lotNumber: '', expiryDate: null, quantity: 0 });
  }

  removeLotAllocation(lineIndex: number, allocIndex: number): void {
    const line = this.lines[lineIndex];
    if (!line || line.lotAllocations.length <= 1) return;
    line.lotAllocations.splice(allocIndex, 1);
  }

  private buildLotAllocations(serverLines: StockVoucherLineDto[]): StockVoucherLineAllocations[] | undefined {
    const payload: StockVoucherLineAllocations[] = [];
    this.lines.forEach((line, i) => {
      const lineId = line.id || serverLines[i]?.id;
      const allocations = buildAllocationPayload(line.lotAllocations ?? []);
      if (lineId && allocations.length > 0) {
        payload.push({ lineId, allocations });
      }
    });
    return payload.length > 0 ? payload : undefined;
  }

  private hasTrackedLines(): boolean {
    const f = this.stockFeatures();
    if (!f) return false;
    return this.lines.some(l => this.showLineTraceability(l));
  }

  formatAvailability(productId: string): string {
    const q = this.stockByProductId().get(productId)?.qty;
    return q == null ? '—' : q.toLocaleString('fr-FR', { maximumFractionDigits: 3 });
  }

  formatAverageCost(productId: string): number {
    return this.stockByProductId().get(productId)?.avgCost ?? 0;
  }

  qtyExceedsAvailable(line: VoucherLine): boolean {
    const avail = this.stockByProductId().get(line.productId)?.qty;
    return avail != null && line.quantity > avail;
  }

  canSubmit(): boolean {
    return !!this.warehouseId && !!this.reason && this.lines.length > 0
      && this.lines.every(l => l.quantity > 0 && l.unitCost >= 0) && !this.submitting();
  }

  cancel(): void {
    void this.router.navigate([this.listRoute]);
  }

  save(andValidate: boolean): void {
    if (!this.canSubmit() || !this.warehouseId) return;
    if (andValidate && !this.isEntry && this.lines.some(l => this.qtyExceedsAvailable(l))) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Stock insuffisant',
        detail: 'Une quantité dépasse le disponible. Ajustez les lignes avant validation.'
      });
      return;
    }

    const payloadLines = this.lines.map(l => ({
      productId: l.productId,
      quantity: l.quantity,
      unitCost: this.isEntry ? l.unitCost : l.unitCost,
      notes: l.notes?.trim() || undefined
    }));

    this.submitting.set(true);
    if (this.editId) {
      const request: UpdateStockVoucherRequest = {
        voucherDate: formatLocalDate(this.voucherDate),
        warehouseId: this.warehouseId,
        reason: this.reason,
        externalReference: this.externalReference || undefined,
        notes: this.notes || undefined,
        lines: payloadLines
      };
      this.voucherService.update(this.editId, request).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => this.afterSave(this.editId!, andValidate),
        error: (err) => this.onSaveError(err)
      });
    } else {
      const request: CreateStockVoucherRequest = {
        kind: this.kind,
        voucherDate: formatLocalDate(this.voucherDate),
        warehouseId: this.warehouseId,
        reason: this.reason,
        externalReference: this.externalReference || undefined,
        notes: this.notes || undefined,
        lines: payloadLines
      };
      this.voucherService.create(request).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (res) => {
          const id = res.data;
          if (!id) {
            this.submitting.set(false);
            return;
          }
          this.afterSave(id, andValidate);
        },
        error: (err) => this.onSaveError(err)
      });
    }
  }

  private afterSave(id: string, andValidate: boolean): void {
    if (!andValidate) {
      this.submitting.set(false);
      this.messageService.add({ severity: 'success', summary: 'Enregistré', detail: 'Brouillon enregistré.' });
      void this.router.navigate([this.listRoute, id]);
      return;
    }
    const validate$ = this.hasTrackedLines()
      ? this.voucherService.getStockVoucher(id).pipe(
          map(res => this.buildLotAllocations(res.data?.lines ?? [])),
          switchMap(alloc => this.voucherService.validate(id, alloc))
        )
      : this.voucherService.validate(id);
    validate$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.submitting.set(false);
        this.messageService.add({ severity: 'success', summary: 'Validé', detail: 'Le stock a été mis à jour.' });
        void this.router.navigate([this.listRoute, id]);
      },
      error: (err) => {
        this.submitting.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Enregistré, validation refusée',
          detail: this.errorHandler.extractErrorMessage(err)
        });
        void this.router.navigate([this.listRoute, id]);
      }
    });
  }

  private onSaveError(err: unknown): void {
    this.submitting.set(false);
    this.messageService.add({
      severity: 'error',
      summary: 'Erreur',
      detail: this.errorHandler.extractErrorMessage(err)
    });
  }

  private loadExisting(id: string): void {
    this.voucherService.getStockVoucher(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        const v = res.data;
        if (!v || !isStockVoucherDraft(v.status)) {
          this.messageService.add({ severity: 'warn', summary: 'Lecture seule', detail: 'Seuls les brouillons sont modifiables.' });
          void this.router.navigate([this.listRoute, id]);
          return;
        }
        this.warehouseId = v.warehouseId;
        this.voucherDate = new Date(v.voucherDate);
        this.reason = movementReasonName(v.reason);
        this.externalReference = v.externalReference ?? '';
        this.notes = v.notes ?? '';
        this.lines = v.lines.map(l => ({
          id: l.id,
          productId: l.productId,
          productCode: l.productCode,
          productName: l.productName,
          unit: l.unit || 'Unité',
          quantity: l.quantity,
          unitCost: l.unitCost,
          notes: l.notes ?? '',
          lotAllocations: [{ lotNumber: '', expiryDate: null, quantity: l.quantity }]
        }));
        this.loadStock();
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Erreur',
          detail: this.errorHandler.extractErrorMessage(err)
        });
        void this.router.navigate([this.listRoute]);
      }
    });
  }

  private loadStock(): void {
    const wid = this.warehouseId;
    if (!wid) {
      this.stockByProductId.set(new Map());
      return;
    }
    this.stockService.getStockItems(wid, undefined, undefined, 1, 1000).pipe(
      switchMap((firstRes) => {
        const merged = new Map<string, { qty: number; avgCost: number }>();
        if (!firstRes.success || !firstRes.data) return of(merged);
        for (const it of firstRes.data.items) {
          merged.set(it.productId, { qty: it.quantityAvailable, avgCost: it.averageCost });
        }
        const totalPages = Math.max(1, Math.ceil(firstRes.data.totalCount / 1000));
        if (totalPages <= 1) return of(merged);
        const rest = [];
        for (let p = 2; p <= totalPages; p++) {
          rest.push(this.stockService.getStockItems(wid, undefined, undefined, p, 1000));
        }
        return forkJoin(rest).pipe(map((results) => {
          for (const r of results) {
            if (r.success && r.data?.items) {
              for (const it of r.data.items) {
                merged.set(it.productId, { qty: it.quantityAvailable, avgCost: it.averageCost });
              }
            }
          }
          return merged;
        }));
      }),
      catchError(() => of(new Map<string, { qty: number; avgCost: number }>())),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((mapValue) => this.stockByProductId.set(mapValue));
  }
}
