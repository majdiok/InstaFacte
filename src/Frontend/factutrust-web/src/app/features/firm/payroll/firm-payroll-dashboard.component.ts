import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ChartModule } from 'primeng/chart';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { ChartCardComponent } from '@shared/components/dashboard/chart-card.component';
import { DashboardPanelComponent } from '@shared/components/dashboard/dashboard-panel.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { PayrollService, PayrollDashboard, PayrollDashboardDeadline } from '@core/services/payroll.service';
import { PayrollAmountPipe, formatPayrollAmount } from '../../payroll/shared/payroll-amount.pipe';

/** Teintes des secteurs, alignées sur la palette applicative. */
const SLICE_COLORS = ['#3862f5', '#0ea5e9', '#f59e0b', '#8b5cf6', '#10b981', '#ef4444'];

@Component({
  selector: 'app-firm-payroll-dashboard',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    ChartModule, SelectModule, TableModule, TagModule,
    PageHeaderComponent, ButtonComponent, StatCardComponent,
    ChartCardComponent, DashboardPanelComponent, EmptyStateComponent,
    PayrollAmountPipe
  ],
  template: `
    <app-page-header
      title="Paie interne du cabinet"
      [subtitle]="data()?.periodLabel || 'Chargement…'">
      <div class="period-picker">
        <p-select [options]="monthOptions" optionLabel="label" optionValue="value"
          [(ngModel)]="selectedMonth" (onChange)="load()" appendTo="body" />
        <p-select [options]="yearOptions" [(ngModel)]="selectedYear" (onChange)="load()" appendTo="body" />
        <app-button variant="outline" size="sm" icon="pi-refresh" (clicked)="load()">Actualiser</app-button>
      </div>
    </app-page-header>

    @if (loading() && initialLoad()) {
      <div class="kpi-row">
        @for (i of [1,2,3,4,5]; track i) { <div class="stat-skeleton"></div> }
      </div>
    }

    <!-- Bloc distinct du squelette : l'alias "as" n'est disponible que sur un @if principal. -->
    @if (data(); as d) {

      @if (!d.hasRun) {
        <div class="notice">
          <i class="pi pi-info-circle"></i>
          <span>Aucun cycle de paie pour {{ d.periodLabel }}.</span>
          <a routerLink="/firm/payroll/runs" class="notice-link">Ouvrir les cycles de paie</a>
        </div>
      }

      <div class="kpi-row">
        <app-stat-card appearance="solid" variant="primary" icon="pi-wallet"
          label="Total brut" [value]="amount(d.gross.amount)" [change]="d.gross.changePercent ?? 0"
          [routerLink]="d.runId ? ['/firm/payroll/runs', d.runId] : undefined" />
        <app-stat-card appearance="solid" variant="success" icon="pi-money-bill"
          label="Net à payer" [value]="amount(d.net.amount)" [change]="d.net.changePercent ?? 0" />
        <app-stat-card appearance="solid" variant="warning" icon="pi-building"
          label="Charges patronales" [value]="amount(d.employerCharges.amount)"
          [change]="d.employerCharges.changePercent ?? 0" />
        <app-stat-card appearance="solid" variant="primary" icon="pi-users"
          label="Effectif du cycle" [value]="d.employeeCount"
          [routerLink]="['/firm/payroll/employees']" />
        <app-stat-card appearance="solid" [variant]="nextDeadlineVariant()" icon="pi-calendar-clock"
          label="Prochaine échéance" [value]="nextDeadlineValue()">
          <span class="stat-sub">{{ nextDeadlineCaption() }}</span>
        </app-stat-card>
      </div>

      <div class="charts-grid">
        <app-chart-card title="Répartition des charges patronales"
          [subtitle]="d.hasRun ? amount(d.employerCharges.amount) : 'Aucun cycle'">
          @if (d.employerChargeBreakdown.length) {
            <p-chart type="doughnut" [data]="chargesChart()" [options]="doughnutOptions" [style]="{ height: '240px' }" />
          } @else {
            <app-empty-state icon="pi-chart-pie" iconSize="2rem" title="Aucune charge"
              description="Calculez le cycle du mois pour voir la répartition." [showAction]="false" />
          }
        </app-chart-card>

        <app-chart-card title="Évolution de l'exercice" subtitle="Brut et net par mois, charges patronales en courbe">
          <p-chart type="bar" [data]="seriesChart()" [options]="seriesOptions" [style]="{ height: '240px' }" />
        </app-chart-card>
      </div>

      <div class="dashboard-grid">
        <app-dashboard-panel [flush]="true" title="Salariés du cycle"
          [subtitle]="d.runStatusDisplay || undefined">
          @if (d.payslips.length) {
            <p-table [value]="d.payslips" responsiveLayout="scroll" [paginator]="d.payslips.length > 10" [rows]="10">
              <ng-template pTemplate="header">
                <tr>
                  <th>Salarié</th><th>Matricule</th><th class="num">Brut</th>
                  <th class="num">Retenues</th><th class="num">Net à payer</th><th>Paiement</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-p>
                <tr>
                  <td>{{ p.employeeName }}</td>
                  <td class="muted">{{ p.employeeNumber }}</td>
                  <td class="num">{{ p.grossSalary | payrollAmount:false }}</td>
                  <td class="num">{{ (p.grossSalary - p.netSalary) | payrollAmount:false }}</td>
                  <td class="num"><strong>{{ p.netSalary | payrollAmount:false }}</strong></td>
                  <td><p-tag [value]="p.paymentStatusDisplay" [severity]="paymentSeverity(p.paymentStatus)" /></td>
                </tr>
              </ng-template>
            </p-table>
          } @else {
            <div class="panel-empty">
              <app-empty-state icon="pi-users" iconSize="2rem" title="Aucun bulletin"
                description="Le cycle du mois n'a pas encore été calculé."
                actionLabel="Ouvrir les cycles" actionRoute="/firm/payroll/runs" />
            </div>
          }
        </app-dashboard-panel>

        <div class="side-column">
          <app-dashboard-panel title="Éléments de paie">
            @if (d.breakdownWarning) {
              <p class="warning">{{ d.breakdownWarning }}</p>
            }
            @for (s of d.earningsBreakdown; track s.label) {
              <div class="line"><span>{{ s.label }}</span><span>{{ s.amount | payrollAmount:false }}</span></div>
            } @empty {
              <p class="muted">Aucun élément sur ce mois.</p>
            }
            @if (d.earningsBreakdown.length) {
              <div class="line line--total"><span>Total brut</span><span>{{ d.gross.amount | payrollAmount:false }}</span></div>
            }
          </app-dashboard-panel>

          <app-dashboard-panel title="Retenues salariales">
            @for (s of d.deductionBreakdown; track s.label) {
              <div class="line"><span>{{ s.label }}</span><span>{{ s.amount | payrollAmount:false }}</span></div>
            } @empty {
              <p class="muted">Aucune retenue sur ce mois.</p>
            }
            @if (d.deductionBreakdown.length) {
              <div class="line line--total line--danger">
                <span>Total retenues</span><span>{{ totalDeductions() | payrollAmount:false }}</span>
              </div>
            }
          </app-dashboard-panel>

          <app-dashboard-panel title="Échéances à venir">
            @for (e of d.upcomingDeadlines; track e.label + e.dueDate) {
              <div class="deadline" [class.deadline--late]="e.isOverdue" [class.deadline--soon]="!e.isOverdue && e.daysRemaining <= 7">
                <div>
                  <span class="deadline-label">{{ e.label }}</span>
                  <span class="muted">{{ e.dueDate | date:'dd/MM/yyyy' }}</span>
                </div>
                <span class="deadline-when">{{ describeDeadline(e) }}</span>
              </div>
            } @empty {
              <p class="muted">Aucune échéance sociale sur les trois prochains mois.</p>
            }
          </app-dashboard-panel>
        </div>
      </div>

      <app-dashboard-panel [flush]="true" title="Derniers cycles de paie">
        @if (d.recentRuns.length) {
          <p-table [value]="d.recentRuns" responsiveLayout="scroll">
            <ng-template pTemplate="header">
              <tr>
                <th>Période</th><th>Statut</th><th class="num">Brut</th>
                <th class="num">Net</th><th>Validé le</th><th style="width:6rem"></th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-r>
              <tr>
                <td>{{ r.label }}</td>
                <td><p-tag [value]="r.statusDisplay" [severity]="runSeverity(r.status)" /></td>
                <td class="num">{{ r.totalGross | payrollAmount:false }}</td>
                <td class="num">{{ r.totalNet | payrollAmount:false }}</td>
                <td class="muted">{{ r.validatedAt ? (r.validatedAt | date:'dd/MM/yyyy') : '—' }}</td>
                <td><a [routerLink]="['/firm/payroll/runs', r.id]" class="row-link">Ouvrir</a></td>
              </tr>
            </ng-template>
          </p-table>
        } @else {
          <div class="panel-empty">
            <app-empty-state icon="pi-calendar" iconSize="2rem" title="Aucun cycle sur l'exercice"
              actionLabel="Créer un cycle" actionRoute="/firm/payroll/runs" />
          </div>
        }
      </app-dashboard-panel>
    }

    <h2 class="section-title">Accès rapides</h2>
    <div class="hub-grid">
      <a routerLink="/firm/payroll/employees" class="hub-card">
        <i class="pi pi-users hub-icon"></i>
        <h3>Salariés</h3>
        <p>Dossiers salariés et contrats des collaborateurs du cabinet.</p>
      </a>
      <a routerLink="/firm/payroll/runs" class="hub-card">
        <i class="pi pi-calendar hub-icon"></i>
        <h3>Cycles de paie</h3>
        <p>Créer, calculer et valider les bulletins de paie interne.</p>
      </a>
      <a routerLink="/firm/governance/collaborator-costs" class="hub-card">
        <i class="pi pi-coins hub-icon"></i>
        <h3>Coûts collaborateurs</h3>
        <p>Synchroniser les taux horaires depuis la paie validée.</p>
      </a>
      @if (isManager()) {
        <a routerLink="/firm/payroll/declarations" class="hub-card">
          <i class="pi pi-file-export hub-icon"></i>
          <h3>Déclarations sociales</h3>
          <p>DTS CNSS, certificats de retenue et déclarations de salaires.</p>
        </a>
        <a routerLink="/firm/payroll/settings" class="hub-card">
          <i class="pi pi-cog hub-icon"></i>
          <h3>Paramètres paie</h3>
          <p>Barèmes de l'exercice, jours fériés et primes annuelles.</p>
        </a>
      }
      <a routerLink="/firm/payroll/reports/payroll-book" class="hub-card">
        <i class="pi pi-book hub-icon"></i>
        <h3>Rapports</h3>
        <p>Livre de paie et journal comptable de la paie interne.</p>
      </a>
      <a routerLink="/firm/governance/leaves" class="hub-card">
        <i class="pi pi-calendar-times hub-icon"></i>
        <h3>Congés &amp; Absences</h3>
        <p>Point de saisie unique : les congés approuvés sont reportés en paie.</p>
      </a>
    </div>
  `,
  styles: [`
  :host { display: block; }
  .period-picker { display: flex; gap: .5rem; align-items: center; flex-wrap: wrap; }

  .kpi-row {
    display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
    gap: var(--spacing-4, 1rem); margin-bottom: var(--spacing-4, 1rem);
  }
  .stat-skeleton {
    height: 118px; border-radius: var(--radius-xl, 12px);
    background: linear-gradient(90deg, #f1f5f9 25%, #e2e8f0 37%, #f1f5f9 63%);
    background-size: 400% 100%; animation: shimmer 1.4s ease infinite;
  }
  @keyframes shimmer { 0% { background-position: 100% 0; } 100% { background-position: -100% 0; } }
  .stat-sub { font-size: .78rem; opacity: .85; }

  .notice {
    display: flex; align-items: center; gap: .6rem; flex-wrap: wrap;
    background: var(--color-surface-muted, #f8fafc);
    border: 1px solid var(--color-border-subtle, #e2e8f0);
    border-radius: var(--radius-lg, 12px);
    padding: var(--spacing-3, .75rem); margin-bottom: var(--spacing-4, 1rem); font-size: .875rem;
  }
  .notice-link { margin-left: auto; color: var(--color-primary, #3862f5); }

  .charts-grid {
    display: grid; grid-template-columns: 1fr 1fr;
    gap: var(--spacing-4, 1rem); margin-bottom: var(--spacing-4, 1rem);
  }
  .dashboard-grid {
    display: grid; grid-template-columns: 3fr 2fr;
    gap: var(--spacing-4, 1rem); margin-bottom: var(--spacing-4, 1rem);
  }
  .side-column { display: flex; flex-direction: column; gap: var(--spacing-4, 1rem); }

  .line {
    display: flex; justify-content: space-between; gap: 1rem;
    padding: .4rem 0; font-size: .875rem;
    border-bottom: 1px dashed var(--color-border-subtle, #e2e8f0);
  }
  .line--total { font-weight: 600; border-bottom: none; border-top: 2px solid var(--color-border-subtle, #e2e8f0); margin-top: .25rem; }
  .line--danger { color: #b91c1c; }
  .warning { font-size: .8rem; color: #b45309; margin: 0 0 .5rem; }

  .deadline {
    display: flex; justify-content: space-between; align-items: center; gap: 1rem;
    padding: .5rem .6rem; border-left: 3px solid var(--color-border-subtle, #e2e8f0);
    border-radius: 6px; margin-bottom: .4rem; background: var(--color-surface-muted, #f8fafc);
  }
  .deadline--soon { border-left-color: #f59e0b; }
  .deadline--late { border-left-color: #ef4444; }
  .deadline-label { display: block; font-size: .875rem; font-weight: 500; }
  .deadline-when { font-size: .8rem; white-space: nowrap; color: var(--color-text-secondary, #64748b); }
  .deadline--late .deadline-when { color: #b91c1c; font-weight: 600; }

  .num { text-align: right; }
  .muted { color: var(--color-text-muted, #64748b); font-size: .8rem; }
  .row-link { color: var(--color-primary, #3862f5); font-size: .8rem; }
  .panel-empty { padding: var(--spacing-4, 1rem); }

  .section-title { font-size: 1.05rem; margin: var(--spacing-5, 1.5rem) 0 var(--spacing-3, .75rem); }
  .hub-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: var(--spacing-4, 1rem); }
  .hub-card {
    display: flex; flex-direction: column; gap: var(--spacing-2, .5rem);
    padding: var(--spacing-4, 1rem);
    border: 1px solid var(--color-border-subtle, #e2e8f0);
    border-radius: var(--radius-xl, 16px);
    background: var(--color-surface, #fff);
    text-decoration: none; color: inherit; transition: box-shadow .15s ease;
  }
  .hub-card:hover { box-shadow: var(--shadow-soft-md, 0 4px 12px rgba(15, 23, 42, 0.08)); }
  .hub-icon { font-size: 1.5rem; color: var(--color-primary, #3862f5); }
  .hub-card h3 { margin: 0; font-size: 1rem; }
  .hub-card p { margin: 0; font-size: .82rem; color: var(--color-text-muted, #64748b); }

  @media (max-width: 1024px) { .dashboard-grid { grid-template-columns: 1fr; } }
  @media (max-width: 900px) { .charts-grid { grid-template-columns: 1fr; } }
  `]
})
export class FirmPayrollDashboardComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);

  /** Le comptable cabinet consulte ; seul le responsable configure et déclare. */
  readonly isManager = this.auth.isFirmManager;

  readonly loading = signal(false);
  readonly initialLoad = signal(true);
  readonly data = signal<PayrollDashboard | null>(null);

  selectedYear = new Date().getFullYear();
  selectedMonth = new Date().getMonth() + 1;

  readonly yearOptions = Array.from({ length: 6 }, (_, i) => new Date().getFullYear() - i);
  readonly monthOptions = [
    'Janvier', 'Février', 'Mars', 'Avril', 'Mai', 'Juin',
    'Juillet', 'Août', 'Septembre', 'Octobre', 'Novembre', 'Décembre'
  ].map((label, i) => ({ label, value: i + 1 }));

  readonly doughnutOptions = {
    plugins: { legend: { position: 'bottom' as const, labels: { boxWidth: 12, font: { size: 11 } } } },
    maintainAspectRatio: false
  };

  /**
   * Barres (brut / net) et courbe (charges) sur un axe secondaire : les charges sont d'un ordre
   * de grandeur inférieur et seraient écrasées sur l'axe des salaires.
   */
  readonly seriesOptions = {
    plugins: { legend: { position: 'bottom' as const, labels: { boxWidth: 12, font: { size: 11 } } } },
    maintainAspectRatio: false,
    scales: {
      y: { position: 'left' as const, ticks: { callback: (v: number | string) => compact(Number(v)) } },
      y1: {
        position: 'right' as const, grid: { drawOnChartArea: false },
        ticks: { callback: (v: number | string) => compact(Number(v)) }
      }
    }
  };

  readonly chargesChart = computed(() => {
    const slices = this.data()?.employerChargeBreakdown ?? [];
    return {
      labels: slices.map(s => s.label),
      datasets: [{ data: slices.map(s => s.amount), backgroundColor: SLICE_COLORS.slice(0, slices.length) }]
    };
  });

  readonly seriesChart = computed(() => {
    const series = this.data()?.monthlySeries ?? [];
    return {
      labels: series.map(m => this.monthOptions[m.month - 1].label.slice(0, 4)),
      datasets: [
        { type: 'bar', label: 'Brut', data: series.map(m => m.gross), backgroundColor: '#3862f5', yAxisID: 'y' },
        { type: 'bar', label: 'Net', data: series.map(m => m.net), backgroundColor: '#0ea5e9', yAxisID: 'y' },
        {
          type: 'line', label: 'Charges patronales', data: series.map(m => m.employerCharges),
          borderColor: '#f59e0b', backgroundColor: '#f59e0b', tension: 0.35, yAxisID: 'y1'
        }
      ]
    };
  });

  readonly totalDeductions = computed(() =>
    (this.data()?.deductionBreakdown ?? []).reduce((sum, s) => sum + s.amount, 0));

  private readonly nextDeadline = computed<PayrollDashboardDeadline | null>(() =>
    this.data()?.upcomingDeadlines?.[0] ?? null);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.payroll.getDashboard(this.selectedYear, this.selectedMonth).subscribe({
      next: res => {
        this.data.set(res.data ?? null);
        this.loading.set(false);
        this.initialLoad.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.initialLoad.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Tableau de bord',
          detail: err?.error?.message || 'Chargement impossible.'
        });
      }
    });
  }

  amount(value: number): string { return formatPayrollAmount(value); }

  nextDeadlineValue(): string {
    const d = this.nextDeadline();
    if (!d) return '—';
    return new Date(d.dueDate).toLocaleDateString('fr-TN');
  }

  nextDeadlineCaption(): string {
    const d = this.nextDeadline();
    return d ? d.label : 'Aucune échéance sociale à venir';
  }

  nextDeadlineVariant(): 'primary' | 'warning' | 'error' {
    const d = this.nextDeadline();
    if (!d) return 'primary';
    if (d.isOverdue) return 'error';
    return d.daysRemaining <= 7 ? 'warning' : 'primary';
  }

  describeDeadline(e: PayrollDashboardDeadline): string {
    if (e.isOverdue) return `En retard de ${Math.abs(e.daysRemaining)} j`;
    if (e.daysRemaining === 0) return "Aujourd'hui";
    return `Dans ${e.daysRemaining} j`;
  }

  paymentSeverity(status: string): 'success' | 'warn' | 'secondary' {
    if (status === 'Paid') return 'success';
    if (status === 'PartiallyPaid') return 'warn';
    return 'secondary';
  }

  runSeverity(status: string): 'success' | 'info' | 'warn' | 'secondary' {
    if (status === 'Closed') return 'success';
    if (status === 'Validated') return 'info';
    if (status === 'Calculated') return 'warn';
    return 'secondary';
  }
}

/** Axes compacts : 12 500 → « 12,5K ». */
function compact(value: number): string {
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(1).replace('.', ',')}M`;
  if (Math.abs(value) >= 1_000) return `${(value / 1_000).toFixed(1).replace('.', ',')}K`;
  return String(value);
}
