import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { TabViewModule } from 'primeng/tabview';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { formatLocalDate } from '@core/utils/date.util';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import {
  ReportsApiService,
  ProductPerformanceReportRow,
  ProductSalesTrendReportRow,
  ProductNeverSoldReportRow,
  SalesRevenueReportRow,
  BasketMetricsReportDto
} from '@core/services/reports-api.service';
import { RevenueTableComponent } from '../sales-by-line/revenue-table.component';

@Component({
  selector: 'app-product-sales-analytics',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    TabViewModule,
    PageHeaderComponent,
    ButtonComponent,
    StatCardComponent,
    RevenueTableComponent
  ],
  template: `
    <app-page-header
      title="Rapports décisionnels ventes produits"
      subtitle="Performance, tendances, panier moyen et produits à mettre en avant">
      <app-button variant="secondary" icon="pi-download" iconPos="left" (click)="onExport()">
        Exporter
      </app-button>
    </app-page-header>

    <div class="filter-section">
      <div class="filter-group">
        <label for="period" class="filter-label">Période</label>
        <select id="period" [(ngModel)]="selectedPeriod" (ngModelChange)="onPeriodChange()" class="filter-select" aria-label="Période">
          @for (p of periodOptions; track p.value) {
            <option [value]="p.value">{{ p.label }}</option>
          }
        </select>
      </div>
    </div>

    <p-tabView styleClass="ft-tabs" (onChange)="onTabChange($event)">
      <p-tabPanel header="Performance produits" leftIcon="pi pi-chart-bar">
        <div class="tab-content">
          <p class="tab-desc">Classement par CA avec marge et part du chiffre d'affaires.</p>
          @if (loading()) {
            <div class="loading-placeholder"><i class="pi pi-spin pi-spinner"></i> Chargement...</div>
          } @else if (!productPerformance().length) {
            <div class="empty-placeholder"><i class="pi pi-info-circle"></i> Aucune donnée pour cette période.</div>
          } @else {
            <p-table [value]="productPerformance()" styleClass="p-datatable-sm reports-table">
              <ng-template pTemplate="header">
                <tr>
                  <th>Produit</th>
                  <th>Catégorie</th>
                  <th class="text-right">Quantité</th>
                  <th class="text-right">CA</th>
                  <th class="text-right">Coût</th>
                  <th class="text-right">Marge</th>
                  <th class="text-right">Marge %</th>
                  <th class="text-right">Part CA %</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td><strong>{{ row.productName }}</strong><br><span class="code">{{ row.productCode }}</span></td>
                  <td>{{ row.categoryName ?? '–' }}</td>
                  <td class="text-right">{{ row.quantitySold | number:'1.3-3' }}</td>
                  <td class="text-right amount">{{ row.revenue | number:'1.3-3' }} {{ row.currency }}</td>
                  <td class="text-right">{{ row.totalCost | number:'1.3-3' }}</td>
                  <td class="text-right amount">{{ row.profit | number:'1.3-3' }}</td>
                  <td class="text-right">{{ row.marginPercent != null ? (row.marginPercent | number:'1.1-1') + ' %' : '–' }}</td>
                  <td class="text-right">{{ row.revenueSharePercent | number:'1.1-1' }} %</td>
                </tr>
              </ng-template>
            </p-table>
          }
        </div>
      </p-tabPanel>

      <p-tabPanel header="Évolution ventes par produit" leftIcon="pi pi-chart-line">
        <div class="tab-content">
          <p class="tab-desc">Ventes par produit et par mois (période sélectionnée).</p>
          @if (loading()) {
            <div class="loading-placeholder"><i class="pi pi-spin pi-spinner"></i> Chargement...</div>
          } @else if (!productSalesTrend().length) {
            <div class="empty-placeholder"><i class="pi pi-info-circle"></i> Aucune donnée pour cette période.</div>
          } @else {
            <p-table [value]="productSalesTrend()" [rows]="20" [paginator]="productSalesTrend().length > 20" styleClass="p-datatable-sm reports-table">
              <ng-template pTemplate="header">
                <tr>
                  <th>Période</th>
                  <th>Produit</th>
                  <th>Catégorie</th>
                  <th class="text-right">Quantité</th>
                  <th class="text-right">CA</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td>{{ row.period }}</td>
                  <td><strong>{{ row.productName }}</strong><br><span class="code">{{ row.productCode }}</span></td>
                  <td>{{ row.categoryName ?? '–' }}</td>
                  <td class="text-right">{{ row.quantity | number:'1.3-3' }}</td>
                  <td class="text-right amount">{{ row.revenue | number:'1.3-3' }} {{ row.currency }}</td>
                </tr>
              </ng-template>
            </p-table>
          }
        </div>
      </p-tabPanel>

      <p-tabPanel header="Indicateurs panier" leftIcon="pi pi-shopping-cart">
        <div class="tab-content">
          <p class="tab-desc">Panier moyen et nombre de lignes par facture sur la période.</p>
          @if (loading()) {
            <div class="loading-placeholder"><i class="pi pi-spin pi-spinner"></i> Chargement...</div>
          } @else if (basketMetrics()) {
            <div class="kpi-grid">
              <app-stat-card
                label="Panier moyen"
                [value]="formatAmount(basketMetrics()!.averageBasket, basketMetrics()!.currency)"
                icon="pi-shopping-cart"
                variant="primary">
              </app-stat-card>
              <app-stat-card
                label="Lignes moy. / facture"
                [value]="(basketMetrics()!.averageLinesPerInvoice | number:'1.1-1') + ''"
                icon="pi-list"
                variant="success">
              </app-stat-card>
              <app-stat-card
                label="Factures"
                [value]="basketMetrics()!.totalInvoices + ''"
                icon="pi-file"
                variant="primary">
              </app-stat-card>
              <app-stat-card
                label="CA total"
                [value]="formatAmount(basketMetrics()!.totalRevenue, basketMetrics()!.currency)"
                icon="pi-dollar"
                variant="success">
              </app-stat-card>
            </div>
          } @else {
            <div class="empty-placeholder"><i class="pi pi-info-circle"></i> Aucune donnée pour cette période.</div>
          }
        </div>
      </p-tabPanel>

      <p-tabPanel header="Produits jamais vendus" leftIcon="pi pi-box">
        <div class="tab-content">
          <p class="tab-desc">Produits actifs sans vente sur la période sélectionnée (opportunités de mise en avant).</p>
          @if (loading()) {
            <div class="loading-placeholder"><i class="pi pi-spin pi-spinner"></i> Chargement...</div>
          } @else if (!productsNeverSold().length) {
            <div class="empty-placeholder"><i class="pi pi-check-circle"></i> Tous vos produits actifs ont été vendus sur cette période.</div>
          } @else {
            <p-table [value]="productsNeverSold()" styleClass="p-datatable-sm reports-table">
              <ng-template pTemplate="header">
                <tr>
                  <th>Produit</th>
                  <th>Catégorie</th>
                  <th class="text-right">Prix unitaire</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td><strong>{{ row.productName }}</strong><br><span class="code">{{ row.productCode }}</span></td>
                  <td>{{ row.categoryName ?? '–' }}</td>
                  <td class="text-right amount">{{ row.unitPrice | number:'1.3-3' }} {{ row.currency }}</td>
                </tr>
              </ng-template>
            </p-table>
          }
        </div>
      </p-tabPanel>

      <p-tabPanel header="CA par catégorie" leftIcon="pi pi-tag">
        <div class="tab-content">
          <p class="tab-desc">Répartition du chiffre d'affaires par catégorie.</p>
          <app-revenue-table
            [rows]="revenueByCategory()"
            [loading]="loading()"
            [currency]="currency()" />
        </div>
      </p-tabPanel>
    </p-tabView>

    <div class="back-link-wrap">
      <a routerLink="/reports" class="back-link">Retour aux rapports</a>
    </div>
  `,
  styles: [`
    .filter-section { margin-bottom: var(--spacing-6); }
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
    .tab-content { padding: var(--spacing-4) 0; }
    .tab-desc { font-size: var(--font-size-sm); color: var(--color-text-secondary); margin-bottom: var(--spacing-4); }
    .loading-placeholder, .empty-placeholder { padding: var(--spacing-8); text-align: center; color: var(--color-text-secondary); }
    .text-right { text-align: right; }
    .amount { font-family: 'JetBrains Mono', monospace; font-weight: var(--font-weight-semibold); }
    .code { font-size: var(--font-size-xs); color: var(--color-text-secondary); }
    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: var(--spacing-4); margin-bottom: var(--spacing-4); }
    .back-link-wrap { margin-top: var(--spacing-6); }
    .back-link { color: var(--color-primary-600); font-weight: var(--font-weight-medium); }
    :host ::ng-deep .reports-table .p-datatable-thead > tr > th { background: var(--color-neutral-50); font-size: var(--font-size-sm); text-transform: uppercase; padding: var(--spacing-4) var(--spacing-3); }
    :host ::ng-deep .reports-table .p-datatable-tbody > tr > td { padding: var(--spacing-4) var(--spacing-3); font-size: var(--font-size-sm); vertical-align: middle; }
  `]
})
export class ProductSalesAnalyticsComponent implements OnInit {
  private reportsApi = inject(ReportsApiService);

