import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { formatLocalDate } from '@core/utils/date.util';
import {
  PaymentsReportsService,
  PaymentsReportsData,
  ReportPeriod
} from '../services/payments-reports.service';
import { ReportsApiService, ClientPaymentReportRow, SupplierPaymentReportRow } from '@core/services/reports-api.service';

interface PeriodOption {
  label: string;
  value: ReportPeriod;
}

@Component({
  selector: 'app-payments-reports',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    PageHeaderComponent,
    StatCardComponent,
    ButtonComponent
  ],
  template: `
    <app-page-header 
      title="Rapports Paiements" 
      subtitle="Encaissements, décaissements et trésorerie nette">
      <app-button 
        variant="secondary"
        icon="pi-download"
        iconPos="left"
        (click)="onExport()">
        Exporter
      </app-button>
    </app-page-header>

    <div class="filter-section">
      <div class="filter-group">
        <label for="period" class="filter-label">Période</label>
        <select 
          id="period"
          [(ngModel)]="selectedPeriod" 
          (ngModelChange)="onPeriodChange()"
          class="filter-select"
          aria-label="Sélectionner la période">
          @for (period of periodOptions; track period.value) {
            <option [value]="period.value">{{ period.label }}</option>
          }
        </select>
      </div>
    </div>

    @if (!loading() && reportsData()?.summaryText) {
      <div class="summary-block" role="status" aria-live="polite">
        <i class="pi pi-info-circle summary-icon"></i>
        <p class="summary-text">{{ reportsData()!.summaryText }}</p>
      </div>
    }

    @if (loading()) {
      <div class="stats-grid">
        <div class="stat-skeleton"></div>
        <div class="stat-skeleton"></div>
        <div class="stat-skeleton"></div>
        <div class="stat-skeleton"></div>
        <div class="stat-skeleton"></div>
      </div>
    } @else if (reportsData()) {
      <div class="stats-grid">
        <app-stat-card
          label="Encaissements"
          [value]="reportsData()!.totalInflows"
          icon="pi-arrow-down"
          variant="success">
        </app-stat-card>
        <app-stat-card
          label="Décaissements"
          [value]="reportsData()!.totalOutflows"
          icon="pi-arrow-up"
          variant="primary">
        </app-stat-card>
        <app-stat-card
          label="Trésorerie nette"
          [value]="reportsData()!.netCashFlow"
          icon="pi-wallet"
          [variant]="getNetVariant()">
        </app-stat-card>
        <app-stat-card
          label="Factures clients en attente"
          [value]="reportsData()!.pendingClientAmount"
          icon="pi-clock"
          variant="warning">
        </app-stat-card>
        <app-stat-card
          label="Factures fournisseurs en attente"
          [value]="reportsData()!.pendingSupplierAmount"
          icon="pi-clock"
          variant="warning">
        </app-stat-card>
      </div>
    }

    <div class="section">
      <div class="section-header">
        <h2 class="section-title">Évolution des flux de trésorerie</h2>
        <span class="section-subtitle">Encaissements et décaissements sur la période</span>
      </div>
      @if (loading()) {
        <div class="chart-placeholder">
          <div class="loading-placeholder">
            <i class="pi pi-spin pi-spinner"></i>
            <span>Chargement...</span>
          </div>
        </div>
      } @else if (reportsData()?.cashFlowChartData && reportsData()!.cashFlowChartData.length > 0) {
        <div class="chart-container" role="img" aria-label="Graphique des flux de trésorerie">
          <div class="chart-bars">
            @for (item of reportsData()!.cashFlowChartData; track item.label) {
              <div class="chart-bar-group">
                <div class="chart-tooltip" aria-hidden="true">
                  <strong>Encaissements:</strong> {{ formatCurrency(item.inflows) }}<br>
                  <strong>Décaissements:</strong> {{ formatCurrency(item.outflows) }}<br>
                  <strong>Net:</strong> {{ formatCurrency(item.net) }}
                </div>
                <div class="chart-bar-wrapper">
                  <div class="chart-bar chart-bar-in" [style.height.%]="getBarHeight(item.inflows, 'in')" title="Encaissements"></div>
                  <div class="chart-bar chart-bar-out" [style.height.%]="getBarHeight(item.outflows, 'out')" title="Décaissements"></div>
                </div>
                <span class="chart-label">{{ item.labelShort }}</span>
              </div>
            }
          </div>
          <div class="chart-legend">
            <span class="legend-item"><span class="legend-color in"></span> Encaissements</span>
            <span class="legend-item"><span class="legend-color out"></span> Décaissements</span>
          </div>
        </div>
      } @else {
        <div class="chart-placeholder">
          <div class="chart-content">
            <i class="pi pi-chart-line chart-icon"></i>
            <p class="chart-text">Aucune donnée pour cette période</p>
            <p class="chart-subtext">Les flux de trésorerie apparaîtront ici une fois que vous aurez des factures payées (clients et fournisseurs).</p>
          </div>
        </div>
      }
    </div>

    <div class="section">
      <div class="section-header">
        <h2 class="section-title">Paiements clients</h2>
        <span class="section-subtitle">Liste des encaissements sur la période</span>
      </div>
      @if (clientPaymentsLoading()) {
        <div class="loading-placeholder">
          <i class="pi pi-spin pi-spinner"></i>
          <span>Chargement...</span>
        </div>
      } @else if (!clientPayments().length) {
        <div class="empty-placeholder">
          <i class="pi pi-info-circle"></i>
          <p>Aucun paiement client sur cette période</p>
        </div>
      } @else {
        <p-table
          [value]="clientPayments()"
          styleClass="p-datatable-sm reports-table"
          aria-label="Paiements clients">
          <ng-template pTemplate="header">
            <tr>
              <th>Date</th>
              <th>Client</th>
              <th>Facture</th>
              <th class="text-right">Montant</th>
              <th>Mode</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.paymentDate | date:'dd/MM/yyyy' }}</td>
              <td>{{ row.clientName }}</td>
              <td>{{ row.invoiceNumber }}</td>
              <td class="text-right amount">{{ row.amount | number:'1.3-3' }} {{ row.currency }}</td>
              <td>{{ row.methodDisplay }}</td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <div class="section">
      <div class="section-header">
        <h2 class="section-title">Paiements fournisseurs</h2>
        <span class="section-subtitle">Liste des décaissements sur la période</span>
      </div>
      @if (supplierPaymentsLoading()) {
        <div class="loading-placeholder">
          <i class="pi pi-spin pi-spinner"></i>
          <span>Chargement...</span>
        </div>
      } @else if (!supplierPayments().length) {
        <div class="empty-placeholder">
          <i class="pi pi-info-circle"></i>
          <p>Aucun paiement fournisseur sur cette période</p>
        </div>
      } @else {
        <p-table
          [value]="supplierPayments()"
          styleClass="p-datatable-sm reports-table"
          aria-label="Paiements fournisseurs">
          <ng-template pTemplate="header">
            <tr>
              <th>Date</th>
              <th>Fournisseur</th>
              <th>Facture</th>
              <th class="text-right">Montant</th>
              <th>Mode</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.paymentDate | date:'dd/MM/yyyy' }}</td>
              <td>{{ row.supplierName }}</td>
              <td>{{ row.invoiceNumber }}</td>
              <td class="text-right amount">{{ row.amount | number:'1.3-3' }} {{ row.currency }}</td>
              <td>{{ row.methodDisplay }}</td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
  `,
  styles: [`
    .filter-section {
      background: var(--color-background-elevated);
      border-radius: var(--radius-xl);
      padding: var(--spacing-4);
      margin-bottom: var(--spacing-6);
      box-shadow: var(--shadow-sm);
      border: 1px solid var(--color-border-subtle);
    }

    .filter-group { display: flex; align-items: center; gap: var(--spacing-4); }
    .filter-label { font-size: var(--font-size-sm); font-weight: var(--font-weight-medium); color: var(--color-text-secondary); }
    .filter-select {
      padding: var(--spacing-2) var(--spacing-3);
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-md);
      background: var(--color-background);
      color: var(--color-text-primary);
      font-size: var(--font-size-sm);
      cursor: pointer;
    }

    .summary-block {
      background: var(--color-primary-50);
      border-radius: var(--radius-lg);
      padding: var(--spacing-4);
      margin-bottom: var(--spacing-6);
      border: 1px solid var(--color-primary-100);
      display: flex;
      align-items: flex-start;
      gap: var(--spacing-3);
    }

    .summary-icon { color: var(--color-primary-600); font-size: 1.25rem; flex-shrink: 0; }
    .summary-text { margin: 0; font-size: var(--font-size-sm); color: var(--color-text-primary); line-height: 1.5; }

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: var(--spacing-4);
      margin-bottom: var(--spacing-6);
    }

    .stat-skeleton {
      background: var(--color-background-elevated);
      border-radius: var(--radius-xl);
      padding: var(--spacing-5);
      height: 120px;
      border: 1px solid var(--color-border-subtle);
      animation: pulse 1.5s ease-in-out infinite;
    }

    @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.5; } }

    .section {
      background: var(--color-background-elevated);
      border-radius: var(--radius-xl);
      padding: var(--spacing-6);
      box-shadow: var(--shadow-sm);
      border: 1px solid var(--color-border-subtle);
    }

    .section-header {
      margin-bottom: var(--spacing-6);
      padding-bottom: var(--spacing-4);
      border-bottom: 2px solid var(--color-border-subtle);
    }

    .section-title {
      font-size: var(--font-size-2xl);
      font-weight: var(--font-weight-bold);
      color: var(--color-text-primary);
      margin: 0;
    }

    .section-title::before {
      content: '';
      display: inline-block;
      width: 4px;
      height: 24px;
      background: linear-gradient(180deg, #14b8a6, #0d9488);
      border-radius: var(--radius-full);
      margin-right: var(--spacing-2);
      vertical-align: middle;
    }

    .section-subtitle { font-size: var(--font-size-sm); color: var(--color-text-secondary); width: 100%; }

    .chart-placeholder {
      min-height: 300px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--color-neutral-50);
      border-radius: var(--radius-lg);
      border: 2px dashed var(--color-border-subtle);
    }

    .chart-content { text-align: center; color: var(--color-text-secondary); }
    .chart-icon { font-size: 3rem; color: #14b8a6; margin-bottom: var(--spacing-3); }
    .chart-text { font-size: var(--font-size-lg); font-weight: var(--font-weight-semibold); margin-bottom: var(--spacing-2); color: var(--color-text-primary); }
    .chart-subtext { font-size: var(--font-size-sm); color: var(--color-text-secondary); }

    .chart-container { padding: var(--spacing-4) 0; }
    .chart-bars {
      display: flex;
      align-items: flex-end;
      justify-content: space-around;
      height: 220px;
      gap: var(--spacing-3);
      padding: 0 var(--spacing-2);
    }

    .chart-bar-group {
      display: flex;
      flex-direction: column;
      align-items: center;
      flex: 1;
      max-width: 80px;
      position: relative;
      height: 100%;
      justify-content: flex-end;
    }

    .chart-bar-wrapper {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: flex-end;
      justify-content: center;
      gap: 2px;
      position: relative;
    }

    .chart-bar {
      width: 45%;
      min-height: 4px;
      border-radius: var(--radius-md) var(--radius-md) 0 0;
      transition: height 0.8s cubic-bezier(0.34, 1.56, 0.64, 1);
      cursor: pointer;
    }

    .chart-bar-in { background: linear-gradient(180deg, #34d399, #10b981); }
    .chart-bar-out { background: linear-gradient(180deg, #60a5fa, #3b82f6); }

    .chart-tooltip {
      position: absolute;
      top: -8px;
      left: 50%;
      transform: translateX(-50%) translateY(-100%);
      background: var(--color-text-primary);
      color: var(--color-background);
      padding: var(--spacing-2) var(--spacing-3);
      border-radius: var(--radius-md);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      white-space: nowrap;
      opacity: 0;
      pointer-events: none;
      transition: opacity var(--transition-fast);
      z-index: 10;
      text-align: left;
      line-height: 1.5;
    }

    .chart-bar-group:hover .chart-tooltip { opacity: 1; }
    .chart-label { margin-top: var(--spacing-2); font-size: var(--font-size-xs); font-weight: var(--font-weight-medium); color: var(--color-text-secondary); text-transform: uppercase; }

    .chart-legend {
      display: flex;
      justify-content: center;
      gap: var(--spacing-6);
      margin-top: var(--spacing-4);
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }

    .legend-item { display: flex; align-items: center; gap: var(--spacing-2); }
    .legend-color { width: 12px; height: 12px; border-radius: var(--radius-sm); }
    .legend-color.in { background: linear-gradient(180deg, #34d399, #10b981); }
    .legend-color.out { background: linear-gradient(180deg, #60a5fa, #3b82f6); }

    .loading-placeholder,
    .empty-placeholder {
      padding: var(--spacing-8);
      text-align: center;
      color: var(--color-text-secondary);
    }

    .empty-placeholder p { margin: 0; }
    .text-right { text-align: right; }
    .amount { font-family: 'JetBrains Mono', 'SF Mono', 'Monaco', 'Consolas', monospace; font-weight: var(--font-weight-semibold); }

    :host ::ng-deep .reports-table .p-datatable-thead > tr > th {
      background: var(--color-neutral-50);
      color: var(--color-text-secondary);
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-sm);
      text-transform: uppercase;
      padding: var(--spacing-4) var(--spacing-3);
      border-bottom: 2px solid var(--color-border-default);
    }
    :host ::ng-deep .reports-table .p-datatable-tbody > tr > td {
      padding: var(--spacing-4) var(--spacing-3);
      border-bottom: 1px solid var(--color-border-subtle);
      font-size: var(--font-size-sm);
      vertical-align: middle;
    }

    @media (max-width: 768px) {
      .stats-grid { grid-template-columns: repeat(2, 1fr); }
      .section { padding: var(--spacing-4); }
      .section-title { font-size: var(--font-size-xl); }
      .section-title::before { display: none; }
    }
  `]
})
export class PaymentsReportsComponent implements OnInit {
  reportsService = inject(PaymentsReportsService);
  reportsApi = inject(ReportsApiService);

