import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DashboardPanelComponent } from '@shared/components/dashboard/dashboard-panel.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { FirmCriticalFiscalRow } from '@core/services/firm-dashboard.service';

@Component({
  selector: 'app-firm-critical-fiscal-table',
  standalone: true,
  imports: [CommonModule, RouterModule, TableModule, TagModule, ButtonModule, DashboardPanelComponent, EmptyStateComponent],
  template: `
    <app-dashboard-panel title="Échéances fiscales critiques" [hasActions]="true" [flush]="true">
      <a panel-actions routerLink="/firm/fiscal-schedule" class="section-link">Voir tout</a>
      @if (loading) {
        <p class="muted">Chargement…</p>
      } @else if (rows.length === 0) {
        <app-empty-state icon="pi-calendar" title="Aucune échéance critique"
          description="Aucune obligation en retard ou à échéance dans les 7 prochains jours." [showAction]="false" />
      } @else {
        <p-table [value]="rows" styleClass="p-datatable-sm decision-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Société</th><th>Type</th><th>Échéance</th><th>Jours</th><th class="num">Montant</th><th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td>{{ r.companyName }}</td>
              <td><span class="obligation">{{ r.obligationLabel }}</span></td>
              <td>{{ r.dueDate | date:'shortDate' }}</td>
              <td>
                @if (r.isOverdue) {
                  <p-tag severity="danger" [value]="'+' + Math.abs(r.daysUntilDue) + ' j'" />
                } @else {
                  <p-tag severity="warn" [value]="r.daysUntilDue + ' j'" />
                }
              </td>
              <td class="num">{{ r.estimatedAmount | number:'1.3-3' }} {{ r.currency }}</td>
              <td>
                <a routerLink="/firm/fiscal-schedule"
                  [queryParams]="{ companyTenantId: r.companyTenantId }"
                  pButton label="Voir" class="p-button-text p-button-sm" icon="pi pi-external-link"></a>
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </app-dashboard-panel>
  `,
  styles: [`
    .section-link { font-size: 0.875rem; color: var(--color-primary, #3862f5); text-decoration: none; }
    .section-link:hover { text-decoration: underline; }
    .muted { color: var(--color-text-muted, #64748b); padding: 0.75rem 1rem; }
    .num { text-align: right; }
    .obligation { font-size: 0.85rem; }
    :host ::ng-deep .decision-table .p-datatable-tbody > tr > td { font-size: 0.875rem; }
  `]
})
export class FirmCriticalFiscalTableComponent {
  readonly Math = Math;
  @Input() rows: FirmCriticalFiscalRow[] = [];
  @Input() loading = false;
}
