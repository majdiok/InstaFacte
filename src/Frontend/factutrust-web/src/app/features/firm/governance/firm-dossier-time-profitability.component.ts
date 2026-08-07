import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { ChartModule } from 'primeng/chart';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import {
  FirmDossierTimeProfitabilityReport,
  FirmGovernanceService,
  FirmMarginSignFilter
} from '@core/services/firm-governance.service';
import { FirmCollaboratorsService, FirmUser } from '@core/services/firm-collaborators.service';
import { ToastService } from '@core/services/toast.service';
import { downloadBlob } from '@features/accounting/shared/accounting-download.util';

@Component({
  selector: 'app-firm-dossier-time-profitability',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    SelectModule,
    InputTextModule,
    ChartModule,
    TagModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  template: `
    <app-page-header
      title="Feuilles de temps et rentabilité"
      subtitle="Analyse de la marge par dossier et collaborateur">
      <a routerLink="/firm/governance/time-sheets" class="link-btn">Retour</a>
      <button
        type="button"
        pButton
        label="Exporter PDF"
        icon="pi pi-file-pdf"
        class="p-button-sm"
        [loading]="exporting()"
        (click)="exportPdf()"></button>
    </app-page-header>

    <div class="toolbar fc-card">
      <label>Société
        <input
          pInputText
          [(ngModel)]="company"
          placeholder="Rechercher…"
          (keyup.enter)="load()" />
      </label>
      <label>Année
        <p-select
          [options]="yearOptions"
          [(ngModel)]="selectedYear"
          [showClear]="true"
          placeholder="Toutes"
          (onChange)="load()" />
      </label>
      <label>Collaborateur
        <p-select
          [options]="collaboratorOptions()"
          [(ngModel)]="selectedCollaboratorId"
          optionLabel="label"
          optionValue="value"
          [showClear]="true"
          [filter]="true"
          filterBy="label"
          placeholder="Tous"
          appendTo="body"
          (onChange)="load()" />
      </label>
      <label>Marge
        <p-select
          [options]="marginOptions"
          [(ngModel)]="selectedMargin"
          optionLabel="label"
          optionValue="value"
          (onChange)="load()" />
      </label>
      <button type="button" pButton label="Rechercher" icon="pi pi-search" class="p-button-sm" (click)="load()"></button>
    </div>

    <div class="summary fc-card">
      Total heures : <strong>{{ (report()?.totalHours ?? 0) | number:'1.2-2' }}</strong> h
      — facturables {{ (report()?.totalBillableHours ?? 0) | number:'1.2-2' }} h,
      non facturables {{ (report()?.totalNonBillableHours ?? 0) | number:'1.2-2' }} h
      · Budget unique : <strong>{{ (report()?.uniqueBudgetSum ?? 0) | number:'1.3-3' }}</strong>
    </div>

    <div class="charts">
      <div class="fc-card chart-card">
        <h3>Heures par année</h3>
        @if (yearChartData()) {
          <p-chart type="bar" [data]="yearChartData()!" [options]="chartOptions" [style]="{ height: '220px' }" />
        } @else {
          <p class="muted">Aucune donnée</p>
        }
      </div>
      <div class="fc-card chart-card">
        <h3>Heures par société</h3>
        @if (companyChartData()) {
          <p-chart type="bar" [data]="companyChartData()!" [options]="chartOptions" [style]="{ height: '220px' }" />
        } @else {
          <p class="muted">Aucune donnée</p>
        }
      </div>
    </div>

    <div class="fc-card">
      @if (!loading() && rows().length === 0) {
        <app-empty-state
          title="Aucune ligne de rentabilité"
          description="Ajustez les filtres ou saisissez des feuilles de temps."
          icon="pi pi-chart-line" />
      } @else {
        <p-table [value]="rows()" [loading]="loading()" [paginator]="true" [rows]="20" responsiveLayout="scroll">
          <ng-template pTemplate="header">
            <tr>
              <th>Nom Société</th>
              <th>Année</th>
              <th>Collaborateur</th>
              <th>Budget annuel</th>
              <th>Total Temps passé</th>
              <th>Dont facturables</th>
              <th>Taux horaire</th>
              <th>Coût de revient</th>
              <th>Marge</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr [class.neg]="r.margin < 0" [class.pos]="r.margin > 0">
              <td>{{ r.companyName }}</td>
              <td>{{ r.year }}</td>
              <td>{{ r.collaboratorName }}</td>
              <td>{{ r.budgetAnnuel | number:'1.3-3' }}{{ r.budgetFromFallback ? ' *' : '' }}</td>
              <td>{{ r.totalHours | number:'1.2-2' }}</td>
              <td>
                {{ r.billableHours | number:'1.2-2' }}
                <small class="ratio">{{ r.billableRatioPercent | number:'1.0-1' }} %</small>
              </td>
              <td class="rate-cell" [title]="r.hourlyRateBasis">
                {{ r.hourlyRate | number:'1.3-3' }}
                <p-tag
                  [severity]="rateSeverity(r.hourlyRateSource)"
                  [value]="r.hourlyRateSourceDisplay"
                  styleClass="rate-tag" />
              </td>
              <td>{{ r.cost | number:'1.3-3' }}</td>
              <td>{{ r.margin | number:'1.3-3' }}</td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-3, 12px);
      margin-bottom: 1rem;
    }
    .toolbar { display: flex; flex-wrap: wrap; gap: .75rem; align-items: flex-end; }
    .toolbar label { display: flex; flex-direction: column; font-size: .875rem; gap: .25rem; min-width: 140px; }
    .link-btn { font-size: .875rem; color: var(--color-primary, #0f766e); margin-right: .5rem; }
    .summary { font-size: .875rem; color: var(--color-text-muted, #64748b); }
    .charts { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    @media (max-width: 900px) { .charts { grid-template-columns: 1fr; } }
    .chart-card h3 { margin: 0 0 .75rem; font-size: .95rem; }
    .muted { color: var(--color-text-muted, #64748b); font-size: .875rem; }
    tr.neg td:last-child { color: #b91c1c; font-weight: 600; }
    tr.pos td:last-child { color: #047857; font-weight: 600; }
    .rate-cell { white-space: nowrap; }
    :host ::ng-deep .rate-tag { margin-left: .35rem; font-size: .6875rem; }
    .ratio { display: block; color: var(--color-text-muted, #64748b); font-size: .75rem; }
  `]
})
export class FirmDossierTimeProfitabilityComponent implements OnInit {
  private readonly api = inject(FirmGovernanceService);
  private readonly collaboratorsApi = inject(FirmCollaboratorsService);
  private readonly toast = inject(ToastService);