  loading = signal(true);
  reportsData = signal<PaymentsReportsData | null>(null);
  clientPayments = signal<ClientPaymentReportRow[]>([]);
  supplierPayments = signal<SupplierPaymentReportRow[]>([]);
  clientPaymentsLoading = signal(false);
  supplierPaymentsLoading = signal(false);
  selectedPeriod: ReportPeriod = 'month';
  private maxInflow = 0;
  private maxOutflow = 0;

  periodOptions: PeriodOption[] = [
    { label: 'Cette semaine', value: 'week' },
    { label: 'Ce mois', value: 'month' },
    { label: 'Ce trimestre', value: 'quarter' },
    { label: 'Cette année', value: 'year' },
    { label: 'Tout', value: 'all' }
  ];

  ngOnInit(): void {
    this.loadReportsData();
  }

  onPeriodChange(): void {
    this.loadReportsData();
  }

  loadReportsData(): void {
    this.loading.set(true);
    const { fromDate, toDate } = this.getPeriodDates();
    this.reportsService.loadReportsData(this.selectedPeriod).subscribe({
      next: (data) => {
        this.reportsData.set(data);
        const inflows = data.cashFlowChartData.map(d => d.inflows);
        const outflows = data.cashFlowChartData.map(d => d.outflows);
        this.maxInflow = Math.max(...inflows, 1);
        this.maxOutflow = Math.max(...outflows, 1);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
    this.loadClientPayments(fromDate, toDate);
    this.loadSupplierPayments(fromDate, toDate);
  }

  private getPeriodDates(): { fromDate: string; toDate: string } {
    const to = new Date();
    const toDate = formatLocalDate(to);
    if (this.selectedPeriod === 'all') {
      return { fromDate: '2020-01-01', toDate };
    }
    const daysMap: Record<Exclude<ReportPeriod, 'all'>, number> = {
      week: 7,
      month: 30,
      quarter: 90,
      year: 365
    };
    const from = new Date();
    from.setDate(from.getDate() - daysMap[this.selectedPeriod]);
    return { fromDate: formatLocalDate(from), toDate };
  }

  private loadClientPayments(fromDate: string, toDate: string): void {
    this.clientPaymentsLoading.set(true);
    this.reportsApi.getClientPayments(fromDate, toDate).subscribe({
      next: (res) => {
        this.clientPayments.set(res.success && res.data ? res.data : []);
        this.clientPaymentsLoading.set(false);
      },
      error: () => {
        this.clientPayments.set([]);
        this.clientPaymentsLoading.set(false);
      }
    });
  }

  private loadSupplierPayments(fromDate: string, toDate: string): void {
    this.supplierPaymentsLoading.set(true);
    this.reportsApi.getSupplierPayments(fromDate, toDate).subscribe({
      next: (res) => {
        this.supplierPayments.set(res.success && res.data ? res.data : []);
        this.supplierPaymentsLoading.set(false);
      },
      error: () => {
        this.supplierPayments.set([]);
        this.supplierPaymentsLoading.set(false);
      }
    });
  }

  getNetVariant(): 'success' | 'error' | 'primary' {
    const data = this.reportsData();
    if (!data) return 'primary';
    return data.netCashFlowAmount >= 0 ? 'success' : 'error';
  }

  getBarHeight(amount: number, type: 'in' | 'out'): number {
    const max = type === 'in' ? this.maxInflow : this.maxOutflow;
    if (max === 0) return 4;
    const percent = (amount / max) * 100;
    return Math.max(percent, 4);
  }

  formatCurrency(amount: number): string {
    const formatted = new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 3, maximumFractionDigits: 3 }).format(amount);
    return `${formatted} TND`;
  }

  onExport(): void {
    const data = this.reportsData();
    if (!data) return;

    const rows: string[][] = [];
    rows.push(['Rapports Paiements InstaFact', '']);
    rows.push(['Période', this.periodOptions.find(p => p.value === this.selectedPeriod)?.label || this.selectedPeriod]);
    rows.push(['']);
    rows.push(['Indicateurs', 'Valeur']);
    rows.push(['Encaissements', data.totalInflows]);
    rows.push(['Décaissements', data.totalOutflows]);
    rows.push(['Trésorerie nette', data.netCashFlow]);
    rows.push(['Factures clients en attente', data.pendingClientAmount]);
    rows.push(['Factures fournisseurs en attente', data.pendingSupplierAmount]);
    rows.push(['Factures payées (clients)', data.paidClientCount.toString()]);
    rows.push(['Factures payées (fournisseurs)', data.paidSupplierCount.toString()]);

    const csv = rows.map(row => row.map(cell => `"${String(cell).replace(/"/g, '""')}"`).join(';')).join('\n');
    const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `rapports-paiements-${formatLocalDate(new Date())}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
}