  loading = signal(false);
  productPerformance = signal<ProductPerformanceReportRow[]>([]);
  productSalesTrend = signal<ProductSalesTrendReportRow[]>([]);
  basketMetrics = signal<BasketMetricsReportDto | null>(null);
  productsNeverSold = signal<ProductNeverSoldReportRow[]>([]);
  revenueByCategory = signal<SalesRevenueReportRow[]>([]);
  currency = signal('TND');
  selectedPeriod: 'month' | 'quarter' | 'year' | 'all' = 'month';
  activeTabIndex = 0;

  periodOptions = [
    { label: 'Ce mois', value: 'month' },
    { label: 'Ce trimestre', value: 'quarter' },
    { label: 'Cette année', value: 'year' },
    { label: 'Tout', value: 'all' }
  ];

  private getPeriodDates(): { fromDate: string; toDate: string } {
    const to = new Date();
    const toDate = formatLocalDate(to);
    if (this.selectedPeriod === 'all') return { fromDate: '2020-01-01', toDate };
    const daysMap = { month: 30, quarter: 90, year: 365 } as const;
    const from = new Date();
    from.setDate(from.getDate() - daysMap[this.selectedPeriod]);
    return { fromDate: formatLocalDate(from), toDate };
  }

  ngOnInit(): void {
    this.loadAll();
  }

