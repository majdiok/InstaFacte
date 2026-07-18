import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { DialogModule } from 'primeng/dialog';
import { InputTextarea } from 'primeng/inputtextarea';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonTableComponent, SkeletonColumn } from '@shared/components/skeleton/skeleton-table.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { TableTotalsBarComponent, TotalMetric } from '@shared/components/table-totals-bar/table-totals-bar.component';
import { AnalyzeWithAiButtonComponent } from '@features/ai-assistant/components/analyze-with-ai-button/analyze-with-ai-button.component';
import { buildScreenAnalysisPayloadV2, sampleRowsSmart } from '@features/ai-assistant/utils/ai-screen-payload.factory';
import { AddCashOperationDialogComponent } from './components/add-cash-operation-dialog/add-cash-operation-dialog.component';
import { BankDepositWizardComponent } from './components/bank-deposit-wizard/bank-deposit-wizard.component';
import {
  CashDeskService,
  CashOperationListItem,
  CashDeskBalances,
  CashOperationStatus,
  CashOperationType,
  CashOperationOrigin
} from '@core/services/cash-desk.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { forkJoin } from 'rxjs';

import { HttpErrorResponse } from '@angular/common/http';

const MONTH_OPTIONS: { label: string; value: number }[] = [
  { label: 'Janvier', value: 1 },
  { label: 'Février', value: 2 },
  { label: 'Mars', value: 3 },
  { label: 'Avril', value: 4 },
  { label: 'Mai', value: 5 },
  { label: 'Juin', value: 6 },
  { label: 'Juillet', value: 7 },
  { label: 'Août', value: 8 },
  { label: 'Septembre', value: 9 },
  { label: 'Octobre', value: 10 },
  { label: 'Novembre', value: 11 },
  { label: 'Décembre', value: 12 }
];

