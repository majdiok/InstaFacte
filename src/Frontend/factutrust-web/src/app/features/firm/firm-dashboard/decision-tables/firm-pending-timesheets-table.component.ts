import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DashboardPanelComponent } from '@shared/components/dashboard/dashboard-panel.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { FirmPendingTimeSheetRow } from '@core/services/firm-dashboard.service';

@Component({
  selector: 'app-firm-pending-timesheets-table',
  standalone: true,
  imports: [CommonModule, RouterModule, TableModule, TagModule, ButtonModule, DashboardPanelComponent, EmptyStateComponent],
  template: `
    <app-dashboard-panel title="Feuilles de temps en attente" [hasActions]="true" [flush]="true">
      <a panel-actions routerLink="/firm/governance/time-sheets" class="section-link">Voir tout</a>
      @if (loading) {
        <p class="muted">Chargement…</p>
      } @else if (rows.length === 0) {
        <app-empty-state icon="pi-clock" title="Aucune saisie bloquée"
          description="Toutes les feuilles de temps soumises ont été traitées." [showAction]="false" />
      } @else {
        <p-table [value]="rows" styleClass="p-datatable-sm decision-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Collaborateur</th><th>Période</th><th class="num">Heures</th><th>Dossiers</th><th>Statut</th><th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td>{{ r.collaboratorName }}</td>
              <td>{{ formatPeriod(r.periodYear, r.periodMonth) }}</td>
              <td class="num">{{ r.submittedHours | number:'1.2-2' }}</td>
              <td class="companies">{{ r.companyNames.join(', ') || '—' }}</td>
              <td><p-tag severity="warn" value="Soumis" /></td>
              <td>
                <a routerLink="/firm/governance/time-sheets"
                  [queryParams]="{ collaboratorId: r.collaboratorUserId, year: r.periodYear, month: r.periodMonth }"
                  pButton label="Valider" class="p-button-text p-button-sm" icon="pi pi-check"></a>
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </app-dashboard-panel>
  `,
  styles: [`
    .section-link { font-size: 0.875rem; color: var(--color-primary, #3862f5); text-decoration: none; }
    .muted { color: var(--color-text-muted, #64748b); padding: 0.75rem 1rem; }
    .num { text-align: right; }
    .companies { font-size: 0.85rem; max-width: 180px; }
  `]
})
export class FirmPendingTimesheetsTableComponent {
  @Input() rows: FirmPendingTimeSheetRow[] = [];
  @Input() loading = false;

  formatPeriod(year: number, month: number): string {
    const d = new Date(year, month - 1, 1);
    return d.toLocaleDateString('fr-TN', { month: 'long', year: 'numeric' });
  }
}
