import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DashboardPanelComponent } from '@shared/components/dashboard/dashboard-panel.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { FirmNegativeMarginRow } from '@core/services/firm-dashboard.service';

@Component({
  selector: 'app-firm-negative-margin-table',
  standalone: true,
  imports: [CommonModule, RouterModule, TableModule, ButtonModule, DashboardPanelComponent, EmptyStateComponent],
  template: `
    <app-dashboard-panel title="Marges dossiers négatives" [hasActions]="true" [flush]="true">
      <a panel-actions routerLink="/firm/governance/dossier-time-profitability"
        [queryParams]="{ margin: 'negative' }" class="section-link">Voir tout</a>
      @if (loading) {
        <p class="muted">Chargement…</p>
      } @else if (rows.length === 0) {
        <app-empty-state icon="pi-chart-line" title="Aucune marge négative"
          description="Aucun dossier avec marge négative sur l'exercice courant." [showAction]="false" />
      } @else {
        <p-table [value]="rows" styleClass="p-datatable-sm decision-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Société</th><th>Collaborateur</th><th class="num">Budget</th><th class="num">Heures</th><th class="num">Marge</th><th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr class="neg">
              <td>{{ r.companyName }}</td>
              <td>{{ r.collaboratorName }}</td>
              <td class="num">{{ r.budgetAnnuel | number:'1.3-3' }}</td>
              <td class="num">{{ r.totalHours | number:'1.2-2' }}</td>
              <td class="num neg-val">{{ r.margin | number:'1.3-3' }}</td>
              <td>
                <a routerLink="/firm/governance/dossier-time-profitability"
                  [queryParams]="{ company: r.companyName, year: r.year, margin: 'negative' }"
                  pButton label="Analyser" class="p-button-text p-button-sm" icon="pi pi-search"></a>
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
    .neg-val { color: var(--color-danger, #dc2626); font-weight: 600; }
  `]
})
export class FirmNegativeMarginTableComponent {
  @Input() rows: FirmNegativeMarginRow[] = [];
  @Input() loading = false;
}
