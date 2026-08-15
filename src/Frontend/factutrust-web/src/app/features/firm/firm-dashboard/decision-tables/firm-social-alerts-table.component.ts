import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DashboardPanelComponent } from '@shared/components/dashboard/dashboard-panel.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { FirmSocialAlertRow } from '@core/services/firm-dashboard.service';

@Component({
  selector: 'app-firm-social-alerts-table',
  standalone: true,
  imports: [CommonModule, RouterModule, TableModule, TagModule, ButtonModule, DashboardPanelComponent, EmptyStateComponent],
  template: `
    <app-dashboard-panel title="Social & paie clients" [hasActions]="true" [flush]="true">
      <a panel-actions routerLink="/firm/governance/social" class="section-link">Voir tout</a>
      @if (loading) {
        <p class="muted">Chargement…</p>
      } @else if (rows.length === 0) {
        <app-empty-state icon="pi-users" title="Social au vert"
          description="Aucun signal social bloquant sur les dossiers clients." [showAction]="false" />
      } @else {
        <p-table [value]="rows" styleClass="p-datatable-sm decision-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Société</th><th>Salariés</th><th>Congés</th><th>Paie brouillon</th><th>DTS</th><th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td>{{ r.companyName }}</td>
              <td>{{ r.employeeCount }}</td>
              <td>
                @if (r.pendingLeaveRequests > 0) {
                  <p-tag severity="warn" [value]="r.pendingLeaveRequests + ''" />
                } @else { 0 }
              </td>
              <td>
                @if (r.payrollRunsDraftCount > 0) {
                  <p-tag severity="warn" [value]="r.payrollRunsDraftCount + ''" />
                } @else { 0 }
              </td>
              <td>
                @if (r.dtsPendingCount > 0) {
                  <p-tag severity="danger" [value]="r.dtsPendingCount + ''" />
                } @else { 0 }
              </td>
              <td>
                <a [routerLink]="['/firm/open', r.companyTenantId]"
                  pButton label="Ouvrir" class="p-button-text p-button-sm" icon="pi pi-folder-open"></a>
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
  `]
})
export class FirmSocialAlertsTableComponent {
  @Input() rows: FirmSocialAlertRow[] = [];
  @Input() loading = false;
}
