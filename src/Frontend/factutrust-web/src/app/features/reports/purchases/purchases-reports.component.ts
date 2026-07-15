import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { TabViewModule } from 'primeng/tabview';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { formatLocalDate } from '@core/utils/date.util';
import {
  PurchasesReportsService,
  PurchasesReportsData,
  ReportPeriod,
  ExpenseCrossTabData
} from '../services/purchases-reports.service';
import {
  ReportsApiService,
  SupplierTransactionReportRow,
  PurchasesByLineReportRow,
  PurchasesVatReportRow,
  SupplierBalanceReportRow,
  SupplierWithholdingReportRow
} from '@core/services/reports-api.service';

interface PeriodOption {
  label: string;
  value: ReportPeriod;
}

@Component({
  selector: 'app-purchases-reports',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    TabViewModule,
    PageHeaderComponent,
    StatCardComponent,
    ButtonComponent
  ],
  template: `
    <app-page-header 
      title="Rapports Achats" 
      subtitle="Analysez vos dépenses et suivez vos indicateurs d'achats">
      <app-button 
        variant="secondary"
        icon="pi-download"
        iconPos="left"
        (click)="onExport()">
        Exporter
      </app-button>
      <app-button 
        variant="secondary"
        icon="pi-print"
        iconPos="left"
        (click)="onPrint()">
        Imprimer
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
          aria-label="Sélectionner la période des rapports">
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
          label="Dépenses totales"
          [value]="reportsData()!.totalExpenses"
          icon="pi-dollar"
          variant="primary">
        </app-stat-card>
        <app-stat-card
          label="Factures fournisseurs"
          [value]="reportsData()!.totalInvoices"
          icon="pi-file"
          variant="primary">
        </app-stat-card>
        <app-stat-card
          label="Factures payées"
          [value]="reportsData()!.paidInvoices"
          icon="pi-check-circle"
          variant="success">
        </app-stat-card>
        <app-stat-card
          label="Taux de paiement"
          [value]="reportsData()!.paymentRate"
          icon="pi-percentage"
          variant="primary">
        </app-stat-card>
        <app-stat-card
          label="Montant à payer"
          [value]="reportsData()!.amountToPay"
          icon="pi-wallet"
          variant="warning">
        </app-stat-card>
      </div>
    }

    <div class="reports-tabs-wrap">
      <p-tabView styleClass="ft-tabs" [scrollable]="true" [activeIndex]="activeTabIndex" (onChange)="onTabChange($event)">
        <p-tabPanel header="Dépenses et fournisseurs" leftIcon="pi pi-chart-line">
        <div class="tab-content">
    <div class="reports-grid">
      <div class="section">
        <div class="section-header">
          <h2 class="section-title">Évolution des dépenses</h2>
          <span class="section-subtitle">Dépenses des factures fournisseurs payées sur la période</span>
        </div>
        @if (loading()) {
          <div class="chart-placeholder">
            <div class="loading-placeholder">
              <i class="pi pi-spin pi-spinner"></i>
              <span>Chargement...</span>
            </div>
          </div>
        } @else if (reportsData()?.expenseChartData && reportsData()!.expenseChartData.length > 0) {
          <div class="chart-container" role="img" aria-label="Graphique - évolution des dépenses">
            <div class="chart-bars">
              @for (item of reportsData()!.expenseChartData; track item.label) {
                <div class="chart-bar-group">
                  <div class="chart-tooltip" aria-hidden="true">
                    {{ formatCurrency(item.amount) }}<br>
                    <small>{{ item.invoiceCount }} facture{{ item.invoiceCount > 1 ? 's' : '' }}</small>
                  </div>
                  <div class="chart-bar-wrapper">
                    <div
                      class="chart-bar chart-bar-blue"
                      [style.height.%]="getBarHeight(item.amount)"
                      [attr.aria-label]="item.label + ': ' + formatCurrency(item.amount)">
                    </div>
                  </div>
                  <span class="chart-label">{{ item.labelShort }}</span>
                </div>
              }
            </div>
          </div>
        } @else {
          <div class="chart-placeholder">
            <div class="chart-content">
              <i class="pi pi-chart-line chart-icon"></i>
              <p class="chart-text">Aucune donnée pour cette période</p>
              <p class="chart-subtext">Les dépenses apparaîtront ici une fois que vous aurez des factures fournisseurs payées. Créez des bons de commande et des factures fournisseurs.</p>
            </div>
          </div>
        }
      </div>

      <div class="section">
        <div class="section-header">
          <h2 class="section-title">Principaux fournisseurs</h2>
          <span class="section-subtitle">Top 10 par montant total acheté</span>
        </div>
        @if (loading()) {
          <div class="loading-placeholder">
            <i class="pi pi-spin pi-spinner"></i>
            <span>Chargement...</span>
          </div>
        } @else if (!reportsData()?.topSuppliers?.length) {
          <div class="empty-placeholder">
            <i class="pi pi-info-circle"></i>
            <p>Aucune donnée disponible</p>
            <p class="empty-hint">Aucune facture fournisseur sur cette période. Changez la période ou créez des factures fournisseurs.</p>
          </div>
        } @else {
          <p-table 
            [value]="reportsData()!.topSuppliers" 
            styleClass="p-datatable-sm reports-table"
            aria-label="Tableau des principaux fournisseurs">
            <ng-template pTemplate="header">
              <tr>
                <th>Fournisseur</th>
                <th class="text-right">Factures</th>
                <th class="text-right">Montant total</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-supplier>
              <tr>
                <td><span class="supplier-name">{{ supplier.supplierName }}</span></td>
                <td class="text-right"><span class="invoice-count">{{ supplier.invoiceCount }}</span></td>
                <td class="text-right amount">{{ supplier.totalAmount | number:'1.3-3' }} {{ supplier.currency }}</td>
              </tr>
            </ng-template>
          </p-table>
        }
      </div>
    </div>
        </div>
      </p-tabPanel>
      <p-tabPanel header="Transactions fournisseurs" leftIcon="pi pi-list">
        <div class="tab-content">
    <div class="section">
      <div class="section-header">
        <h2 class="section-title">Transactions fournisseurs</h2>
        <span class="section-subtitle">Factures fournisseurs et paiements sur la période</span>
      </div>
      @if (supplierTransactionsLoading()) {
        <div class="loading-placeholder"><i class="pi pi-spin pi-spinner"></i><span>Chargement...</span></div>
      } @else if (!supplierTransactions().length) {
        <div class="empty-placeholder"><i class="pi pi-info-circle"></i><p>Aucune transaction sur cette période</p></div>
      } @else {
        <p-table [value]="supplierTransactions()" styleClass="p-datatable-sm reports-table" aria-label="Transactions fournisseurs">
          <ng-template pTemplate="header">
            <tr><th>Date</th><th>Type</th><th>Fournisseur</th><th>Référence</th><th class="text-right">Montant</th></tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.date | date:'dd/MM/yyyy' }}</td>
              <td>{{ row.transactionType }}</td>
              <td>{{ row.supplierName }}</td>
              <td>{{ row.reference }}</td>
              <td class="text-right amount">{{ row.amount | number:'1.3-3' }} {{ row.currency }}</td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
        </div>
      </p-tabPanel>
      <p-tabPanel header="Détails achats par ligne" leftIcon="pi pi-box">
        <div class="tab-content">
    <div class="section">
      <div class="section-header">
        <h2 class="section-title">Détails achats par ligne produit</h2>
        <span class="section-subtitle">Lignes de factures fournisseurs agrégées par produit sur la période</span>
      </div>
      @if (purchasesByLineLoading()) {
        <div class="loading-placeholder"><i class="pi pi-spin pi-spinner"></i><span>Chargement...</span></div>
      } @else if (!purchasesByLine().length) {
        <div class="empty-placeholder"><i class="pi pi-info-circle"></i><p>Aucune ligne d'achat sur cette période</p></div>
      } @else {
        <p-table [value]="purchasesByLine()" styleClass="p-datatable-sm reports-table" aria-label="Détails achats par ligne">
          <ng-template pTemplate="header">
            <tr>
              <th>Produit</th>
              <th>Code</th>
              <th>Fournisseur</th>
              <th class="text-right">Quantité</th>
              <th class="text-right">Montant TTC</th>
              <th class="text-right">TVA</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.productName }}</td>
              <td>{{ row.productCode }}</td>
              <td>{{ row.supplierName }}</td>
              <td class="text-right">{{ row.quantity | number:'1.3-3' }}</td>
              <td class="text-right amount">{{ row.amountTTC | number:'1.3-3' }} {{ row.currency }}</td>
              <td class="text-right amount">{{ row.vatAmount | number:'1.3-3' }} {{ row.currency }}</td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
        </div>
      </p-tabPanel>
      <p-tabPanel header="TVA achats" leftIcon="pi pi-calculator">
        <div class="tab-content">
    <div class="section">
      <div class="section-header">
        <h2 class="section-title">TVA achats</h2>
        <span class="section-subtitle">Agrégat TVA par taux sur la période</span>
      </div>
      @if (purchasesVatLoading()) {
        <div class="loading-placeholder"><i class="pi pi-spin pi-spinner"></i><span>Chargement...</span></div>
      } @else if (!purchasesVat().length) {
        <div class="empty-placeholder"><i class="pi pi-info-circle"></i><p>Aucune TVA achats sur cette période</p></div>
      } @else {
        <p-table [value]="purchasesVat()" styleClass="p-datatable-sm reports-table" aria-label="TVA achats">
          <ng-template pTemplate="header">
            <tr>
              <th>Taux TVA</th>
              <th class="text-right">Base taxable</th>
              <th class="text-right">Montant TVA</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.vatRateDisplay }}</td>
              <td class="text-right amount">{{ row.totalTaxableAmount | number:'1.3-3' }} {{ row.currency }}</td>
              <td class="text-right amount">{{ row.totalVatAmount | number:'1.3-3' }} {{ row.currency }}</td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
        </div>
      </p-tabPanel>
      <p-tabPanel header="Soldes fournisseur" leftIcon="pi pi-wallet">
        <div class="tab-content">
    <div class="section">
      <div class="section-header">
        <h2 class="section-title">Soldes fournisseur</h2>
        <span class="section-subtitle">Total facturé, total payé et solde par fournisseur</span>
      </div>
      @if (supplierBalancesLoading()) {
        <div class="loading-placeholder"><i class="pi pi-spin pi-spinner"></i><span>Chargement...</span></div>
      } @else if (!supplierBalances().length) {
        <div class="empty-placeholder"><i class="pi pi-info-circle"></i><p>Aucun solde fournisseur</p></div>
      } @else {
        <p-table [value]="supplierBalances()" styleClass="p-datatable-sm reports-table" aria-label="Soldes fournisseurs">
          <ng-template pTemplate="header">
            <tr>
              <th>Fournisseur</th>
              <th class="text-right">Total facturé</th>
              <th class="text-right">Total payé</th>
              <th class="text-right">Solde</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.supplierName }}</td>
              <td class="text-right amount">{{ row.totalInvoiced | number:'1.3-3' }} {{ row.currency }}</td>
              <td class="text-right amount">{{ row.totalPaid | number:'1.3-3' }} {{ row.currency }}</td>
              <td class="text-right amount" [class.balance-negative]="row.balance < 0">{{ row.balance | number:'1.3-3' }} {{ row.currency }}</td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
        </div>
      </p-tabPanel>
      <p-tabPanel header="Retenues fournisseurs" leftIcon="pi pi-percentage">
        <div class="tab-content">
    <div class="section">
      <div class="section-header">
        <h2 class="section-title">Total des retenues pour les fournisseurs</h2>
        <span class="section-subtitle">Agrégé par fournisseur selon la date de solde (PaidAt), aligné déclaration TEJ</span>
      </div>
      <p class="section-hint">
        <a routerLink="/withholding-tax/dashboard" class="section-link">Tableau de bord retenue à la source</a>
      </p>
      @if (supplierWithholdingsLoading()) {
        <div class="loading-placeholder"><i class="pi pi-spin pi-spinner"></i><span>Chargement...</span></div>
      } @else if (!supplierWithholdings().length) {
        <div class="empty-placeholder"><i class="pi pi-info-circle"></i><p>Aucune retenue fournisseur sur cette période</p></div>
      } @else {
        <p-table [value]="supplierWithholdings()" styleClass="p-datatable-sm reports-table" aria-label="Retenues fournisseurs">
          <ng-template pTemplate="header">
            <tr>
              <th>Fournisseur</th>
              <th class="text-right">N° factures</th>
              <th class="text-right">Montant HT</th>
              <th class="text-right">Retenue</th>
              <th class="text-right">Net servi</th>
              <th>Devise</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.supplierName }}</td>
              <td class="text-right">{{ row.invoiceCount }}</td>
              <td class="text-right amount">{{ row.totalHT | number:'1.3-3' }}</td>
              <td class="text-right amount">{{ row.totalWithholding | number:'1.3-3' }}</td>
              <td class="text-right amount">{{ row.totalNetPaid | number:'1.3-3' }}</td>
              <td>{{ row.currency }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="footer">
            <tr>
              <td><strong>Total</strong></td>
              <td class="text-right"><strong>{{ supplierWithholdingsInvoiceTotal() }}</strong></td>
              <td class="text-right amount"><strong>{{ supplierWithholdingsHtTotal() | number:'1.3-3' }}</strong></td>
              <td class="text-right amount"><strong>{{ supplierWithholdingsTotal() | number:'1.3-3' }}</strong></td>
              <td class="text-right amount"><strong>{{ supplierWithholdingsNetTotal() | number:'1.3-3' }}</strong></td>
              <td></td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
        </div>
      </p-tabPanel>
      <p-tabPanel header="Synthèse mensuelle" leftIcon="pi pi-calendar">
        <div class="tab-content">
    @if (!loading() && reportsData()?.monthlySummary && reportsData()!.monthlySummary.length > 0) {
      <div class="section">
        <div class="section-header">
          <h2 class="section-title">Synthèse mensuelle</h2>
          <span class="section-subtitle">6 derniers mois</span>
        </div>
        <p-table 
          [value]="reportsData()!.monthlySummary" 
          styleClass="p-datatable-sm reports-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Mois</th>
              <th class="text-right">Dépenses</th>
              <th class="text-right">Factures</th>
              <th class="text-right">Payées</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.monthShort }} {{ row.year }}</td>
              <td class="text-right amount">{{ row.amount | number:'1.3-3' }} {{ reportsData()!.currency }}</td>
              <td class="text-right">{{ row.invoiceCount }}</td>
              <td class="text-right">{{ row.paidCount }}</td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    } @else if (!loading()) {
      <div class="section">
        <div class="empty-placeholder">
          <i class="pi pi-info-circle"></i>
          <p>Aucune donnée pour la synthèse mensuelle</p>
        </div>
      </div>
    }
        </div>
      </p-tabPanel>
      <p-tabPanel header="Répartition par statut" leftIcon="pi pi-chart-bar">
        <div class="tab-content">
    <div class="section">
      <div class="section-header">
        <h2 class="section-title">Répartition par statut</h2>
        <span class="section-subtitle">Répartition des factures fournisseurs selon leur statut</span>
      </div>
      @if (loading()) {
        <div class="loading-placeholder">
          <i class="pi pi-spin pi-spinner"></i>
          <span>Chargement...</span>
        </div>
      } @else if (!reportsData()?.statusBreakdown?.length) {
        <div class="empty-placeholder">
          <i class="pi pi-info-circle"></i>
          <p>Aucune donnée disponible</p>
        </div>
      } @else {
        <div class="status-grid">
          @for (status of reportsData()!.statusBreakdown; track status.status) {
            <div class="status-card">
              <div class="status-header">
                <span class="status-label">{{ status.status }}</span>
                <span class="status-count">{{ status.count }}</span>
              </div>
              <div class="status-bar">
                <div 
                  class="status-fill" 
                  [style.width.%]="status.percentage"
                  [style.background]="reportsService.getStatusColor(status.status)">
                </div>
              </div>
              <div class="status-amount">{{ status.totalAmount | number:'1.3-3' }} {{ status.currency }}</div>
            </div>
          }
        </div>
      }
    </div>
        </div>
      </p-tabPanel>
      <p-tabPanel header="Dépenses par mois / Année" leftIcon="pi pi-table">
        <div class="tab-content">
    <div class="section crosstab-section">
      <div class="section-header">
        <h2 class="section-title">Dépenses par mois / Année </h2>
        <span class="section-subtitle">Dépenses par mois et année - 5 dernières années (factures payées)</span>
      </div>
      @if (crossTabLoading()) {
        <div class="loading-placeholder">
          <i class="pi pi-spin pi-spinner"></i>
          <span>Chargement...</span>
        </div>
      } @else if (!crossTabData()) {
        <div class="empty-placeholder">
          <i class="pi pi-info-circle"></i>
          <p>Aucune donnée disponible</p>
          <p class="empty-hint">Aucune facture fournisseur payée sur la période.</p>
        </div>
      } @else {
        <div class="crosstab-table-wrapper">
          <table class="crosstab-table" aria-label="Tableau croisé des dépenses par mois et année">
            <thead>
              <tr>
                <th scope="col">Année</th>
                @for (month of crossTabData()!.monthLabels; track month) {
                  <th scope="col" class="text-right">{{ month }}</th>
                }
                <th scope="col" class="text-right total-col">Total</th>
              </tr>
            </thead>
            <tbody>
              @for (year of crossTabData()!.years; track year) {
                <tr>
                  <th scope="row">{{ year }}</th>
                  @for (amt of crossTabData()!.matrix.get(year) ?? []; track $index) {
                    <td class="text-right amount">{{ amt | number:'1.3-3' }}</td>
                  }
                  <td class="text-right amount total-cell">{{ (crossTabData()!.rowTotals.get(year) ?? 0) | number:'1.3-3' }} {{ crossTabData()!.currency }}</td>
                </tr>
              }
            </tbody>
            <tfoot>
              <tr class="totals-row">
                <th scope="row">Total</th>
                @for (col of crossTabData()!.columnTotals; track $index) {
                  <td class="text-right amount">{{ col | number:'1.3-3' }}</td>
                }
                <td class="text-right amount total-cell">{{ crossTabData()!.grandTotal | number:'1.3-3' }} {{ crossTabData()!.currency }}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      }
    </div>
        </div>
      </p-tabPanel>
      </p-tabView>
    </div>

    <!-- Print-only section -->
    <div class="print-only print-report">
      <!-- Professional Header -->
      <div class="print-header">
        <div class="print-header__brand-bar"></div>
        <div class="print-header__content">
          <div class="print-header__left">
            <h1 class="print-header__title">RAPPORT ACHATS</h1>
            <p class="print-header__subtitle">{{ activeTabName }}</p>
            <p class="print-header__period">Période : {{ periodOptions[selectedPeriodIndex].label || 'Tout' }}
              @if (periodDateRangeLabel()) {
                <span> — {{ periodDateRangeLabel() }}</span>
              }
            </p>
          </div>
          <div class="print-header__right">
            <p class="print-header__app">InstaFact</p>
            <p class="print-header__date">Généré le {{ printDate }}</p>
          </div>
        </div>
      </div>

      <!-- KPI Summary -->
      @if (reportsData()) {
        <div class="print-kpi-bar">
          <div class="print-kpi-item">
            <span class="print-kpi-item__label">Dépenses totales</span>
            <span class="print-kpi-item__value">{{ reportsData()!.totalExpenses }}</span>
          </div>
          <div class="print-kpi-item">
            <span class="print-kpi-item__label">Factures</span>
            <span class="print-kpi-item__value">{{ reportsData()!.totalInvoices }}</span>
          </div>
          <div class="print-kpi-item">
            <span class="print-kpi-item__label">Taux paiement</span>
            <span class="print-kpi-item__value">{{ reportsData()!.paymentRate }}</span>
          </div>
          <div class="print-kpi-item">
            <span class="print-kpi-item__label">À payer</span>
            <span class="print-kpi-item__value">{{ reportsData()!.amountToPay }}</span>
          </div>
        </div>
      }

      <!-- Tab 0: Expenses & top suppliers -->
      @if (activeTabIndex === 0 && reportsData()) {
        @if (reportsData()!.expenseChartData.length) {
          <h4 class="print-section-title">Évolution des dépenses</h4>
          <table class="print-table">
            <thead><tr><th>Période</th><th class="text-right">Dépenses</th><th class="text-right">Factures</th></tr></thead>
            <tbody>
              @for (item of reportsData()!.expenseChartData; track item.label) {
                <tr>
                  <td>{{ item.label }}</td>
                  <td class="text-right print-amount">{{ item.amount | number:'1.3-3' }} {{ reportsData()!.currency }}</td>
                  <td class="text-right">{{ item.invoiceCount }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
        @if (reportsData()!.topSuppliers.length) {
          <h4 class="print-section-title">Principaux fournisseurs (Top 10)</h4>
          <table class="print-table">
            <thead><tr><th>Fournisseur</th><th class="text-right">Factures</th><th class="text-right">Montant total</th></tr></thead>
            <tbody>
              @for (supplier of reportsData()!.topSuppliers; track supplier.supplierName) {
                <tr>
                  <td>{{ supplier.supplierName }}</td>
                  <td class="text-right">{{ supplier.invoiceCount }}</td>
                  <td class="text-right print-amount">{{ supplier.totalAmount | number:'1.3-3' }} {{ supplier.currency }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      }

      <!-- Tab 1: Transactions fournisseurs -->
      @if (activeTabIndex === 1 && supplierTransactions().length) {
        <table class="print-table">
          <thead><tr><th>Date</th><th>Type</th><th>Fournisseur</th><th>Référence</th><th class="text-right">Montant</th></tr></thead>
          <tbody>
            @for (row of supplierTransactions(); track row.reference) {
              <tr>
                <td>{{ row.date | date:'dd/MM/yyyy' }}</td>
                <td>{{ row.transactionType }}</td>
                <td>{{ row.supplierName }}</td>
                <td>{{ row.reference }}</td>
                <td class="text-right print-amount">{{ row.amount | number:'1.3-3' }} {{ row.currency }}</td>
              </tr>
            }
          </tbody>
        </table>
      }

      <!-- Tab 2: Détails achats par ligne -->
      @if (activeTabIndex === 2 && purchasesByLine().length) {
        <table class="print-table">
          <thead><tr><th>Produit</th><th>Code</th><th>Fournisseur</th><th class="text-right">Quantité</th><th class="text-right">Montant TTC</th><th class="text-right">TVA</th></tr></thead>
          <tbody>
            @for (row of purchasesByLine(); track row.productCode) {
              <tr>
                <td>{{ row.productName }}</td>
                <td>{{ row.productCode }}</td>
                <td>{{ row.supplierName }}</td>
                <td class="text-right">{{ row.quantity | number:'1.3-3' }}</td>
                <td class="text-right print-amount">{{ row.amountTTC | number:'1.3-3' }} {{ row.currency }}</td>
                <td class="text-right print-amount">{{ row.vatAmount | number:'1.3-3' }} {{ row.currency }}</td>
              </tr>
            }
          </tbody>
        </table>
      }

      <!-- Tab 3: TVA achats -->
      @if (activeTabIndex === 3 && purchasesVat().length) {
        <table class="print-table">
          <thead><tr><th>Taux TVA</th><th class="text-right">Base taxable</th><th class="text-right">Montant TVA</th></tr></thead>
          <tbody>
            @for (row of purchasesVat(); track row.vatRateDisplay) {
              <tr>
                <td>{{ row.vatRateDisplay }}</td>
                <td class="text-right print-amount">{{ row.totalTaxableAmount | number:'1.3-3' }} {{ row.currency }}</td>
                <td class="text-right print-amount">{{ row.totalVatAmount | number:'1.3-3' }} {{ row.currency }}</td>
              </tr>
            }
          </tbody>
        </table>
      }

      <!-- Tab 4: Soldes fournisseur -->
      @if (activeTabIndex === 4 && supplierBalances().length) {
        <table class="print-table">
          <thead><tr><th>Fournisseur</th><th class="text-right">Total facturé</th><th class="text-right">Total payé</th><th class="text-right">Solde</th></tr></thead>
          <tbody>
            @for (row of supplierBalances(); track row.supplierName) {
              <tr>
                <td>{{ row.supplierName }}</td>
                <td class="text-right print-amount">{{ row.totalInvoiced | number:'1.3-3' }} {{ row.currency }}</td>
                <td class="text-right print-amount">{{ row.totalPaid | number:'1.3-3' }} {{ row.currency }}</td>
                <td class="text-right print-amount" [style.color]="row.balance < 0 ? '#e53e3e' : '#2d3748'">{{ row.balance | number:'1.3-3' }} {{ row.currency }}</td>
              </tr>
            }
          </tbody>
        </table>
      }

      <!-- Tab 5: Synthèse mensuelle -->
      @if (activeTabIndex === 5 && supplierWithholdings().length) {
        <table class="print-table">
          <thead><tr><th>Fournisseur</th><th class="text-right">Factures</th><th class="text-right">HT</th><th class="text-right">Retenue</th><th class="text-right">Net</th></tr></thead>
          <tbody>
            @for (row of supplierWithholdings(); track row.supplierId) {
              <tr>
                <td>{{ row.supplierName }}</td>
                <td class="text-right">{{ row.invoiceCount }}</td>
                <td class="text-right print-amount">{{ row.totalHT | number:'1.3-3' }}</td>
                <td class="text-right print-amount">{{ row.totalWithholding | number:'1.3-3' }}</td>
                <td class="text-right print-amount">{{ row.totalNetPaid | number:'1.3-3' }} {{ row.currency }}</td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (activeTabIndex === 6 && reportsData()?.monthlySummary?.length) {
        <table class="print-table">
          <thead><tr><th>Mois</th><th class="text-right">Dépenses</th><th class="text-right">Factures</th><th class="text-right">Payées</th></tr></thead>
          <tbody>
            @for (row of reportsData()!.monthlySummary; track row.monthShort + row.year) {
              <tr>
                <td>{{ row.monthShort }} {{ row.year }}</td>
                <td class="text-right print-amount">{{ row.amount | number:'1.3-3' }} {{ reportsData()!.currency }}</td>
                <td class="text-right">{{ row.invoiceCount }}</td>
                <td class="text-right">{{ row.paidCount }}</td>
              </tr>
            }
          </tbody>
        </table>
      }

      <!-- Tab 6: Répartition par statut -->
      @if (activeTabIndex === 7 && reportsData()?.statusBreakdown?.length) {
        <table class="print-table">
          <thead><tr><th>Statut</th><th class="text-right">Nombre</th><th class="text-right">Pourcentage</th><th class="text-right">Montant</th></tr></thead>
          <tbody>
            @for (status of reportsData()!.statusBreakdown; track status.status) {
              <tr>
                <td>{{ status.status }}</td>
                <td class="text-right">{{ status.count }}</td>
                <td class="text-right">{{ status.percentage | number:'1.1-1' }} %</td>
                <td class="text-right print-amount">{{ status.totalAmount | number:'1.3-3' }} {{ status.currency }}</td>
              </tr>
            }
          </tbody>
        </table>
      }

      <!-- Tab 7: Cross-tab dépenses -->
      @if (activeTabIndex === 8 && crossTabData()) {
        <table class="print-table">
          <thead>
            <tr>
              <th>Année</th>
              @for (month of crossTabData()!.monthLabels; track month) {
                <th class="text-right">{{ month }}</th>
              }
              <th class="text-right">Total</th>
            </tr>
          </thead>
          <tbody>
            @for (year of crossTabData()!.years; track year) {
              <tr>
                <td><strong>{{ year }}</strong></td>
                @for (amt of crossTabData()!.matrix.get(year) ?? []; track $index) {
                  <td class="text-right print-amount">{{ amt | number:'1.3-3' }}</td>
                }
                <td class="text-right print-amount"><strong>{{ (crossTabData()!.rowTotals.get(year) ?? 0) | number:'1.3-3' }} {{ crossTabData()!.currency }}</strong></td>
              </tr>
            }
          </tbody>
          <tfoot>
            <tr>
              <td><strong>Total</strong></td>
              @for (col of crossTabData()!.columnTotals; track $index) {
                <td class="text-right print-amount"><strong>{{ col | number:'1.3-3' }}</strong></td>
              }
              <td class="text-right print-amount"><strong>{{ crossTabData()!.grandTotal | number:'1.3-3' }} {{ crossTabData()!.currency }}</strong></td>
            </tr>
          </tfoot>
        </table>
      }

      <!-- Professional Footer -->
      <div class="print-footer">
        <div class="print-footer__content">
          <span class="print-footer__brand">Généré par InstaFact</span>
          <span class="print-footer__page">{{ printDate }}</span>
        </div>
        <div class="print-footer__bar"></div>
      </div>
    </div>
  `,
  styles: [`
    /* Layout onglets (le style visuel est fourni par la classe globale .ft-tabs) */
    .reports-tabs-wrap { margin-top: var(--spacing-6); }
    .tab-content { padding-top: var(--spacing-4); }

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

    .reports-grid {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: var(--spacing-6);
      margin-bottom: var(--spacing-6);
    }

    .section {
      background: var(--color-background-elevated);
      border-radius: var(--radius-xl);
      padding: var(--spacing-6);
      box-shadow: var(--shadow-sm);
      border: 1px solid var(--color-border-subtle);
    }

    .section-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: var(--spacing-2);
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
      background: linear-gradient(180deg, #3b82f6, #2563eb);
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
    .chart-icon { font-size: 3rem; color: #3b82f6; margin-bottom: var(--spacing-3); }
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
      position: relative;
    }

    .chart-bar {
      width: 70%;
      min-height: 4px;
      border-radius: var(--radius-md) var(--radius-md) 0 0;
      transition: height 0.8s cubic-bezier(0.34, 1.56, 0.64, 1);
      cursor: pointer;
    }

    .chart-bar-blue {
      background: linear-gradient(180deg, #60a5fa, #3b82f6);
    }

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
      text-align: center;
    }

    .chart-bar-group:hover .chart-tooltip { opacity: 1; }
    .chart-label { margin-top: var(--spacing-2); font-size: var(--font-size-xs); font-weight: var(--font-weight-medium); color: var(--color-text-secondary); text-transform: uppercase; }

    .loading-placeholder, .empty-placeholder {
      padding: var(--spacing-8);
      text-align: center;
      color: var(--color-text-secondary);
    }

    .empty-hint { font-size: var(--font-size-sm); margin-top: var(--spacing-2); opacity: 0.8; }

    .supplier-name { font-weight: var(--font-weight-medium); color: var(--color-text-primary); }
    .invoice-count { font-weight: var(--font-weight-semibold); color: var(--color-text-primary); }
    .text-right { text-align: right; }
    .amount { font-family: 'JetBrains Mono', 'SF Mono', 'Monaco', 'Consolas', monospace; font-weight: var(--font-weight-semibold); color: var(--color-text-primary); }
    .balance-negative { color: var(--color-error-600, #dc2626); }

    :host ::ng-deep .reports-table {
      .p-datatable-thead > tr > th {
        background: var(--color-neutral-50);
        color: var(--color-text-secondary);
        font-weight: var(--font-weight-semibold);
        font-size: var(--font-size-sm);
        text-transform: uppercase;
        padding: var(--spacing-4) var(--spacing-3);
        border-bottom: 2px solid var(--color-border-default);
      }
      .p-datatable-tbody > tr > td { padding: var(--spacing-4) var(--spacing-3); border-bottom: 1px solid var(--color-border-subtle); font-size: var(--font-size-sm); vertical-align: middle; }
      .p-datatable-tbody > tr:hover { background: var(--color-primary-50); }
    }

    .status-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: var(--spacing-4); }
    .status-card {
      padding: var(--spacing-4);
      background: var(--color-neutral-50);
      border-radius: var(--radius-lg);
      border: 1px solid var(--color-border-subtle);
    }
    .status-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--spacing-3); }
    .status-label { font-size: var(--font-size-sm); font-weight: var(--font-weight-medium); color: var(--color-text-secondary); text-transform: uppercase; }
    .status-count { font-size: var(--font-size-lg); font-weight: var(--font-weight-bold); color: var(--color-text-primary); }
    .status-bar { height: 8px; background: var(--color-neutral-200); border-radius: var(--radius-full); overflow: hidden; margin-bottom: var(--spacing-3); }
    .status-fill { height: 100%; transition: width var(--transition-normal); border-radius: var(--radius-full); }
    .status-amount { font-family: 'JetBrains Mono', 'SF Mono', 'Monaco', 'Consolas', monospace; font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); color: var(--color-text-primary); }

    .crosstab-section { margin-top: var(--spacing-6); }
    .crosstab-table-wrapper { overflow-x: auto; border-radius: var(--radius-lg); border: 1px solid var(--color-border-subtle); }
    .crosstab-table { width: 100%; border-collapse: collapse; font-size: var(--font-size-base); }
    .crosstab-table th, .crosstab-table td { padding: var(--spacing-3) var(--spacing-4); border-bottom: 1px solid var(--color-border-subtle); }
    .crosstab-table thead th { background: var(--color-neutral-50); color: var(--color-text-secondary); font-weight: var(--font-weight-semibold); font-size: var(--font-size-sm); text-transform: uppercase; }
    .crosstab-table tbody tr:hover { background: var(--color-primary-50); }
    .crosstab-table tfoot .totals-row { background: var(--color-primary-50); border-top: 2px solid #3b82f6; }
    .crosstab-table tfoot th, .crosstab-table tfoot td { font-weight: var(--font-weight-bold); color: #1d4ed8; }
    .crosstab-table .total-col, .crosstab-table .total-cell { font-weight: var(--font-weight-semibold); }

    @media (max-width: 1024px) { .reports-grid { grid-template-columns: 1fr; } }
    @media (max-width: 768px) {
      .stats-grid { grid-template-columns: repeat(2, 1fr); }
      .section { padding: var(--spacing-4); }
      .section-title { font-size: var(--font-size-xl); }
      .section-title::before { display: none; }
    }

    /* ── Print ──────────────────────────────────────────────── */
    .print-only { display: none; }

    @media print {
      @page {
        size: A4;
        margin: 12mm 15mm 18mm 15mm;
      }

      :host {
        font-family: 'Segoe UI', 'Helvetica Neue', Arial, sans-serif;
        font-size: 10pt;
        color: #2d3748;
        line-height: 1.5;
      }

      .filter-section,
      .summary-block,
      .stats-grid,
      .reports-tabs-wrap,
      app-page-header {
        display: none !important;
      }

      .print-only {
        display: block !important;
      }

      .print-report {
        padding: 0;
        max-width: 100%;
      }

      /* ── Header ── */
      .print-header {
        margin-bottom: 20px;
      }

      .print-header__brand-bar {
        height: 5px;
        background: linear-gradient(90deg, #3b82f6 0%, #2563eb 60%, #e2e8f0 100%);
        border-radius: 3px;
        margin-bottom: 16px;
      }

      .print-header__content {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        padding-bottom: 14px;
        border-bottom: 2px solid #e2e8f0;
      }

      .print-header__title {
        font-size: 20pt;
        font-weight: 800;
        color: #1a202c;
        margin: 0 0 4px;
        letter-spacing: 1px;
      }

      .print-header__subtitle {
        font-size: 12pt;
        color: #3b82f6;
        font-weight: 600;
        margin: 0 0 4px;
      }

      .print-header__period {
        font-size: 9pt;
        color: #718096;
        margin: 0;
      }

      .print-header__right {
        text-align: right;
      }

      .print-header__app {
        font-size: 13pt;
        font-weight: 700;
        color: #3b82f6;
        margin: 0 0 4px;
      }

      .print-header__date {
        font-size: 8pt;
        color: #a0aec0;
        margin: 0;
      }

      /* ── KPI Bar ── */
      .print-kpi-bar {
        display: flex;
        gap: 10px;
        margin-bottom: 20px;
        flex-wrap: wrap;
      }

      .print-kpi-item {
        flex: 1;
        min-width: 100px;
        padding: 10px 12px;
        border: 1.5px solid #e2e8f0;
        border-radius: 8px;
        text-align: center;
        background: #f7fafc;
      }

      .print-kpi-item__label {
        display: block;
        font-size: 7pt;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: 0.5px;
        color: #718096;
        margin-bottom: 4px;
      }

      .print-kpi-item__value {
        display: block;
        font-size: 12pt;
        font-weight: 700;
        color: #1a202c;
      }

      /* ── Section titles ── */
      .print-section-title {
        font-size: 12pt;
        font-weight: 700;
        margin: 18px 0 10px;
        color: #2d3748;
        padding-bottom: 6px;
        border-bottom: 2px solid #3b82f6;
        display: inline-block;
      }

      /* ── Tables ── */
      .print-table {
        width: 100%;
        border-collapse: collapse;
        font-size: 11pt;
        margin-bottom: 18px;
      }

      .print-table thead {
        display: table-header-group;
      }

      .print-table thead th {
        text-align: left;
        padding: 9px 12px;
        font-weight: 700;
        color: #1a202c;
        font-size: 10pt;
        text-transform: uppercase;
        letter-spacing: 0.3px;
        white-space: nowrap;
        border-bottom: 2.5px solid #3b82f6;
        background: #f7fafc;
      }

      .print-table thead th.text-right {
        text-align: right;
      }

      .print-table tbody td {
        padding: 8px 12px;
        border-bottom: 1px solid #edf2f7;
        color: #4a5568;
        vertical-align: middle;
      }

      .print-table tbody td.text-right {
        text-align: right;
      }

      .print-table tbody tr {
        page-break-inside: avoid;
      }

      .print-table tbody tr:nth-child(even) {
        background: #f7fafc;
      }

      .print-table tfoot td {
        padding: 9px 12px;
        border-top: 2.5px solid #3b82f6;
        font-weight: 700;
        font-size: 11pt;
        color: #1a202c;
        background: #eff6ff;
      }

      .print-amount {
        font-family: 'JetBrains Mono', 'SF Mono', 'Consolas', monospace;
        font-weight: 600;
        letter-spacing: -0.3px;
      }

      /* ── Footer ── */
      .print-footer {
        margin-top: 24px;
        page-break-inside: avoid;
      }

      .print-footer__content {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 8px 0;
        border-top: 1px solid #e2e8f0;
      }

      .print-footer__brand {
        font-size: 8pt;
        color: #a0aec0;
        font-weight: 500;
      }

      .print-footer__page {
        font-size: 7pt;
        color: #cbd5e0;
      }

      .print-footer__bar {
        height: 4px;
        margin-top: 8px;
        background: linear-gradient(90deg, #3b82f6 0%, #2563eb 40%, transparent 100%);
        border-radius: 2px;
      }
    }`]
})
export class PurchasesReportsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  reportsService = inject(PurchasesReportsService);
  reportsApi = inject(ReportsApiService);

  loading = signal(true);
  reportsData = signal<PurchasesReportsData | null>(null);
  crossTabData = signal<ExpenseCrossTabData | null>(null);
  crossTabLoading = signal(true);
  supplierTransactions = signal<SupplierTransactionReportRow[]>([]);
  supplierTransactionsLoading = signal(false);
  purchasesByLine = signal<PurchasesByLineReportRow[]>([]);
  purchasesByLineLoading = signal(false);
  purchasesVat = signal<PurchasesVatReportRow[]>([]);
  purchasesVatLoading = signal(false);
  supplierBalances = signal<SupplierBalanceReportRow[]>([]);
  supplierBalancesLoading = signal(false);
  supplierWithholdings = signal<SupplierWithholdingReportRow[]>([]);
  supplierWithholdingsLoading = signal(false);
  selectedPeriod: ReportPeriod = 'month';
  activeTabIndex = 0;
  private maxAmount = 0;

  periodOptions: PeriodOption[] = [
    { label: 'Cette semaine', value: 'week' },
    { label: 'Ce mois', value: 'month' },
    { label: 'Ce trimestre', value: 'quarter' },
    { label: 'Cette année', value: 'year' },
    { label: 'Tout', value: 'all' }
  ];

  ngOnInit(): void {
    this.applyTabFromQuery(this.route.snapshot.queryParamMap.get('tab'));
    this.route.queryParamMap.subscribe(params => this.applyTabFromQuery(params.get('tab')));
    this.loadReportsData();
    this.loadCrossTabData();
    this.loadSupplierTransactions();
    this.loadPurchasesByLine();
    this.loadPurchasesVat();
    this.loadSupplierBalances();
    this.loadSupplierWithholdings();
  }

  supplierWithholdingsTotal(): number {
    return this.supplierWithholdings().reduce((sum, r) => sum + r.totalWithholding, 0);
  }

  supplierWithholdingsHtTotal(): number {
    return this.supplierWithholdings().reduce((sum, r) => sum + r.totalHT, 0);
  }

  supplierWithholdingsNetTotal(): number {
    return this.supplierWithholdings().reduce((sum, r) => sum + r.totalNetPaid, 0);
  }

  supplierWithholdingsInvoiceTotal(): number {
    return this.supplierWithholdings().reduce((sum, r) => sum + r.invoiceCount, 0);
  }

  private applyTabFromQuery(tab: string | null): void {
    const tabIndexMap: Record<string, number> = {
      'soldes-fournisseur': 4,
      'retenues-fournisseurs': 5
    };
    if (tab && tabIndexMap[tab] !== undefined) {
      this.activeTabIndex = tabIndexMap[tab];
    }
  }

  onPeriodChange(): void {
    this.loadReportsData();
    this.loadSupplierTransactions();
    this.loadPurchasesByLine();
    this.loadPurchasesVat();
    this.loadSupplierWithholdings();
  }

  onTabChange(event: { index: number }): void {
    this.activeTabIndex = event.index;
  }

  private getPeriodDates(): { fromDate: string; toDate: string } {
    const to = new Date();
    const toDate = formatLocalDate(to);
    if (this.selectedPeriod === 'all') return { fromDate: '2020-01-01', toDate };
    const daysMap: Record<Exclude<ReportPeriod, 'all'>, number> = { week: 7, month: 30, quarter: 90, year: 365 };
    const from = new Date();
    from.setDate(from.getDate() - daysMap[this.selectedPeriod]);
    return { fromDate: formatLocalDate(from), toDate };
  }

  private loadSupplierTransactions(): void {
    this.supplierTransactionsLoading.set(true);
    const { fromDate, toDate } = this.getPeriodDates();
    this.reportsApi.getSupplierTransactions(fromDate, toDate).subscribe({
      next: (res) => {
        this.supplierTransactions.set(res.success && res.data ? res.data : []);
        this.supplierTransactionsLoading.set(false);
      },
      error: () => { this.supplierTransactions.set([]); this.supplierTransactionsLoading.set(false); }
    });
  }

  private loadPurchasesByLine(): void {
    this.purchasesByLineLoading.set(true);
    const { fromDate, toDate } = this.getPeriodDates();
    this.reportsApi.getPurchasesByLine(fromDate, toDate).subscribe({
      next: (res) => {
        this.purchasesByLine.set(res.success && res.data ? res.data : []);
        this.purchasesByLineLoading.set(false);
      },
      error: () => { this.purchasesByLine.set([]); this.purchasesByLineLoading.set(false); }
    });
  }

  private loadPurchasesVat(): void {
    this.purchasesVatLoading.set(true);
    const { fromDate, toDate } = this.getPeriodDates();
    this.reportsApi.getPurchasesVat(fromDate, toDate).subscribe({
      next: (res) => {
        this.purchasesVat.set(res.success && res.data ? res.data : []);
        this.purchasesVatLoading.set(false);
      },
      error: () => { this.purchasesVat.set([]); this.purchasesVatLoading.set(false); }
    });
  }

  private loadSupplierBalances(): void {
    this.supplierBalancesLoading.set(true);
    this.reportsApi.getSupplierBalances().subscribe({
      next: (res) => {
        this.supplierBalances.set(res.success && res.data ? res.data : []);
        this.supplierBalancesLoading.set(false);
      },
      error: () => { this.supplierBalances.set([]); this.supplierBalancesLoading.set(false); }
    });
  }

  private loadSupplierWithholdings(): void {
    this.supplierWithholdingsLoading.set(true);
    const { fromDate, toDate } = this.getPeriodDates();
    this.reportsApi.getSupplierWithholdings(fromDate, toDate).subscribe({
      next: (res) => {
        this.supplierWithholdings.set(res.success && res.data ? res.data : []);
        this.supplierWithholdingsLoading.set(false);
      },
      error: () => { this.supplierWithholdings.set([]); this.supplierWithholdingsLoading.set(false); }
    });
  }

  loadReportsData(): void {
    this.loading.set(true);
    this.reportsService.loadReportsData(this.selectedPeriod).subscribe({
      next: (data) => {
        this.reportsData.set(data);
        const amounts = data.expenseChartData.map(d => d.amount);
        this.maxAmount = Math.max(...amounts, 1);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  loadCrossTabData(): void {
    this.crossTabLoading.set(true);
    this.reportsService.loadExpenseCrossTabData().subscribe({
      next: (data) => {
        this.crossTabData.set(data);
        this.crossTabLoading.set(false);
      },
      error: () => {
        this.crossTabData.set(null);
        this.crossTabLoading.set(false);
      }
    });
  }

  getBarHeight(amount: number): number {
    if (this.maxAmount === 0) return 4;
    const percent = (amount / this.maxAmount) * 100;
    return Math.max(percent, 4);
  }

  formatCurrency(amount: number): string {
    const formatted = new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 3, maximumFractionDigits: 3 }).format(amount);
    return `${formatted} TND`;
  }

  onExport(): void {
    if (this.activeTabIndex === 5) {
      this.exportSupplierWithholdingsTab();
      return;
    }

    const data = this.reportsData();
    const crossTab = this.crossTabData();
    if (!data) return;

    const rows: string[][] = [];
    rows.push(['Rapports Achats InstaFact', '']);
    rows.push(['Période', this.periodOptions.find(p => p.value === this.selectedPeriod)?.label || this.selectedPeriod]);
    rows.push(['']);
    rows.push(['Indicateurs', 'Valeur']);
    rows.push(['Dépenses totales', data.totalExpenses]);
    rows.push(['Factures fournisseurs', data.totalInvoices.toString()]);
    rows.push(['Factures payées', data.paidInvoices.toString()]);
    rows.push(['Taux de paiement', data.paymentRate]);
    rows.push(['Montant à payer', data.amountToPay]);
    rows.push(['']);
    rows.push(['Principaux fournisseurs', '', '']);
    rows.push(['Fournisseur', 'Factures', 'Montant total']);
    data.topSuppliers.forEach(s => {
      rows.push([s.supplierName, s.invoiceCount.toString(), `${s.totalAmount.toFixed(3)} ${s.currency}`]);
    });

    if (crossTab && crossTab.years.length > 0) {
      rows.push(['']);
      rows.push(['Tableau croisé dépenses par mois et année', '']);
      const headerRow = ['Année', ...crossTab.monthLabels, 'Total'];
      rows.push(headerRow);
      crossTab.years.forEach(year => {
        const monthAmts = crossTab.matrix.get(year) ?? [];
        const rowTotal = crossTab.rowTotals.get(year) ?? 0;
        rows.push([year.toString(), ...monthAmts.map(a => a.toFixed(3)), `${rowTotal.toFixed(3)} ${crossTab.currency}`]);
      });
      rows.push(['Total', ...crossTab.columnTotals.map(c => c.toFixed(3)), `${crossTab.grandTotal.toFixed(3)} ${crossTab.currency}`]);
    }

    const csv = rows.map(row => row.map(cell => `"${String(cell).replace(/"/g, '""')}"`).join(';')).join('\n');
    const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `rapports-achats-${formatLocalDate(new Date())}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private exportSupplierWithholdingsTab(): void {
    const escape = (v: string | number) => `"${String(v).replace(/"/g, '""')}"`;
    const rows = this.supplierWithholdings();
    const headers = ['Fournisseur', 'N° factures', 'Montant HT', 'Retenue', 'Net servi', 'Devise'];
    const csv = [
      headers.map(escape).join(';'),
      ...rows.map(r =>
        [r.supplierName, r.invoiceCount, r.totalHT.toFixed(3), r.totalWithholding.toFixed(3), r.totalNetPaid.toFixed(3), r.currency].map(escape).join(';')
      )
    ].join('\n');
    const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `rapports-achats-retenues-fournisseurs-${formatLocalDate(new Date())}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private readonly tabNames = [
    'Dépenses et fournisseurs', 'Transactions fournisseurs',
    'Détails achats par ligne', 'TVA achats',
    'Soldes fournisseur', 'Retenues fournisseurs', 'Synthèse mensuelle',
    'Répartition par statut', 'Dépenses par mois / Année'
  ];

  get activeTabName(): string {
    return this.tabNames[this.activeTabIndex] ?? 'Rapport';
  }

  get printDate(): string {
    return new Date().toLocaleDateString('fr-FR', {
      day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit'
    });
  }

  get selectedPeriodIndex(): number {
    return this.periodOptions.findIndex(p => p.value === this.selectedPeriod);
  }

  periodDateRangeLabel(): string {
    const { fromDate, toDate } = this.getPeriodDates();
    const from = new Date(fromDate + 'T12:00:00');
    const to = new Date(toDate + 'T12:00:00');
    const fromStr = from.toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' });
    const toStr = to.toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' });
    if (fromDate === toDate) return fromStr;
    return `${fromStr} – ${toStr}`;
  }

  onPrint(): void {
    globalThis.print();
  }
}
