import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FirmDashboardService,
  FirmDecisionTablesData
} from '@core/services/firm-dashboard.service';
import { FirmCriticalFiscalTableComponent } from './firm-critical-fiscal-table.component';
import { FirmAtRiskDossiersTableComponent } from './firm-at-risk-dossiers-table.component';
import { FirmNegativeMarginTableComponent } from './firm-negative-margin-table.component';
import { FirmPendingTimesheetsTableComponent } from './firm-pending-timesheets-table.component';
import { FirmHonorairesAlertsTableComponent } from './firm-honoraires-alerts-table.component';

@Component({
  selector: 'app-firm-decision-tables-section',
  standalone: true,
  imports: [
    CommonModule,
    FirmCriticalFiscalTableComponent,
    FirmAtRiskDossiersTableComponent,
    FirmNegativeMarginTableComponent,
    FirmPendingTimesheetsTableComponent,
    FirmHonorairesAlertsTableComponent
  ],
  template: `
    <section class="decision-section" aria-labelledby="decision-tables-title">
      <h2 id="decision-tables-title" class="section-title">Pilotage décisionnel</h2>

      @if (partialFailures().length > 0) {
        <div class="partial-banner" role="status">
          <i class="pi pi-exclamation-triangle"></i>
          <span>Données partielles : certains tableaux n'ont pas pu être chargés.</span>
        </div>
      }

      <div class="decision-grid">
        <app-firm-critical-fiscal-table [rows]="data()?.criticalFiscalSchedules ?? []" [loading]="loading()" />
        <app-firm-at-risk-dossiers-table [rows]="data()?.atRiskDossiers ?? []" [loading]="loading()" />
        <app-firm-negative-margin-table [rows]="data()?.negativeMargins ?? []" [loading]="loading()" />
        <app-firm-pending-timesheets-table [rows]="data()?.pendingTimeSheets ?? []" [loading]="loading()" />
        <app-firm-honoraires-alerts-table [rows]="data()?.honorairesAlerts ?? []" [loading]="loading()" />
      </div>
    </section>
  `,
  styles: [`
    .decision-section { margin-top: 1.5rem; margin-bottom: 1.5rem; }
    .section-title {
      margin: 0 0 1rem;
      font-size: 1rem;
      font-weight: 600;
      color: var(--color-neutral-700, #334155);
    }
    .decision-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(480px, 1fr));
      gap: 1.25rem;
    }
    .partial-banner {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 1rem;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      background: #fef3c7;
      color: #92400e;
      font-size: 0.875rem;
    }
    @media (max-width: 520px) {
      .decision-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class FirmDecisionTablesSectionComponent implements OnInit {
  private readonly dashboardService = inject(FirmDashboardService);

  readonly loading = signal(true);
  readonly data = signal<FirmDecisionTablesData | null>(null);
  readonly partialFailures = signal<{ section: string; message: string }[]>([]);

  ngOnInit(): void {
    this.dashboardService.getDecisionTables().subscribe({
      next: res => {
        if (res.success && res.data) {
          this.data.set(res.data);
          this.partialFailures.set(res.data.meta?.partialFailures ?? []);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
