import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { CheckboxModule } from 'primeng/checkbox';
import { AutoCompleteModule, AutoCompleteCompleteEvent, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { WarehouseSelectorComponent } from '@shared/components/warehouse-selector/warehouse-selector.component';
import { ToastService } from '@core/services/toast.service';
import { QuickCreateProductDialogComponent } from '@shared/components/quick-create-product-dialog/quick-create-product-dialog.component';
import {
  QuickCreateClientDialogComponent,
  QuickCreatedClient,
} from '@shared/components/quick-create-client-dialog/quick-create-client-dialog.component';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { formatLocalDate } from '@core/utils/date.util';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { DeliveryNoteService } from '../../services/delivery-note.service';
import { CreateDeliveryNoteDto, CreateDeliveryNoteLineDto } from '../../models/delivery-note.model';
import { ClientService, ClientListItem } from '@core/services/client.service';
import { ProductService, ProductListItem } from '@core/services/product.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';

interface LineRow {
  product: ProductListItem | null; // Selected product
  designation: string; // Read-only snapshot
  description: string; // Read-only snapshot
  orderedQuantity: number;
  unit: string; // Read-only snapshot
  unitPriceHT: number; // Read-only snapshot for display
  vatRatePercent: number; // Read-only snapshot for display
  notes: string;
}

@Component({
  selector: 'app-delivery-note-form',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    ButtonModule,
    CardModule,
    InputTextModule,
    DropdownModule,
    CalendarModule,
    InputNumberModule,
    InputTextareaModule,
    CheckboxModule,
    AutoCompleteModule,
    DialogModule,
    ToastModule,
    BreadcrumbComponent,
    QuickCreateProductDialogComponent,
    QuickCreateClientDialogComponent,
    WarehouseSelectorComponent,
    FormSectionComponent,
    ButtonComponent,
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <div class="form-container">
      <h1 class="page-title">Créer un bon de livraison</h1>
      <p class="page-subtitle">
        Renseignez le client, l'adresse de livraison et les articles à livrer.
      </p>
      <p class="page-hint">
        Le bon de livraison sera créé en brouillon. Vous pourrez le confirmer ensuite.
      </p>

      <form (ngSubmit)="onSubmit()" class="dn-form">
        <app-form-section title="Client et informations" icon="pi-user" [number]="1">
          <div class="form-grid">
            <div class="field">
              <label for="client">Client <span class="required">*</span></label>
              <div class="client-cell">
                <div class="client-cell-input">
                  <p-dropdown
                    id="client"
                    [options]="clientOptions()"
                    [(ngModel)]="selectedClientId"
                    optionLabel="name"
                    optionValue="id"
                    placeholder="Choisir un client"
                    [filter]="true"
                    filterBy="name"
                    [showClear]="true"
                    name="client"
                    styleClass="w-full">
                  </p-dropdown>
                </div>
                @if (canCreateClient()) {
                  <app-button
                    variant="primary"
                    size="sm"
                    icon="pi-plus"
                    [iconOnly]="true"
                    ariaLabel="Créer un client"
                    (click)="openQuickCreateClient()"
                    class="btn-add-client-circle" />
                }
              </div>
            </div>
            <div class="field">
              <label for="issueDate">Date d'émission <span class="required">*</span></label>
              <p-calendar
                id="issueDate"
                [(ngModel)]="issueDate"
                [readonlyInput]="true"
                dateFormat="dd/mm/yy"
                name="issueDate"
                styleClass="w-full">
              </p-calendar>
            </div>
            <div class="field">
              <label for="reference">Référence</label>
              <input pInputText id="reference" [(ngModel)]="reference" name="reference" class="w-full" />
            </div>
          </div>
        </app-form-section>

        <app-form-section title="Adresse de livraison" icon="pi-map-marker" [number]="2">
          <div class="form-grid">
            <div class="field span-full">
              <label for="deliveryAddress">Adresse <span class="required">*</span></label>
              <input pInputText id="deliveryAddress" [(ngModel)]="deliveryAddress" name="deliveryAddress" class="w-full" placeholder="Adresse complète" />
            </div>
            <div class="field">
              <label for="deliveryCity">Ville</label>
              <input pInputText id="deliveryCity" [(ngModel)]="deliveryCity" name="deliveryCity" class="w-full" />
            </div>
            <div class="field">
              <label for="deliveryPostalCode">Code postal</label>
              <input pInputText id="deliveryPostalCode" [(ngModel)]="deliveryPostalCode" name="deliveryPostalCode" class="w-full" />
            </div>
          </div>
        </app-form-section>

        <app-form-section title="Lignes du bon de livraison" icon="pi-list" [number]="3">
          <div class="lines-actions">
            <app-button variant="secondary" icon="pi-plus" iconPos="left" (click)="addLine()">Ajouter une ligne</app-button>
          </div>
          <div class="lines-table-wrap">
            <table class="lines-table">
              <thead>
                <tr>
                  <th style="width: 25%">Article (Recherche) *</th>
                  <th style="width: 20%">Désignation</th>
                  <th style="width: 10%">Qté *</th>
                  <th style="width: 10%">Unité</th>
                  <th style="width: 10%">P.U. HT</th>
                  <th style="width: 10%">Total HT</th>
                  <th style="width: 10%"></th>
                </tr>
              </thead>
              <tbody>
                @for (line of lines; track $index) {
                  <tr>
                    <td>
                      <div class="product-cell">
                        <div class="product-cell-input">
                          <p-autoComplete
                            [(ngModel)]="line.product"
                            [suggestions]="productSuggestions()"
                            (completeMethod)="searchProducts($event)"
                            (onSelect)="onProductSelect(line, $event)"
                            field="name"
                            [dropdown]="true"
                            [forceSelection]="true"
                            [ngModelOptions]="{ standalone: true }"
                            placeholder="Rechercher un produit..."
                            styleClass="w-full"
                            inputStyleClass="w-full">
                            <ng-template let-product pTemplate="item">
                              <div class="product-item">
                                <span class="font-bold">{{ product.code }}</span> - {{ product.name }}
                                <div class="text-sm text-gray-500">{{ product.unitPrice | number:'1.3-3' }} TND</div>
                              </div>
                            </ng-template>
                          </p-autoComplete>
                        </div>
                        <app-button
                          variant="primary"
                          size="sm"
                          icon="pi-plus"
                          [iconOnly]="true"
                          ariaLabel="Créer un produit"
                          (click)="openQuickCreateProduct($index)"
                          class="btn-add-product-circle" />
                      </div>
                    </td>
                    <td>
                      <input
                        pInputText
                        [value]="line.designation"
                        disabled="true"
                        placeholder="-"
                        class="w-full bg-gray-50" />
                    </td>
                    <td>
                      <p-inputNumber
                        [(ngModel)]="line.orderedQuantity"
                        [ngModelOptions]="{ standalone: true }"
                        [min]="0.001"
                        [minFractionDigits]="0"
                        [maxFractionDigits]="3"
                        mode="decimal"
                        class="w-full">
                      </p-inputNumber>
                    </td>
                    <td>
                      <input
                        pInputText
                        [value]="line.unit"
                        disabled="true"
                        placeholder="-"
                        class="w-full bg-gray-50" />
                    </td>
                    <td>
                      <div class="text-right px-2">
                        {{ line.unitPriceHT | number:'1.3-3' }}
                      </div>
                    </td>
                    <td>
                      <div class="text-right px-2 font-bold">
                        {{ (line.unitPriceHT * line.orderedQuantity) | number:'1.3-3' }}
                      </div>
                    </td>
                    <td>
                      <app-button
                        variant="ghost"
                        size="sm"
                        icon="pi-trash"
                        [iconOnly]="true"
                        (click)="removeLine($index)"
                        [disabled]="lines.length <= 1"
                        ariaLabel="Supprimer la ligne">
                      </app-button>
                    </td>
                  </tr>
                  <tr> <!-- Optional Second Row for Description/Notes -->
                     <td colspan="7" class="pb-4 border-b">
                        <input
                          pInputText
                          [(ngModel)]="line.notes"
                          [ngModelOptions]="{ standalone: true }"
                          placeholder="Notes internes pour cette ligne (optionnel)"
                          class="w-full text-sm" />
                     </td>
                  </tr>
                }
              </tbody>
              <tfoot>
                 <tr>
                    <td colspan="5" class="text-right font-bold py-3">Total HT Estimé :</td>
                    <td class="text-right font-bold py-3">{{ totalHT() | number:'1.3-3' }} TND</td>
                    <td></td>
                 </tr>
              </tfoot>
            </table>
          </div>
          <p class="lines-hint">{{ lines.length }} ligne(s) ajoutée(s)</p>
        </app-form-section>

        <app-form-section title="Options et notes (optionnel)" icon="pi-file-edit" [number]="4">
          <div class="field">
            <label for="notes">Notes générales</label>
            <textarea
              pInputTextarea
              id="notes"
              [(ngModel)]="notes"
              name="notes"
              [rows]="2"
              class="w-full">
            </textarea>
          </div>
          <div class="field mt-3">
            <p-checkbox
              [(ngModel)]="allowGroupInvoicing"
              [binary]="true"
              name="allowGroupInvoicing"
              inputId="allowGroupInvoicing"
              label="Autoriser la facturation groupée">
            </p-checkbox>
            <small class="field-hint">Si activé, ce bon de livraison pourra être regroupé avec d'autres pour une facturation unique.</small>
          </div>

          <div class="field mt-3">
            <app-warehouse-selector
              label="Entrepôt source"
              placeholder="Entrepôt par défaut"
              [value]="selectedWarehouseId"
              (valueChange)="selectedWarehouseId = $event">
            </app-warehouse-selector>
            <small class="field-hint">Entrepôt utilisé pour la sortie de stock lors de la livraison (optionnel)</small>
          </div>
        </app-form-section>

        <div class="form-actions">
          <app-button
            variant="outline"
            icon="pi-times"
            iconPos="left"
            routerLink="/delivery-notes">
            Annuler
          </app-button>
          <app-button
            variant="primary"
            [icon]="submitting() ? 'pi-spin pi-spinner' : 'pi-check'"
            iconPos="left"
            type="submit"
            [disabled]="!canSubmit() || submitting()">
            {{ submitting() ? 'Création...' : 'Créer le bon de livraison' }}
          </app-button>
        </div>
      </form>

      <app-quick-create-product-dialog
        [panelMode]="true"
        [(visible)]="quickCreateProductVisible"
        (productCreated)="onQuickProductCreated($event)">
      </app-quick-create-product-dialog>

      <app-quick-create-client-dialog
        [panelMode]="true"
        [(visible)]="quickCreateClientVisible"
        (clientCreated)="onQuickClientCreated($event)">
      </app-quick-create-client-dialog>
    </div>
  `,
  styles: [`
    .form-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: var(--spacing-6) 0;
    }
    .page-title {
      font-size: var(--font-size-2xl);
      font-weight: var(--font-weight-bold);
      color: var(--color-text-primary);
      margin: 0 0 var(--spacing-3);
    }
    .page-subtitle {
      color: var(--color-text-secondary);
      margin: 0 0 var(--spacing-2);
      font-size: var(--font-size-base);
    }
    .page-hint {
      color: var(--color-text-secondary);
      margin: 0 0 var(--spacing-8);
      font-size: var(--font-size-sm);
      font-style: italic;
    }
    .dn-form {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-6);
    }
    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: var(--spacing-4);
    }
    @media (max-width: 768px) {
      .form-grid {
        grid-template-columns: 1fr;
      }
    }
    .span-full {
      grid-column: 1 / -1;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
    }
    .field label {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
      margin-bottom: var(--spacing-1);
    }
    .required {
      color: var(--color-error-600);
    }
    .field-hint {
      display: block;
      margin-top: var(--spacing-1);
      font-size: var(--font-size-xs);
      color: var(--color-text-secondary);
    }
    .mt-3 {
      margin-top: var(--spacing-3);
    }
    .w-full {
      width: 100%;
    }
    .bg-gray-50 {
        background-color: #f9fafb;
    }
    .text-right {
        text-align: right;
    }
    .font-bold {
        font-weight: bold;
    }
    .text-sm {
        font-size: 0.875rem;
    }
    .text-gray-500 {
        color: #6b7280;
    }
    .px-2 {
        padding-left: 0.5rem;
        padding-right: 0.5rem;
    }
    .py-3 {
        padding-top: 0.75rem;
        padding-bottom: 0.75rem;
    }
    .pb-4 {
        padding-bottom: 1rem;
    }
    .border-b {
        border-bottom: 1px solid var(--color-border-subtle);
    }
    .lines-actions {
      margin-bottom: var(--spacing-4);
    }
    .lines-hint {
      margin-top: var(--spacing-2);
      font-size: var(--font-size-xs);
      color: var(--color-text-secondary);
    }
    .lines-table-wrap {
      overflow-x: auto;
    }
    .lines-table {
      width: 100%;
      border-collapse: collapse;
    }
    .lines-table th,
    .lines-table td {
      padding: var(--spacing-4) var(--spacing-3);
      text-align: left;
      vertical-align: top;
    }
    .lines-table th {
      font-size: var(--font-size-xs);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--color-text-secondary);
      background: var(--color-background-subtle);
      font-weight: var(--font-weight-semibold);
      border-bottom: 1px solid var(--color-border-subtle);
    }
    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-4);
      margin-top: var(--spacing-6);
      padding-top: var(--spacing-6);
      border-top: 1px solid var(--color-border-subtle);
    }
    .product-item {
        display: flex;
        flex-direction: column;
    }
    .product-cell {
      display: flex;
      flex-direction: row;
      align-items: center;
      gap: var(--spacing-2);
    }
    .product-cell-input {
      flex: 1;
      min-width: 0;
    }
    :host ::ng-deep .product-cell-input .p-autocomplete {
      width: 100%;
    }
    .btn-add-product-circle {
      border-radius: var(--radius-full);
      width: 36px;
      height: 36px;
      padding: 0;
      flex-shrink: 0;
    }
    .client-cell {
      display: flex;
      flex-direction: row;
      align-items: center;
      gap: var(--spacing-2);
    }
    .client-cell-input {
      flex: 1;
      min-width: 0;
    }
    :host ::ng-deep .client-cell-input .p-dropdown {
      width: 100%;
    }
    .btn-add-client-circle {
      border-radius: var(--radius-full);
      width: 36px;
      height: 36px;
      padding: 0;
      flex-shrink: 0;
    }
  `]
})
export class DeliveryNoteFormComponent implements OnInit {
  private deliveryNoteService = inject(DeliveryNoteService);
  private clientService = inject(ClientService);
  private auth = inject(AuthService);
  private productService = inject(ProductService);
  private router = inject(Router);
  private toastService = inject(ToastService);
  private errorHandler = inject(ErrorHandlerService);
  private warehouseContext = inject(WarehouseContextService);

  breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
    { label: 'Bons de Livraison', route: '/delivery-notes' },
    { label: 'Nouveau bon de livraison' },
  ];

  clientOptions = signal<ClientListItem[]>([]);
  productSuggestions = signal<ProductListItem[]>([]);

  selectedClientId: string | null = null;
  issueDate: Date = new Date();
  reference = '';
  deliveryAddress = '';
  deliveryCity = '';
  deliveryPostalCode = '';
  notes = '';
  allowGroupInvoicing = false;
  selectedWarehouseId: string | null = null;

  lines: LineRow[] = [
    this.createEmptyLine()
  ];

  submitting = signal(false);
  quickCreateProductVisible = false;
  quickCreateClientVisible = false;
  lineIndexForNewProduct = 0;

  canCreateClient = computed(() => this.auth.hasPermission(PERMISSIONS.clients.create));

  ngOnInit(): void {
    const ctxWh = this.warehouseContext.selectedWarehouseId();
    if (ctxWh && this.selectedWarehouseId == null) {
      this.selectedWarehouseId = ctxWh;
    }
    this.clientService.getClients({ pageSize: 500, isActive: true }).subscribe({
      next: (res) => {
        if (res.success) this.clientOptions.set(res.data.items);
      },
    });
  }

  createEmptyLine(): LineRow {
    return {
      product: null,
      designation: '',
      description: '',
      orderedQuantity: 1,
      unit: '',
      unitPriceHT: 0,
      vatRatePercent: 0,
      notes: ''
    };
  }

  searchProducts(event: AutoCompleteCompleteEvent): void {
    this.productService.getProducts({
      search: event.query,
      isActive: true,
      pageSize: 20
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.productSuggestions.set(res.data.items);
        }
      }
    });
  }

  onProductSelect(line: LineRow, event: AutoCompleteSelectEvent): void {
    const product = event.value as ProductListItem;
    line.product = product;
    line.designation = product.name;
    line.description = product.description || '';
    line.unit = product.unit;
    line.unitPriceHT = product.unitPrice;
    line.vatRatePercent = product.vatRate;
  }

  openQuickCreateProduct(lineIndex: number): void {
    this.lineIndexForNewProduct = lineIndex;
    this.quickCreateProductVisible = true;
  }

  openQuickCreateClient(): void {
    this.quickCreateClientVisible = true;
  }

  onQuickClientCreated(created: QuickCreatedClient): void {
    const client = created.listItem;
    this.clientOptions.update((opts) => [client, ...opts.filter((c) => c.id !== client.id)]);
    this.selectedClientId = client.id;
    this.quickCreateClientVisible = false;

    if (!this.deliveryAddress?.trim()) {
      this.deliveryAddress = created.street;
    }
    if (!this.deliveryCity?.trim()) {
      this.deliveryCity = client.city;
    }
    if (!this.deliveryPostalCode?.trim() && created.postalCode) {
      this.deliveryPostalCode = created.postalCode;
    }

    this.toastService.add({
      severity: 'success',
      summary: 'Client créé',
      detail: 'Le client a été créé et sélectionné.',
    });
  }

  onQuickProductCreated(product: ProductListItem): void {
    const line = this.lines[this.lineIndexForNewProduct];
    if (line) {
      line.product = product;
      line.designation = product.name;
      line.description = product.description || '';
      line.unit = product.unit;
      line.unitPriceHT = product.unitPrice;
      line.vatRatePercent = product.vatRate;
    }
    this.productSuggestions.set([product, ...this.productSuggestions()]);
    this.quickCreateProductVisible = false;
    this.toastService.add({
      severity: 'success',
      summary: 'Produit créé',
      detail: 'Le produit a été créé et ajouté à la ligne.',
    });
  }

  addLine(): void {
    this.lines.push(this.createEmptyLine());
  }

  removeLine(i: number): void {
    if (this.lines.length > 1) this.lines.splice(i, 1);
  }

  totalHT(): number {
    return this.lines.reduce((sum, line) => sum + (line.unitPriceHT * line.orderedQuantity), 0);
  }

  canSubmit(): boolean {
    if (!this.selectedClientId || !this.issueDate) return false;
    if (!this.deliveryAddress?.trim()) return false;

    // Validate lines: must have product and quantity > 0
    const validLines = this.lines.every(
      (l) => l.product !== null && l.orderedQuantity > 0
    );

    return validLines;
  }

  onSubmit(): void {
    if (!this.canSubmit() || !this.selectedClientId) return;

    const lineDtos: CreateDeliveryNoteLineDto[] = this.lines
      .filter((l) => l.product !== null && l.orderedQuantity > 0)
      .map((l) => ({
        productId: l.product!.id,
        orderedQuantity: l.orderedQuantity,
        notes: l.notes?.trim() || undefined,
      }));

    if (lineDtos.length === 0) {
      this.toastService.add({
        severity: 'warn',
        summary: 'Lignes manquantes',
        detail: 'Ajoutez au moins une ligne avec un produit sélectionné.',
      });
      return;
    }

    const dto: CreateDeliveryNoteDto = {
      clientId: this.selectedClientId,
      issueDate: formatLocalDate(this.issueDate),
      deliveryAddress: this.deliveryAddress.trim(),
      deliveryCity: this.deliveryCity?.trim() || undefined,
      deliveryPostalCode: this.deliveryPostalCode?.trim() || undefined,
      reference: this.reference?.trim() || undefined,
      notes: this.notes?.trim() || undefined,
      allowGroupInvoicing: this.allowGroupInvoicing,
      warehouseId: this.selectedWarehouseId || undefined,
      lines: lineDtos,
    };

    this.submitting.set(true);
    this.deliveryNoteService.createDeliveryNote(dto).subscribe({
      next: (res) => {
        this.submitting.set(false);
        if (res.success && res.data) {
          this.toastService.add({
            severity: 'success',
            summary: 'Bon de livraison créé',
            detail: 'Redirection vers le bon de livraison...',
          });
          this.router.navigate(['/delivery-notes', res.data]);
        }
      },
      error: (err) => {
        this.submitting.set(false);
        const detail = this.errorHandler.extractErrorMessage(err);
        const isContextError =
          detail.toLowerCase().includes('contexte') || detail.toLowerCase().includes('entreprise valide');
        const fullDetail = isContextError
          ? `${detail} Déconnectez-vous et reconnectez-vous, ou contactez l'administrateur pour vérifier votre rattachement à une entreprise.`
          : detail;
        this.toastService.add({
          severity: 'error',
          summary: 'Erreur',
          detail: fullDetail,
        });
      },
    });
  }
}
