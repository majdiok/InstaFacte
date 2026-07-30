import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextarea } from 'primeng/inputtextarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { SalesOrderService, CreateSalesOrderLineRequest } from '@core/services/sales-order.service';
import { PricingService, PriceSource } from '@core/services/pricing.service';
import { ClientService } from '@core/services/client.service';
import { ProductService } from '@core/services/product.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';

interface ClientOption {
  id: string;
  label: string;
}

interface ProductOption {
  id: string;
  code: string;
  name: string;
  label: string;
  unitPrice: number;
}

/** Ligne en cours de saisie, avec la trace de la résolution de prix. */
interface DraftLine {
  product: ProductOption;
  quantity: number;
  unitPrice: number;
  discountPercent: number | null;
  notes: string | null;
  /** Origine du prix proposé par le serveur — sert à signaler un prix négocié. */
  priceSource: PriceSource | null;
  /** Vrai si l'utilisateur a écrasé le prix résolu : on n'y retouche plus. */
  priceOverridden: boolean;
}

@Component({
  selector: 'app-sales-order-create',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    InputTextarea,
    InputNumberModule,
    AutoCompleteModule,
    TagModule,
    TooltipModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    ButtonComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <app-page-header
      title="Nouvelle commande client"
      subtitle="Les prix sont ceux que le serveur appliquera : prix négocié, grille du client, ou catalogue.">
      <app-button variant="secondary" routerLink="/sales-orders">Annuler</app-button>
      <app-button variant="primary" [disabled]="!canSubmit() || saving()" (clicked)="submit()">
        Créer la commande
      </app-button>
    </app-page-header>

    <!-- En-tête -->
    <div class="ft-panel">
      <h3 class="ft-panel__title">Informations</h3>
      <div class="ft-form-grid">
        <div class="ft-field">
          <label for="so-client">Client <span class="ft-required">*</span></label>
          <p-autoComplete
            inputId="so-client"
            [(ngModel)]="selectedClient"
            [suggestions]="clientSuggestions()"
            (completeMethod)="searchClients($event)"
            (onSelect)="onClientChange()"
            (onClear)="onClientChange()"
            field="label"
            [forceSelection]="true"
            [dropdown]="true"
            placeholder="Rechercher un client"
            appendTo="body"></p-autoComplete>
          <small class="ft-hint">
            Le client détermine la grille tarifaire appliquée aux lignes.
          </small>
        </div>

        <div class="ft-field">
          <label for="so-date">Date de commande <span class="ft-required">*</span></label>
          <input id="so-date" type="date" pInputText [(ngModel)]="orderDate" (change)="onDateChange()" />
        </div>

        <div class="ft-field">
          <label for="so-delivery">Livraison prévue</label>
          <input id="so-delivery" type="date" pInputText [(ngModel)]="expectedDeliveryDate" />
        </div>

        <div class="ft-field">
          <label for="so-ref">Référence</label>
          <input id="so-ref" pInputText [(ngModel)]="reference" maxlength="100"
                 placeholder="Bon de commande du client" />
        </div>

        <div class="ft-field">
          <label for="so-terms">Conditions de règlement</label>
          <input id="so-terms" pInputText [(ngModel)]="paymentTerms" maxlength="500" />
        </div>

        <div class="ft-field ft-field--full">
          <label for="so-notes">Notes</label>
          <textarea id="so-notes" pInputTextarea [(ngModel)]="notes" rows="2" maxlength="2000"></textarea>
        </div>
      </div>
    </div>

    <!-- Lignes -->
    <div class="ft-panel">
      <h3 class="ft-panel__title">Lignes</h3>

      <div class="ft-form-grid ft-form-grid--inline">
        <div class="ft-field ft-field--grow">
          <label for="so-product">Produit</label>
          <p-autoComplete
            inputId="so-product"
            [(ngModel)]="newProduct"
            [suggestions]="productSuggestions()"
            (completeMethod)="searchProducts($event)"
            field="label"
            [forceSelection]="true"
            [dropdown]="true"
            placeholder="Rechercher par code ou nom"
            appendTo="body"></p-autoComplete>
        </div>

        <div class="ft-field">
          <label for="so-qty">Quantité</label>
          <p-inputNumber
            inputId="so-qty"
            [(ngModel)]="newQuantity"
            mode="decimal"
            [minFractionDigits]="0"
            [maxFractionDigits]="4"
            [min]="0"></p-inputNumber>
        </div>

        <div class="ft-field ft-field--actions">
          <app-button
            variant="secondary"
            icon="pi-plus"
            iconPos="left"
            [disabled]="!newProduct || !newQuantity || newQuantity <= 0 || resolving()"
            (clicked)="addLine()">
            Ajouter
          </app-button>
        </div>
      </div>

      @if (lines().length === 0) {
        <p class="ft-muted ft-empty-inline">Aucune ligne. Ajoutez au moins un produit.</p>
      } @else {
        <p-table [value]="lines()" styleClass="ft-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Produit</th>
              <th class="ft-num">Quantité</th>
              <th class="ft-num">PU HT</th>
              <th>Origine du prix</th>
              <th class="ft-num">Remise %</th>
              <th class="ft-num">Total HT</th>
              <th class="ft-actions-col"></th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-line let-i="rowIndex">
            <tr>
              <td>
                <span class="ft-muted">{{ line.product.code }}</span> — {{ line.product.name }}
              </td>
              <td class="ft-num">
                <p-inputNumber
                  [(ngModel)]="line.quantity"
                  (onBlur)="onQuantityChange(i)"
                  mode="decimal"
                  [minFractionDigits]="0"
                  [maxFractionDigits]="4"
                  [min]="0"
                  styleClass="ft-inline-input"></p-inputNumber>
              </td>
              <td class="ft-num">
                <p-inputNumber
                  [(ngModel)]="line.unitPrice"
                  (onInput)="markOverridden(i)"
                  mode="decimal"
                  [minFractionDigits]="3"
                  [maxFractionDigits]="3"
                  [min]="0"
                  styleClass="ft-inline-input"></p-inputNumber>
              </td>
              <td>
                @if (line.priceOverridden) {
                  <p-tag severity="warning" value="Saisi"
                         pTooltip="Prix imposé à la main : il remplace le prix résolu."></p-tag>
                } @else if (line.priceSource === 'ClientPrice') {
                  <p-tag severity="success" value="Prix négocié"></p-tag>
                } @else if (line.priceSource === 'PriceList') {
                  <p-tag severity="info" value="Grille"></p-tag>
                } @else {
                  <span class="ft-muted">Catalogue</span>
                }
              </td>
              <td class="ft-num">
                <p-inputNumber
                  [(ngModel)]="line.discountPercent"
                  mode="decimal"
                  [minFractionDigits]="0"
                  [maxFractionDigits]="2"
                  [min]="0"
                  [max]="100"
                  styleClass="ft-inline-input"></p-inputNumber>
              </td>
              <td class="ft-num">{{ lineTotal(line) | number: '1.3-3' }}</td>
              <td class="ft-actions-col">
                <button
                  pButton
                  type="button"
                  icon="pi pi-trash"
                  class="p-button-text p-button-sm p-button-danger"
                  pTooltip="Retirer"
                  (click)="removeLine(i)"></button>
              </td>
            </tr>
          </ng-template>
        </p-table>

        <div class="ft-totals">
          <div class="ft-totals__row ft-totals__row--grand">
            <span>Total HT</span><span>{{ totalHt() | number: '1.3-3' }} TND</span>
          </div>
          <small class="ft-hint">
            TVA, FODEC et timbre fiscal sont calculés par le serveur à la création.
          </small>
        </div>
      }
    </div>
  `
})
export class SalesOrderCreateComponent {
  private readonly router = inject(Router);
  private readonly salesOrderService = inject(SalesOrderService);
  private readonly pricingService = inject(PricingService);
  private readonly clientService = inject(ClientService);
  private readonly productService = inject(ProductService);
  private readonly toastService = inject(ToastService);
  private readonly errorHandler = inject(ErrorHandlerService);

  readonly lines = signal<DraftLine[]>([]);
  readonly clientSuggestions = signal<ClientOption[]>([]);
  readonly productSuggestions = signal<ProductOption[]>([]);
  readonly saving = signal(false);
  readonly resolving = signal(false);

  selectedClient: ClientOption | null = null;
  orderDate = new Date().toISOString().substring(0, 10);
  expectedDeliveryDate = '';
  reference = '';
  notes = '';
  paymentTerms = '';

  newProduct: ProductOption | null = null;
  newQuantity: number | null = 1;

  readonly breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Ventes' },
    { label: 'Commandes clients', route: '/sales-orders' },
    { label: 'Nouvelle' }
  ];

  readonly canSubmit = computed(
    () => this.selectedClient !== null && this.lines().length > 0 && !!this.orderDate
  );

  readonly totalHt = computed(() =>
    this.lines().reduce((sum, l) => sum + this.lineTotal(l), 0)
  );

  lineTotal(line: DraftLine): number {
    const gross = line.quantity * line.unitPrice;
    const discount = line.discountPercent ? (gross * line.discountPercent) / 100 : 0;
    return gross - discount;
  }

  searchClients(event: { query: string }): void {
    this.clientService.getClients({ search: event.query, pageSize: 20, isActive: true }).subscribe({
      next: res => {
        const items = res.data?.items ?? [];
        this.clientSuggestions.set(items.map(c => ({ id: c.id, label: `${c.name}` })));
      },
      error: () => this.clientSuggestions.set([])
    });
  }

  searchProducts(event: { query: string }): void {
    this.productService.getProducts({ search: event.query, pageSize: 20 }).subscribe({
      next: res => {
        const items = res.data?.items ?? [];
        this.productSuggestions.set(
          items.map(p => ({
            id: p.id,
            code: p.code,
            name: p.name,
            label: `${p.code} — ${p.name}`,
            unitPrice: p.unitPrice
          }))
        );
      },
      error: () => this.productSuggestions.set([])
    });
  }

  /**
   * Changer de client change la grille applicable : on retarife toutes les lignes dont le prix
   * n'a pas été imposé à la main. Les laisser au prix du client précédent afficherait un total
   * que le serveur ne confirmerait pas.
   */
  onClientChange(): void {
    this.reresolveAll();
  }

  onDateChange(): void {
    this.reresolveAll();
  }

  private reresolveAll(): void {
    const current = this.lines();
    if (current.length === 0) return;

    current.forEach((line, index) => {
      if (!line.priceOverridden) this.resolvePriceFor(index);
    });
  }

  addLine(): void {
    if (!this.newProduct || !this.newQuantity || this.newQuantity <= 0) return;

    const product = this.newProduct;
    const quantity = this.newQuantity;

    // Prix catalogue en attendant la réponse du serveur : la ligne ne reste jamais vide.
    this.lines.update(list => [
      ...list,
      {
        product,
        quantity,
        unitPrice: product.unitPrice,
        discountPercent: null,
        notes: null,
        priceSource: null,
        priceOverridden: false
      }
    ]);

    this.newProduct = null;
    this.newQuantity = 1;

    this.resolvePriceFor(this.lines().length - 1);
  }

  onQuantityChange(index: number): void {
    // La quantité entrera dans la résolution avec les paliers (tranche 5B) ; on redemande
    // dès maintenant pour que l'écran suive le serveur sans changement ultérieur.
    const line = this.lines()[index];
    if (line && !line.priceOverridden) this.resolvePriceFor(index);
  }

  markOverridden(index: number): void {
    this.lines.update(list =>
      list.map((l, i) => (i === index ? { ...l, priceOverridden: true, priceSource: null } : l))
    );
  }

  private resolvePriceFor(index: number): void {
    const line = this.lines()[index];
    if (!line) return;

    this.resolving.set(true);
    this.pricingService
      .resolve(line.product.id, this.selectedClient?.id ?? null, line.quantity, this.orderDate)
      .subscribe({
        next: res => {
          const resolved = res.data;
          if (resolved) {
            this.lines.update(list =>
              list.map((l, i) =>
                i === index && !l.priceOverridden
                  ? { ...l, unitPrice: resolved.unitPriceHT, priceSource: resolved.source }
                  : l
              )
            );
          }
          this.resolving.set(false);
        },
        error: err => {
          // Repli sur le prix catalogue déjà en place : la saisie continue. Le serveur
          // recalculera de toute façon à la création.
          this.errorHandler.logError('Price resolution failed', err);
          this.resolving.set(false);
        }
      });
  }

  removeLine(index: number): void {
    this.lines.update(list => list.filter((_, i) => i !== index));
  }

  submit(): void {
    if (!this.canSubmit() || !this.selectedClient) return;

    const lines: CreateSalesOrderLineRequest[] = this.lines().map(l => ({
      productId: l.product.id,
      quantity: l.quantity,
      // Le prix résolu est renvoyé tel quel : il est ainsi figé sur la commande, et l'écran
      // ne peut pas diverger de ce que le serveur enregistre.
      unitPrice: l.unitPrice,
      discountPercent: l.discountPercent,
      notes: l.notes
    }));

    this.saving.set(true);
    this.salesOrderService
      .createSalesOrder({
        clientId: this.selectedClient.id,
        orderDate: this.orderDate,
        expectedDeliveryDate: this.expectedDeliveryDate || null,
        reference: this.reference || null,
        notes: this.notes || null,
        paymentTerms: this.paymentTerms || null,
        lines
      })
      .subscribe({
        next: res => {
          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: 'Commande créée en brouillon.'
          });
          this.saving.set(false);
          const id = res.data;
          this.router.navigate(['/sales-orders', id]);
        },
        error: err => {
          const msg = this.errorHandler.extractErrorMessage(err);
          this.toastService.add({
            severity: 'error',
            summary: 'Erreur',
            detail: msg || 'Création impossible'
          });
          this.errorHandler.logError('Create sales order failed', err);
          this.saving.set(false);
        }
      });
  }
}