@Component({
  selector: 'app-cash-desk',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    ButtonModule,
    DropdownModule,
    DialogModule,
    InputTextarea,
    TooltipModule,
    PageHeaderComponent,
    SkeletonTableComponent,
    EmptyStateComponent,
    ButtonComponent,
    TableTotalsBarComponent,
    AddCashOperationDialogComponent,
    BankDepositWizardComponent,
    AnalyzeWithAiButtonComponent
  ],
  template: `
    <app-page-header
      title="Caisse de trésorerie"
      subtitle="Enregistrez et gérez vos opérations de caisse (encaissements et décaissements).">
      <app-analyze-with-ai-button
        screenId="cash-desk"
        [payloadBuilder]="buildCashDeskAnalyzePayload"
        [disabled]="loading() || initialLoad()" />
    </app-page-header>

    <div class="page-grid">
      <div class="filters-card">
        <div class="filters-header">
          <h3 class="filters-title">Période</h3>
        </div>
        <div class="filters-row">
          <div class="filter-field">
            <label for="month">Mois</label>
            <p-dropdown
              id="month"
              [options]="monthOptions"
              [(ngModel)]="selectedMonth"
              optionLabel="label"
              optionValue="value"
              (onChange)="onPeriodChange()"
              [showClear]="false"
              styleClass="w-full">
            </p-dropdown>
          </div>
          <div class="filter-field">
            <label for="year">Année</label>
            <p-dropdown
              id="year"
              [options]="yearOptions"
              [(ngModel)]="selectedYear"
              optionLabel="label"
              optionValue="value"
              (onChange)="onPeriodChange()"
              [showClear]="false"
              styleClass="w-full">
            </p-dropdown>
          </div>
          @if (!auth.isFirmDelegatedReadonly()) {
            <div class="filter-action filter-actions">
              <app-button
                variant="outline"
                icon="pi pi-building"
                (click)="openBankDepositWizard()"
                ariaLabel="Remise en banque">
                Remise en banque
              </app-button>
              <app-button
                variant="primary"
                icon="pi pi-plus"
                (click)="openAddDialog()"
                ariaLabel="Enregistrer une opération">
                Enregistrer une opération
              </app-button>
            </div>
          }
        </div>
      </div>

      <div class="balances-card" *ngIf="!loading()">
        <div class="balances-grid">
          <div class="balance-panel">
            <h3 class="panel-title">Solde primaire (mois)</h3>
            <div class="panel-total" [class.negative]="(balances()?.primaryTotal ?? 0) < 0">
              {{ balances()?.primaryTotal | number:'1.3-3' }} {{ balances()?.currency }}
            </div>
            <div class="balance-summary">
              <span class="balance-credit">
                <i class="pi pi-arrow-down-left" aria-hidden="true"></i>
                {{ balances()?.primaryCreditsTotal | number:'1.3-3' }}
              </span>
              <span class="balance-debit">
                <i class="pi pi-arrow-up-right" aria-hidden="true"></i>
                {{ balances()?.primaryDebitsTotal | number:'1.3-3' }}
              </span>
            </div>
            <div class="method-list">
              @for (row of balances()?.primary ?? []; track row.method) {
                <div class="method-row">
                  <span class="method-name">{{ row.methodDisplay }}</span>
                  <span class="method-amount mono" [class.negative]="row.amount < 0">{{ row.amount | number:'1.3-3' }}</span>
                </div>
              }
            </div>
          </div>

          <div class="balance-panel">
            <h3 class="panel-title">Solde secondaire (YTD)</h3>
            <div class="panel-total" [class.negative]="(balances()?.secondaryTotal ?? 0) < 0">
              {{ balances()?.secondaryTotal | number:'1.3-3' }} {{ balances()?.currency }}
            </div>
            <div class="balance-summary">
              <span class="balance-credit">
                <i class="pi pi-arrow-down-left" aria-hidden="true"></i>
                {{ balances()?.secondaryCreditsTotal | number:'1.3-3' }}
              </span>
              <span class="balance-debit">
                <i class="pi pi-arrow-up-right" aria-hidden="true"></i>
                {{ balances()?.secondaryDebitsTotal | number:'1.3-3' }}
              </span>
            </div>
            <div class="method-list">
              @for (row of balances()?.secondary ?? []; track row.method) {
                <div class="method-row">
                  <span class="method-name">{{ row.methodDisplay }}</span>
                  <span class="method-amount mono" [class.negative]="row.amount < 0">{{ row.amount | number:'1.3-3' }}</span>
                </div>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Synthèse de la période (mêmes valeurs que le panneau de soldes, présentation uniforme) -->
      <app-table-totals-bar [metrics]="summaryMetrics()" [loading]="loading()"></app-table-totals-bar>

      <div class="table-card">
        @if (initialLoad()) {
          <app-skeleton-table [rows]="10" [columns]="skeletonColumns"></app-skeleton-table>
        } @else {
          @if (operations().length === 0) {
            @if (auth.isFirmDelegatedReadonly()) {
              <app-empty-state
                illustration="empty-payments.svg"
                title="Aucune opération"
                description="Aucune opération trouvée pour la période sélectionnée.">
              </app-empty-state>
            } @else {
              <app-empty-state
                illustration="empty-payments.svg"
                title="Aucune opération"
                description="Aucune opération trouvée pour la période sélectionnée."
                actionLabel="Enregistrer une opération"
                (actionClick)="openAddDialog()">
              </app-empty-state>
            }
          } @else {
            <p-table
              [value]="operations()"
              [paginator]="true"
              [rows]="pageSize"
              [totalRecords]="totalRecords()"
              [lazy]="false"
              styleClass="p-datatable-sm cash-desk-table"
              (onPage)="onPageChange($event)">

              <ng-template pTemplate="header">
                <tr>
                  <th style="width:130px">Date</th>
                  <th style="width:100px">Type</th>
                  <th style="width:170px">Moyen</th>
                  <th style="min-width:200px">Catégorie</th>
                  <th>Libellé</th>
                  <th style="width:200px">Document</th>
                  <th style="width:140px" class="text-right">Débit</th>
                  <th style="width:140px" class="text-right">Crédit</th>
                  @if (!auth.isFirmDelegatedReadonly()) {
                    <th style="width:80px"></th>
                  }
                </tr>
              </ng-template>

              <ng-template pTemplate="body" let-op>
                <tr class="table-row">
                  <td>{{ op.operationDate | date:'dd/MM/yyyy' }}</td>
                  <td>
                    <span class="type-badge" [class.credit]="op.operationType === CashOperationType.Credit" [class.debit]="op.operationType === CashOperationType.Debit">
                      {{ op.operationTypeDisplay }}
                    </span>
                  </td>
                  <td>{{ op.methodDisplay }}</td>
                  <td class="category-cell">{{ op.categoryDisplay || op.revenueCategoryDisplay }}</td>
                  <td>
                    <div class="label-cell">
                      <span>{{ op.label }}</span>
                      @if (op.origin === CashOperationOrigin.InvoicePayment) {
                        <span class="origin-badge">Encaissement facture</span>
                      }
                      @if (op.origin === CashOperationOrigin.SupplierPayment) {
                        <span class="origin-badge">Paiement fournisseur</span>
                      }
                      @if (op.sourceInvoiceId) {
                        <a
                          [routerLink]="['/invoices', op.sourceInvoiceId]"
                          class="source-link"
                          [attr.aria-label]="'Voir la facture ' + (op.sourceInvoiceNumber || '')">
                          {{ op.sourceInvoiceNumber || 'Voir facture' }}
                        </a>
                      }
                      @if (op.sourceSupplierInvoiceId) {
                        <a
                          [routerLink]="['/supplier-invoices', op.sourceSupplierInvoiceId]"
                          class="source-link"
                          [attr.aria-label]="'Voir la facture fournisseur ' + (op.sourceSupplierInvoiceNumber || '')">
                          {{ op.sourceSupplierInvoiceNumber || 'Voir facture fournisseur' }}
                        </a>
                      }
                    </div>
                  </td>
                  <td><span class="mono">{{ op.document }}</span></td>
                  <td class="text-right amount debit-col">
                    @if (op.operationType === CashOperationType.Debit) {
                      {{ op.amount | number:'1.3-3' }} {{ op.currency }}
                    }
                  </td>
                  <td class="text-right amount credit-col">
                    @if (op.operationType === CashOperationType.Credit) {
                      {{ op.amount | number:'1.3-3' }} {{ op.currency }}
                    }
                  </td>
                  @if (!auth.isFirmDelegatedReadonly()) {
                    <td>
                      <div class="row-actions">
                        @if (
                          op.status === CashOperationStatus.Terminee &&
                          op.origin !== CashOperationOrigin.InvoicePayment &&
                          op.origin !== CashOperationOrigin.SupplierPayment
                        ) {
                          <app-button
                            variant="danger"
                            icon="pi pi-trash"
                            [iconOnly]="true"
                            [iconAlwaysVisible]="true"
                            pTooltip="Annuler l'opération"
                            ariaLabel="Annuler l'opération"
                            (click)="openCancelDialog(op); $event.stopPropagation()">
                          </app-button>
                        } @else if (
                          op.status === CashOperationStatus.Terminee &&
                          (op.origin === CashOperationOrigin.InvoicePayment || op.origin === CashOperationOrigin.SupplierPayment)
                        ) {
                          <app-button
                            variant="danger"
                            icon="pi pi-lock"
                            [iconOnly]="true"
                            [iconAlwaysVisible]="true"
                            [disabled]="true"
                            pTooltip="Géré via la facture"
                            ariaLabel="Opération gérée via la facture">
                          </app-button>
                        }
                      </div>
                    </td>
                  }
                </tr>
              </ng-template>
            </p-table>
          }
        }
      </div>
    </div>

    <app-add-cash-operation-dialog
      [(visible)]="addDialogVisible"
      (operationCreated)="onOperationCreated()">
    </app-add-cash-operation-dialog>

    <app-bank-deposit-wizard
      [(visible)]="bankDepositWizardVisible"
      [year]="selectedYear"
      [month]="selectedMonth"
      (depositCreated)="onBankDepositCreated()">
    </app-bank-deposit-wizard>

    <!-- Cancel dialog -->
    <p-dialog
      header="Annuler l'opération"
      [(visible)]="cancelDialogVisible"
      [modal]="true"
      [style]="{ width: '480px' }"
      [draggable]="false"
      [closable]="true"
      (onHide)="closeCancelDialog()">

      <div class="dialog-body">
        <p class="dialog-message">
          Indiquez le motif d'annulation (obligatoire).
        </p>
        <textarea
          pInputTextarea
          [(ngModel)]="cancelReason"
          rows="4"
          class="w-full"
          [maxLength]="500"
          aria-label="Motif d'annulation">
        </textarea>
        @if (cancelError()) {
          <div class="alert" role="alert">{{ cancelError() }}</div>
        }
      </div>

      <ng-template pTemplate="footer">
        <div class="dialog-footer">
          <app-button variant="outline" (click)="closeCancelDialog()" ariaLabel="Retour">Retour</app-button>
          <app-button
            variant="danger"
            icon="pi pi-times"
            iconPos="left"
            [disabled]="!cancelReason.trim() || cancelling()"
            (click)="submitCancel()" ariaLabel="Annuler l'opération">
            Annuler
          </app-button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .page-grid { display: flex; flex-direction: column; gap: var(--spacing-6); }
    .filters-card, .balances-card, .table-card { background: var(--color-background-elevated); border: 1px solid var(--color-border-subtle); border-radius: var(--radius-xl); box-shadow: var(--shadow-sm); }
    .filters-card { padding: var(--spacing-5); }
    .filters-header { margin-bottom: var(--spacing-4); display: flex; align-items: center; justify-content: space-between; }
    .filters-title { margin: 0; font-size: var(--font-size-lg); font-weight: var(--font-weight-semibold); color: var(--color-text-primary); }
    .filters-row { display: grid; grid-template-columns: 1fr 1fr auto; gap: var(--spacing-4); align-items: end; }
    .filter-field { display: flex; flex-direction: column; gap: var(--spacing-2); }
    .filter-field label { color: var(--color-text-secondary); font-size: var(--font-size-sm); font-weight: var(--font-weight-medium); }
    .filter-action { display: flex; justify-content: flex-end; }
    .filter-actions { display: flex; flex-wrap: wrap; gap: var(--spacing-3); justify-content: flex-end; align-items: center; }

    .balances-card { padding: var(--spacing-5); }
    .balances-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--spacing-4); }
    .balance-panel { background: rgba(20, 184, 166, 0.06); border: 1px solid var(--color-border-subtle); border-radius: var(--radius-lg); padding: var(--spacing-4); }
    .panel-title { margin: 0 0 var(--spacing-3) 0; color: var(--color-text-secondary); font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); text-transform: uppercase; letter-spacing: 0.04em; }
    .panel-total { font-size: var(--font-size-lg); font-weight: var(--font-weight-bold); color: var(--color-text-primary); margin-bottom: var(--spacing-2); font-family: var(--font-family-mono, 'JetBrains Mono', monospace); }
    .panel-total.negative { color: var(--color-danger-600); }

    .balance-summary { display: flex; gap: var(--spacing-4); margin-bottom: var(--spacing-4); font-size: var(--font-size-xs); }
    .balance-credit { color: var(--color-success-600); display: flex; align-items: center; gap: var(--spacing-1); font-family: var(--font-family-mono, 'JetBrains Mono', monospace); font-weight: var(--font-weight-medium); }
    .balance-debit { color: var(--color-danger-600); display: flex; align-items: center; gap: var(--spacing-1); font-family: var(--font-family-mono, 'JetBrains Mono', monospace); font-weight: var(--font-weight-medium); }
    .balance-credit i, .balance-debit i { font-size: 0.65rem; }

    .method-list { display: flex; flex-direction: column; gap: var(--spacing-2); }
    .method-row { display: flex; align-items: center; justify-content: space-between; gap: var(--spacing-3); padding: var(--spacing-2) 0; border-bottom: 1px dashed rgba(255, 255, 255, 0.08); }
    .method-row:last-child { border-bottom: none; }
    .method-name { color: var(--color-text-secondary); font-size: var(--font-size-sm); }
    .method-amount { color: var(--color-text-primary); font-size: var(--font-size-sm); }
    .method-amount.negative { color: var(--color-danger-600); }
    .mono { font-family: var(--font-family-mono, 'JetBrains Mono', monospace); font-weight: var(--font-weight-semibold); }

    .table-card { padding: var(--spacing-4) var(--spacing-5); }
    :host ::ng-deep .cash-desk-table {
      .p-datatable-thead > tr > th {
        background: var(--color-neutral-50);
        position: sticky;
        top: 0;
        z-index: 1;
        font-size: var(--font-size-xs);
        text-transform: uppercase;
        letter-spacing: 0.5px;
      }
      .p-datatable-tbody > tr.table-row > td { border-bottom: 1px solid var(--color-border-subtle); }
    }
    .text-right { text-align: right; }
    .amount { font-family: var(--font-family-mono, 'JetBrains Mono', monospace); font-weight: var(--font-weight-semibold); }
    .debit-col { color: var(--color-danger-600); }
    .credit-col { color: var(--color-success-600); }
    .category-cell { font-size: var(--font-size-sm); color: var(--color-text-secondary); max-width: 320px; line-height: 1.35; }
    .label-cell { display: flex; flex-direction: column; gap: var(--spacing-1); }
    .origin-badge {
      display: inline-flex;
      width: fit-content;
      padding: 0.1rem 0.45rem;
      border-radius: var(--radius-md);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      color: var(--color-info-700);
      background: color-mix(in srgb, var(--color-info-500) 10%, transparent);
      border: 1px solid color-mix(in srgb, var(--color-info-500) 25%, transparent);
    }
    .source-link {
      font-size: var(--font-size-xs);
      color: var(--color-primary-600);
      text-decoration: none;
      width: fit-content;
    }
    .source-link:hover { text-decoration: underline; }
    .row-actions { display: flex; gap: var(--spacing-2); justify-content: flex-end; }

    .type-badge {
      display: inline-flex;
      align-items: center;
      padding: 0.125rem 0.5rem;
      border-radius: var(--radius-md);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    .type-badge.debit {
      background: rgba(220, 38, 38, 0.08);
      color: var(--color-danger-700);
      border: 1px solid rgba(220, 38, 38, 0.15);
    }
    .type-badge.credit {
      background: rgba(22, 163, 74, 0.08);
      color: var(--color-success-700);
      border: 1px solid rgba(22, 163, 74, 0.15);
    }

    .dialog-body { padding: var(--spacing-4) 0; }
    .dialog-message { color: var(--color-text-secondary); margin: 0 0 var(--spacing-4) 0; }
    .alert { margin-top: var(--spacing-3); padding: var(--spacing-3) var(--spacing-4); border-radius: var(--radius-lg); border: 1px solid var(--color-danger-300); background: rgba(220, 38, 38, 0.06); font-size: var(--font-size-sm); }
    .dialog-footer { display: flex; justify-content: flex-end; gap: var(--spacing-3); }

    @media (max-width: 980px) {
      .filters-row { grid-template-columns: 1fr; }
      .filter-action { justify-content: flex-start; }
      .balances-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class CashDeskComponent implements OnInit {
  private readonly cashDeskService = inject(CashDeskService);
  private readonly toastService = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly monthOptions = MONTH_OPTIONS;
  selectedMonth = new Date().getMonth() + 1;
  selectedYear = new Date().getFullYear();
  yearOptions: { label: string; value: number }[] = [];

  readonly CashOperationStatus = CashOperationStatus;
  readonly CashOperationType = CashOperationType;
  readonly CashOperationOrigin = CashOperationOrigin;

  loading = signal(true);
  initialLoad = signal(true);
  balances = signal<CashDeskBalances | null>(null);
  operations = signal<CashOperationListItem[]>([]);
  totalRecords = signal(0);

  /**
   * Synthèse de la période sous forme de tuiles (présentation uniforme avec le reste de l'app).
   * Réutilise les valeurs DÉJÀ calculées dans balances() — aucun calcul ni endpoint nouveau.
   */
  summaryMetrics = computed<TotalMetric[]>(() => {
    const b = this.balances();
    const currency = b?.currency ?? 'TND';
    return [
      { label: 'Solde caisse (mois)', value: b?.primaryTotal, format: 'currency', currency, icon: 'pi-wallet', tone: 'primary' },
      { label: 'Encaissements', value: b?.primaryCreditsTotal, format: 'currency', currency, icon: 'pi-arrow-down-left', tone: 'emerald' },
      { label: 'Décaissements', value: b?.primaryDebitsTotal, format: 'currency', currency, icon: 'pi-arrow-up-right', tone: 'rose' },
      { label: 'Opérations', value: this.totalRecords(), format: 'number', icon: 'pi-list', tone: 'cyan' }
    ];
  });

  pageSize = 10;
  currentPage = 0;

  addDialogVisible = false;
  bankDepositWizardVisible = false;

  cancelDialogVisible = false;
  private operationToCancel: CashOperationListItem | null = null;
  cancelReason = '';
  cancelError = signal('');
  cancelling = signal(false);

  skeletonColumns: SkeletonColumn[] = [
    { width: '130px' },
    { width: '100px' },
    { width: '170px' },
    { width: '200px' },
    { width: '220px' },
    { width: '200px' },
    { width: '140px' },
    { width: '140px' },
    { width: '80px' }
  ];

  ngOnInit(): void {
    const now = new Date();
    const currentYear = now.getFullYear();
    this.yearOptions = Array.from({ length: 6 }, (_, i) => {
      const y = currentYear - 1 + i;
      return { label: String(y), value: y };
    });

    this.loadAll();
  }

  private periodParams() {
    return {
      year: this.selectedYear,
      month: this.selectedMonth
    };
  }

  private loadOperations(): void {
    const { year, month } = this.periodParams();
    this.cashDeskService.getOperations(year, month, this.currentPage + 1, this.pageSize).subscribe({
      next: res => {
        if (res.success && res.data) {
          this.operations.set(res.data.items);
          this.totalRecords.set(res.data.totalCount);
        } else {
          this.operations.set([]);
          this.totalRecords.set(0);
        }
      },
      error: () => {
        this.toastService.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Impossible de charger les opérations.'
        });
      }
    });
  }

  private loadAll(): void {
    this.loading.set(true);
    this.initialLoad.set(true);

    const { year, month } = this.periodParams();
    const monthPadded = String(month).padStart(2, '0');
    this.cashDeskService.invalidateCachesAfterCashLedgerMutation(`${year}-${monthPadded}-01`);
    const balances$ = this.cashDeskService.getBalances(year, month);
    const operations$ = this.cashDeskService.getOperations(year, month, this.currentPage + 1, this.pageSize);

    forkJoin({ balances: balances$, operations: operations$ }).subscribe({
      next: ({ balances, operations }) => {
        if (balances.success && balances.data) this.balances.set(balances.data);
        if (operations.success && operations.data) {
          this.operations.set(operations.data.items);
          this.totalRecords.set(operations.data.totalCount);
        }
      },
      error: () => {
        this.loading.set(false);
        this.initialLoad.set(false);
        this.toastService.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Impossible de charger la caisse de trésorerie.'
        });
      },
      complete: () => {
        this.loading.set(false);
        this.initialLoad.set(false);
      }
    });
  }

  onPeriodChange(): void {
    this.currentPage = 0;
    this.loadAll();
  }

  onPageChange(event: any): void {
    this.currentPage = event.page ?? 0;
    this.pageSize = event.rows ?? this.pageSize;
    this.loadOperations();
  }

  openAddDialog(): void {
    if (this.auth.isFirmDelegatedReadonly()) return;
    this.addDialogVisible = true;
  }

  openBankDepositWizard(): void {
    if (this.auth.isFirmDelegatedReadonly()) return;
    this.bankDepositWizardVisible = true;
  }

  onBankDepositCreated(): void {
    this.loadAll();
  }

  onOperationCreated(): void {
    this.loadAll();
  }

  openCancelDialog(op: CashOperationListItem): void {
    if (this.auth.isFirmDelegatedReadonly()) return;
    this.operationToCancel = op;
    this.cancelReason = '';
    this.cancelError.set('');
    this.cancelDialogVisible = true;
  }

  closeCancelDialog(): void {
    this.cancelDialogVisible = false;
    this.cancelReason = '';
    this.cancelError.set('');
    this.cancelling.set(false);
    this.operationToCancel = null;
  }

  readonly buildCashDeskAnalyzePayload = (): unknown => {
    const b = this.balances();
    const ops = this.operations();
    const { rows, sampling } = sampleRowsSmart(ops, 50, 'recent', op => Math.abs(op.amount));
    const totalPages = Math.ceil(this.totalRecords() / this.pageSize) || 1;
    const warnings: string[] = [];
    if (totalPages > 1) {
      warnings.push(`Seule la page ${this.currentPage + 1}/${totalPages} est incluse dans l'échantillon`);
    }

    return buildScreenAnalysisPayloadV2({
      screenId: 'cash-desk',
      filters: { year: this.selectedYear, month: this.selectedMonth },
      summary: {
        primaryTotal: b?.primaryTotal ?? null,
        secondaryTotal: b?.secondaryTotal ?? null,
        primaryCreditsTotal: b?.primaryCreditsTotal ?? null,
        primaryDebitsTotal: b?.primaryDebitsTotal ?? null,
        secondaryCreditsTotal: b?.secondaryCreditsTotal ?? null,
        secondaryDebitsTotal: b?.secondaryDebitsTotal ?? null,
        netPrimary: b ? b.primaryCreditsTotal - b.primaryDebitsTotal : null,
        netSecondary: b ? b.secondaryCreditsTotal - b.secondaryDebitsTotal : null,
        operationsOnPage: ops.length,
        totalOperations: this.totalRecords()
      },
      highlights: b
        ? [
            {
              type: 'top' as const,
              label: 'Solde caisse primaire',
              value: b.primaryTotal,
              context: b.currency
            },
            {
              type: 'top' as const,
              label: 'Solde banque secondaire',
              value: b.secondaryTotal,
              context: b.currency
            }
          ]
        : [],
      rows: rows.map(op => ({
        date: op.operationDate,
        type: op.operationTypeDisplay,
        method: op.methodDisplay,
        label: op.label,
        amount: op.amount,
        currency: op.currency,
        debitOrCredit: op.operationType === CashOperationType.Debit ? 'debit' : 'credit',
        status: op.status
      })),
      sampling,
      dataQuality: {
        hasData: !!b || ops.length > 0,
        isPartial: totalPages > 1 || sampling.truncated,
        warnings
      },
      extra: {
        balances: b
          ? {
              currency: b.currency,
              primary: b.primary?.map(r => ({ method: r.methodDisplay, amount: r.amount })),
              secondary: b.secondary?.map(r => ({ method: r.methodDisplay, amount: r.amount }))
            }
          : null,
        pagination: {
          page: this.currentPage + 1,
          pageSize: this.pageSize,
          totalRecords: this.totalRecords(),
          totalPages
        }
      }
    });
  };

  submitCancel(): void {
    if (!this.operationToCancel) return;

    const reason = this.cancelReason.trim();
    if (!reason) {
      this.cancelError.set('Le motif d\u2019annulation est obligatoire.');
      return;
    }

    if (this.cancelling()) return;
    this.cancelling.set(true);
    this.cancelError.set('');

    this.cashDeskService.cancelOperation(this.operationToCancel.id, reason).subscribe({
      next: res => {
        if (res.success) {
          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: 'Opération annulée.'
          });
          this.loadAll();
          this.cancelDialogVisible = false;
        } else {
          this.cancelError.set(res.message ?? 'Impossible d\u2019annuler l\u2019opération.');
        }
        this.cancelling.set(false);
      },
      error: (err: HttpErrorResponse) => {
        const msg =
          (err.error as any)?.errors?.[0] ??
          (err.error as any)?.message ??
          (err.error as any)?.error?.description ??
          'Erreur lors de l\u2019annulation.';
        this.cancelError.set(msg);
        this.cancelling.set(false);
      }
    });
  }
}