  report = signal<FirmDossierTimeProfitabilityReport | null>(null);
  loading = signal(false);
  exporting = signal(false);
  collaborators = signal<FirmUser[]>([]);

  company = '';
  selectedYear: number | null = new Date().getFullYear();
  selectedCollaboratorId: string | null = null;
  selectedMargin: FirmMarginSignFilter = 0;

  readonly yearOptions = Array.from({ length: 8 }, (_, i) => {
    const y = new Date().getFullYear() - 4 + i;
    return { label: String(y), value: y };
  });

  readonly marginOptions: { label: string; value: FirmMarginSignFilter }[] = [
    { label: 'Toutes', value: 0 },
    { label: 'Négatives', value: 1 },
    { label: 'Positives', value: 2 }
  ];

  readonly chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true } }
  };

  rows = computed(() => this.report()?.rows ?? []);

  collaboratorOptions = computed(() =>
    this.collaborators().map(u => ({
      label: `${u.firstName} ${u.lastName}`.trim() || u.email,
      value: u.id
    }))
  );

  yearChartData = computed(() => this.toChartData(this.report()?.hoursByYear));
  companyChartData = computed(() => this.toChartData(this.report()?.hoursByCompany));

  /**
   * Signale visuellement les taux non justifiés.
   *
   * Un taux calculé ou imposé est défendable ; un taux par défaut cabinet signale au contraire
   * qu'aucun coût employeur n'est renseigné et que la marge affichée n'est qu'indicative.
   */
  rateSeverity(source: number): 'success' | 'info' | 'warn' {
    switch (source) {
      case 1: return 'success';  // calculé
      case 2: return 'info';     // imposé, justifié
      default: return 'warn';    // profil historique ou défaut cabinet
    }
  }

  ngOnInit(): void {
    this.collaboratorsApi.list({ isActive: true }).subscribe({
      next: users => this.collaborators.set(users),
      error: () => { /* dropdown optionnel */ }
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getDossierTimeProfitability({
      company: this.company || undefined,
      year: this.selectedYear ?? undefined,
      collaboratorUserId: this.selectedCollaboratorId ?? undefined,
      margin: this.selectedMargin
    }).subscribe({
      next: res => {
        this.report.set(res.data ?? null);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Chargement impossible.' });
      }
    });
  }

  exportPdf(): void {
    this.exporting.set(true);
    this.api.exportDossierTimeProfitabilityPdf({
      company: this.company || undefined,
      year: this.selectedYear ?? undefined,
      collaboratorUserId: this.selectedCollaboratorId ?? undefined,
      margin: this.selectedMargin
    }).subscribe({
      next: blob => {
        downloadBlob(blob, 'rentabilite-dossiers.pdf');
        this.exporting.set(false);
      },
      error: () => {
        this.exporting.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Export PDF impossible.' });
      }
    });
  }

  private toChartData(slices?: { label: string; value: number }[]): { labels: string[]; datasets: { data: number[]; backgroundColor: string }[] } | null {
    if (!slices?.length) return null;
    return {
      labels: slices.map(s => s.label),
      datasets: [{
        data: slices.map(s => s.value),
        backgroundColor: 'rgba(15, 118, 110, 0.65)'
      }]
    };
  }
}