  onPeriodChange(): void {
    this.loadAll();
  }

  onTabChange(event: { index: number }): void {
    this.activeTabIndex = event.index;
  }

  private loadAll(): void {
    const { fromDate, toDate } = this.getPeriodDates();
    this.loading.set(true);
    let pending = 5;
    const maybeDone = () => { if (--pending === 0) this.loading.set(false); };

    this.reportsApi.getProductPerformance(fromDate, toDate).subscribe({
      next: (res) => this.productPerformance.set(res.success && res.data ? res.data : []),
      error: () => this.productPerformance.set([]),
      complete: maybeDone
    });

    this.reportsApi.getProductSalesTrend(fromDate, toDate).subscribe({
      next: (res) => this.productSalesTrend.set(res.success && res.data ? res.data : []),
      error: () => this.productSalesTrend.set([]),
      complete: maybeDone
    });

    this.reportsApi.getBasketMetrics(fromDate, toDate).subscribe({
      next: (res) => this.basketMetrics.set(res.success && res.data ? res.data : null),
      error: () => this.basketMetrics.set(null),
      complete: maybeDone
    });

    this.reportsApi.getProductsNeverSold(fromDate, toDate).subscribe({
      next: (res) => this.productsNeverSold.set(res.success && res.data ? res.data : []),
      error: () => this.productsNeverSold.set([]),
      complete: maybeDone
    });

    this.reportsApi.getSalesRevenueByProduct(fromDate, toDate, 'Category').subscribe({
      next: (res) => {
        this.revenueByCategory.set(res.success && res.data ? res.data : []);
        if (res.data?.length) this.currency.set(res.data[0].currency ?? 'TND');
      },
      error: () => this.revenueByCategory.set([]),
      complete: maybeDone
    });
  }

  formatAmount(value: number, currency = 'TND'): string {
    return new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 3, maximumFractionDigits: 3 }).format(value) + ' ' + currency;
  }

  onExport(): void {
    const { fromDate, toDate } = this.getPeriodDates();
    const dateStr = formatLocalDate(new Date());
    let csv = '';
    const escape = (v: string | number) => `"${String(v).replace(/"/g, '""')}"`;

    switch (this.activeTabIndex) {
      case 0: {
        const rows = this.productPerformance();
        const headers = ['Produit', 'Code', 'Catégorie', 'Quantité', 'CA', 'Coût', 'Marge', 'Marge %', 'Part CA %', 'Devise'];
        csv = [headers.map(escape).join(';'), ...rows.map(r =>
          [r.productName, r.productCode, r.categoryName ?? '', r.quantitySold, r.revenue.toFixed(3), r.totalCost.toFixed(3), r.profit.toFixed(3), r.marginPercent != null ? r.marginPercent.toFixed(1) : '', r.revenueSharePercent.toFixed(1), r.currency].map(escape).join(';')
        )].join('\n');
        break;
      }
      case 1: {
        const rows = this.productSalesTrend();
        const headers = ['Période', 'Produit', 'Code', 'Catégorie', 'Quantité', 'CA', 'Devise'];
        csv = [headers.map(escape).join(';'), ...rows.map(r =>
          [r.period, r.productName, r.productCode, r.categoryName ?? '', r.quantity, r.revenue.toFixed(3), r.currency].map(escape).join(';')
        )].join('\n');
        break;
      }
      case 2: {
        const m = this.basketMetrics();
        if (m) csv = 'Indicateur;Valeur\n"Panier moyen";' + m.averageBasket.toFixed(3) + '\n"Lignes moy. / facture";' + m.averageLinesPerInvoice.toFixed(1) + '\n"Factures";' + m.totalInvoices + '\n"CA total";' + m.totalRevenue.toFixed(3) + '\n"Devise";' + m.currency;
        break;
      }
      case 3: {
        const rows = this.productsNeverSold();
        const headers = ['Produit', 'Code', 'Catégorie', 'Prix unitaire', 'Devise'];
        csv = [headers.map(escape).join(';'), ...rows.map(r =>
          [r.productName, r.productCode, r.categoryName ?? '', r.unitPrice.toFixed(3), r.currency].map(escape).join(';')
        )].join('\n');
        break;
      }
      case 4: {
        const rows = this.revenueByCategory();
        const headers = ['Catégorie', 'Quantité', 'Chiffre d\'affaires', 'Devise'];
        csv = [headers.map(escape).join(';'), ...rows.map(r =>
          [r.groupKey, r.quantity, r.revenue.toFixed(3), r.currency].map(escape).join(';')
        )].join('\n');
        break;
      }
    }

    if (!csv) return;
    const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8;' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `rapports-ventes-produits-${dateStr}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  }
}
