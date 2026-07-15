import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { AutoCompleteModule, AutoCompleteCompleteEvent, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { QuickCreateProductDialogComponent } from '@shared/components/quick-create-product-dialog/quick-create-product-dialog.component';
import {
  QuickCreateClientDialogComponent,
  QuickCreatedClient,
} from '@shared/components/quick-create-client-dialog/quick-create-client-dialog.component';
import { AuthService } from '@core/services/auth.service';
import { formatLocalDate } from '@core/utils/date.util';
import { PERMISSIONS } from '@core/config/permission-keys';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { QuoteService, CreateQuoteRequest, CreateQuoteLine } from '@core/services/quote.service';
import { ClientService, ClientListItem } from '@core/services/client.service';
import { ProductService, ProductListItem } from '@core/services/product.service';
import { CrmService } from '@features/crm/services/crm.service';

interface LineRow {
  product: ProductListItem | null;
  designation: string;
  description: string;
  quantity: number;
  unit: string;
  unitPrice: number;
  vatRatePercent: number;
  discountPercent?: number;
}

@Component({
  selector: 'app-quote-form',
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
    AutoCompleteModule,
    DialogModule,
    MessageModule,
    BreadcrumbComponent,
    QuickCreateProductDialogComponent,
    QuickCreateClientDialogComponent,
    FormSectionComponent,
    ButtonComponent,
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <div class="form-container">
      <h1 class="page-title">Créer un devis</h1>
      <p class="page-subtitle">
        Renseignez le client, les dates et les lignes. Vous pourrez envoyer le devis au client une fois créé.
      </p>
      <p class="page-hint">
        Ce devis reste modifiable tant qu'il n'est pas envoyé au client.
      </p>

      <form (ngSubmit)="onSubmit()" class="quote-form">
        <app-form-section title="Client et période" icon="pi-user" [number]="1">
          <div class="form-grid">
            <div class="field span-full">
              <label for="quoteTemplate">Modèle de devis (optionnel)</label>
              <p-dropdown
                id="quoteTemplate"
                [options]="quoteTemplateOptions()"
                [(ngModel)]="selectedQuoteTemplateId"
                optionLabel="label"
                optionValue="value"
                placeholder="Aucun — saisie libre"
                [showClear]="true"
                name="quoteTemplate"
                styleClass="w-full"
                (onChange)="onQuoteTemplateChange()">
              </p-dropdown>
              <small class="field-hint">Préremplit les lignes, les notes, les conditions et la date de validité.</small>
            </div>
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
              <label for="expiryDate">Valide jusqu'au <span class="required">*</span></label>
              <p-calendar
                id="expiryDate"
                [(ngModel)]="expiryDate"
                [readonlyInput]="true"
                dateFormat="dd/mm/yy"
                name="expiryDate"
                styleClass="w-full">
              </p-calendar>
              <small class="field-hint">Date limite de validité de l'offre. Passée cette date, le devis ne pourra plus être accepté.</small>
            </div>
          </div>
          <div class="field mt-3">
            <label for="reference">Référence</label>
            <input pInputText id="reference" [(ngModel)]="reference" name="reference" class="w-full" />
          </div>
        </app-form-section>

        <app-form-section title="Lignes du devis" icon="pi-list" [number]="2">
          <div class="lines-actions">
            <app-button variant="secondary" icon="pi-plus" iconPos="left" (click)="addLine()">Ajouter une ligne</app-button>
          </div>
          <div class="lines-table-wrap">
            <table class="lines-table">
              <thead>
                <tr>
                  <th style="width: 22%">Article (Recherche) *</th>
                  <th style="width: 14%">Désignation</th>
                  <th style="width: 8%">Qté *</th>
                  <th style="width: 8%">Unité</th>
                  <th style="width: 12%">Prix unit. HT *</th>
                  <th style="width: 10%">TVA %</th>
                  <th style="width: 10%">Remise %</th>
                  <th style="width: 10%">Total HT</th>
                  <th style="width: 6%"></th>
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
                        [(ngModel)]="line.quantity"
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
                      <p-inputNumber
                        [(ngModel)]="line.unitPrice"
                        [ngModelOptions]="{ standalone: true }"
                        [min]="0"
                        [minFractionDigits]="2"
                        [maxFractionDigits]="3"
                        mode="decimal">
                      </p-inputNumber>
                    </td>
                    <td>
                      <p-dropdown
                        [options]="vatOptions"
                        [(ngModel)]="line.vatRatePercent"
                        [ngModelOptions]="{ standalone: true }"
                        optionLabel="label"
                        optionValue="value"
                        styleClass="w-full">
                      </p-dropdown>
                    </td>
                    <td>
                      <p-inputNumber
                        [(ngModel)]="line.discountPercent"
                        [ngModelOptions]="{ standalone: true }"
                        [min]="0"
                        [max]="100"
                        [minFractionDigits]="0"
                        [maxFractionDigits]="1"
                        mode="decimal"
                        suffix="%">
                      </p-inputNumber>
                    </td>
                    <td>
                      <div class="text-right px-2 font-bold">
                        {{ (line.unitPrice * line.quantity * (1 - (line.discountPercent || 0) / 100)) | number:'1.3-3' }}
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
                  <tr>
                    <td colspan="9" class="pb-4 border-b">
                      <input
                        pInputText
                        [(ngModel)]="line.description"
                        [ngModelOptions]="{ standalone: true }"
                        placeholder="Notes / description pour cette ligne (optionnel)"
                        class="w-full text-sm" />
                    </td>
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr>
                  <td colspan="7" class="text-right font-bold py-3">Total HT Estimé :</td>
                  <td class="text-right font-bold py-3">{{ totals.subTotal | number:'1.3-3' }} {{ currency }}</td>
                  <td></td>
                </tr>
              </tfoot>
            </table>
          </div>
          <p class="lines-hint">{{ lines.length }} ligne(s) ajoutée(s)</p>
          <div class="totals-panel">
            <div class="totals-row">
              <span>Total HT</span>
              <span class="mono">{{ totals.subTotal | number:'1.3-3' }} {{ currency }}</span>
            </div>
            <div class="totals-row">
              <span>Total TVA</span>
              <span class="mono">{{ totals.totalVat | number:'1.3-3' }} {{ currency }}</span>
            </div>
            <div class="totals-row grand">
              <span>Total TTC</span>
              <span class="mono">{{ totals.totalAmount | number:'1.3-3' }} {{ currency }}</span>
            </div>
          </div>
          <p class="totals-hint">Les montants se mettent à jour automatiquement selon les lignes saisies.</p>
        </app-form-section>

        <app-form-section title="Notes et conditions (optionnel)" icon="pi-file-edit" [number]="3">
          <div class="field">
            <label for="notes">Notes</label>
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
            <label for="terms">Conditions générales</label>
            <textarea
              pInputTextarea
              id="terms"
              [(ngModel)]="termsAndConditions"
              name="terms"
              [rows]="2"
              class="w-full">
            </textarea>
          </div>
        </app-form-section>

        @if (submitError()) {
          <div class="submit-error-banner" role="alert" aria-live="polite">
            <p-message
              severity="error"
              [text]="submitError()!"
              styleClass="w-full">
            </p-message>
          </div>
        }

        <div class="form-actions">
          <app-button
            variant="outline"
            icon="pi-times"
            iconPos="left"
            routerLink="/quotes">
            Annuler
          </app-button>
          <app-button
            variant="primary"
            [icon]="submitting() ? 'pi-spin pi-spinner' : 'pi-check'"
            iconPos="left"
            type="submit"
            [disabled]="!canSubmit() || submitting()">
            {{ submitting() ? 'Création...' : 'Créer le devis' }}
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
  styles: [
    `
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
      .quote-form {
        display: flex;
        flex-direction: column;
        gap: var(--spacing-6);
      }
      .form-grid {
        display: grid;
        grid-template-columns: 1fr 1fr 1fr;
        gap: var(--spacing-4);
      }
      .span-full {
        grid-column: 1 / -1;
      }
      @media (max-width: 768px) {
        .form-grid {
          grid-template-columns: 1fr;
        }
        .span-full {
          grid-column: span 1;
        }
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
      .lines-actions {
        margin-bottom: var(--spacing-4);
      }
      .totals-panel {
        min-width: 280px;
        max-width: 320px;
        margin-top: var(--spacing-4);
        margin-left: auto;
        padding: var(--spacing-4);
        background: var(--color-primary-50);
        border-radius: var(--radius-lg);
        border: 1px solid var(--color-primary-100);
      }
      .totals-row {
        display: flex;
        justify-content: space-between;
        padding: var(--spacing-2) 0;
        font-size: var(--font-size-sm);
      }
      .totals-row.grand {
        margin-top: var(--spacing-2);
        padding-top: var(--spacing-3);
        border-top: 2px solid var(--color-primary-200);
        font-size: var(--font-size-lg);
        font-weight: var(--font-weight-bold);
        color: var(--color-primary-700);
      }
      .totals-row .mono {
        font-family: 'JetBrains Mono', monospace;
        font-weight: var(--font-weight-semibold);
      }
      .totals-hint {
        margin-top: var(--spacing-2);
        font-size: var(--font-size-xs);
        color: var(--color-text-secondary);
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
      :host ::ng-deep .lines-table p-autocomplete .p-autocomplete {
        width: 100%;
      }
      :host ::ng-deep .lines-table p-autocomplete .p-autocomplete-input {
        width: 100%;
      }
      :host ::ng-deep .lines-table .p-autocomplete-panel {
        min-width: 350px !important;
      }
      :host ::ng-deep .lines-table p-inputnumber,
      :host ::ng-deep .lines-table p-inputnumber .p-inputnumber,
      :host ::ng-deep .lines-table p-inputnumber .p-inputtext {
        width: 100%;
      }
      :host ::ng-deep .lines-table p-dropdown,
      :host ::ng-deep .lines-table p-dropdown .p-dropdown {
        width: 100%;
      }
      .submit-error-banner {
        margin-bottom: var(--spacing-4);
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
    `,
  ],
})
export class QuoteFormComponent implements OnInit {
  private quoteService = inject(QuoteService);
  private clientService = inject(ClientService);
  private auth = inject(AuthService);
  private productService = inject(ProductService);
  private crm = inject(CrmService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toastService = inject(ToastService);
  private errorHandler = inject(ErrorHandlerService);

  breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
    { label: 'Devis', route: '/quotes' },
    { label: 'Nouveau devis' },
  ];

  clientOptions = signal<ClientListItem[]>([]);
  productSuggestions = signal<ProductListItem[]>([]);
  quoteTemplateOptions = signal<{ label: string; value: string }[]>([]);
  selectedQuoteTemplateId: string | null = null;
  selectedClientId: string | null = null;
  issueDate: Date = new Date();
  expiryDate: Date = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000);
  reference = '';
  notes = '';
  termsAndConditions = '';
  lines: LineRow[] = [
    this.createEmptyLine(),
  ];
  submitting = signal(false);
  submitError = signal<string | null>(null);
  quickCreateProductVisible = false;
  quickCreateClientVisible = false;
  lineIndexForNewProduct = 0;

  canCreateClient = computed(() => this.auth.hasPermission(PERMISSIONS.clients.create));

  vatOptions = [
    { label: '0%', value: 0 },
    { label: '7%', value: 7 },
    { label: '13%', value: 13 },
    { label: '19%', value: 19 },
  ];

  currency = 'TND';

  get totals(): { subTotal: number; totalVat: number; totalAmount: number } {
    const valid = this.lines.filter(
      (l) => (l.designation?.trim()?.length ?? 0) > 0 && l.quantity > 0 && l.unitPrice >= 0
    );
    let subTotal = 0;
    let totalVat = 0;
    for (const l of valid) {
      const gross = l.quantity * l.unitPrice;
      const discount = ((l.discountPercent ?? 0) / 100) * gross;
      const st = gross - discount;
      const vat = st * (l.vatRatePercent / 100);
      subTotal += st;
      totalVat += vat;
    }
    return { subTotal, totalVat, totalAmount: subTotal + totalVat };
  }

  ngOnInit(): void {
    this.clientService.getClients({ pageSize: 500, isActive: true }).subscribe({
      next: (res) => {
        if (res.success) {
          this.clientOptions.set(res.data.items);
          const clientId = this.route.snapshot.queryParamMap.get('clientId');
          if (clientId && res.data.items.some((c) => c.id === clientId)) {
            this.selectedClientId = clientId;
          }
        }
      },
    });
    this.crm.getQuoteTemplates({ activeOnly: true }).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.quoteTemplateOptions.set(res.data.map((t) => ({ label: t.name, value: t.id })));
          const tid = this.route.snapshot.queryParamMap.get('templateId');
          if (tid && res.data.some((x) => x.id === tid)) {
            this.selectedQuoteTemplateId = tid;
            this.applyQuoteTemplate(tid);
          }
        }
      },
    });
  }

  onQuoteTemplateChange(): void {
    if (!this.selectedQuoteTemplateId) return;
    this.applyQuoteTemplate(this.selectedQuoteTemplateId);
  }

  private applyQuoteTemplate(templateId: string): void {
    this.crm.getQuoteTemplate(templateId).subscribe({
      next: (res) => {
        if (!res.success || !res.data) return;
        const t = res.data;
        this.notes = t.defaultNotes ?? '';
        this.termsAndConditions = t.defaultTermsAndConditions ?? '';
        const start = new Date(this.issueDate);
        const exp = new Date(start);
        exp.setDate(exp.getDate() + t.defaultValidityDays);
        this.expiryDate = exp;
        if (!t.lines.length) return;
        const productCalls = t.lines.map((l) => this.productService.getProduct(l.productId));
        forkJoin(productCalls).subscribe({
          next: (responses) => {
            const newLines: LineRow[] = [];
            for (let i = 0; i < t.lines.length; i++) {
              const tl = t.lines[i];
              const pr = responses[i];
              if (!pr.success || !pr.data) continue;
              const p = pr.data;
              const pl: ProductListItem = {
                id: p.id,
                code: p.code,
                name: p.name,
                description: p.description,
                typeDisplay: p.typeDisplay,
                categoryId: p.categoryId,
                category: p.category,
                unitPrice: p.unitPrice,
                unit: p.unit,
                vatRate: p.vatRate,
                isActive: p.isActive,
                isStockManaged: p.isStockManaged,
              };
              newLines.push({
                product: pl,
                designation: p.name,
                description: p.description || '',
                quantity: tl.quantity,
                unit: p.unit,
                unitPrice:
                  tl.customUnitPrice != null && tl.customUnitPrice > 0 ? tl.customUnitPrice : p.unitPrice,
                vatRatePercent: p.vatRate,
                discountPercent: tl.discountPercent ?? undefined,
              });
            }
            if (newLines.length) this.lines = newLines;
          },
        });
      },
    });
  }

  createEmptyLine(): LineRow {
    return {
      product: null,
      designation: '',
      description: '',
      quantity: 1,
      unit: 'unité',
      unitPrice: 0,
      vatRatePercent: 19,
    };
  }

  addLine(): void {
    this.lines.push(this.createEmptyLine());
  }

  removeLine(i: number): void {
    if (this.lines.length > 1) this.lines.splice(i, 1);
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
    line.unitPrice = product.unitPrice;
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
      line.unitPrice = product.unitPrice;
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

  canSubmit(): boolean {
    if (!this.selectedClientId || !this.issueDate || !this.expiryDate) return false;
    if (this.expiryDate <= this.issueDate) return false;
    const valid = this.lines.every(
      (l) => l.product !== null && l.quantity > 0 && l.unitPrice >= 0
    );
    return valid;
  }

  onSubmit(): void {
    if (!this.canSubmit() || !this.selectedClientId) return;

    const lineDtos: CreateQuoteLine[] = this.lines
      .filter((l) => l.product !== null && l.quantity > 0)
      .map((l) => ({
        productId: l.product!.id,
        designation: l.designation.trim(),
        description: l.description?.trim() || undefined,
        quantity: l.quantity,
        unit: l.unit?.trim() || 'unité',
        unitPrice: l.unitPrice,
        vatRatePercent: l.vatRatePercent,
        discountPercent: l.discountPercent && l.discountPercent > 0 ? l.discountPercent : undefined,
      }));

    if (lineDtos.length === 0) {
      this.toastService.add({
        severity: 'warn',
        summary: 'Lignes manquantes',
        detail: 'Ajoutez au moins une ligne avec un produit sélectionné.',
      });
      return;
    }

    this.submitError.set(null);

    const req: CreateQuoteRequest = {
      clientId: this.selectedClientId,
      issueDate: formatLocalDate(this.issueDate),
      expiryDate: formatLocalDate(this.expiryDate),
      reference: this.reference?.trim() || undefined,
      notes: this.notes?.trim() || undefined,
      termsAndConditions: this.termsAndConditions?.trim() || undefined,
      lines: lineDtos,
      quoteTemplateId: this.selectedQuoteTemplateId ?? undefined,
    };

    this.submitting.set(true);
    this.quoteService.createQuote(req).subscribe({
      next: (res) => {
        this.submitting.set(false);
        this.submitError.set(null);
        if (res.success && res.data) {
          this.toastService.add({
            severity: 'success',
            summary: 'Devis créé',
            detail: 'Redirection vers le devis...',
          });
          this.router.navigate(['/quotes', res.data]);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        const detail = this.errorHandler.extractErrorMessage(err);
        this.submitError.set(detail);
        this.toastService.add({
          severity: 'error',
          summary: 'Erreur',
          detail,
        });
      },
    });
  }
}
