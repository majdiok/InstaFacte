import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DashboardPanelComponent } from '@shared/components/dashboard/dashboard-panel.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { FirmHonorairesAlertRow } from '@core/services/firm-dashboard.service';

@Component({
  selector: 'app-firm-honoraires-alerts-table',
  standalone: true,
  imports: [CommonModule, RouterModule, TableModule, TagModule, ButtonModule, DashboardPanelComponent, EmptyStateComponent],
  template: `
    <app-dashboard-panel title="Honoraires & recouvrement" [hasActions]="true" [flush]="true">
      <a panel-actions routerLink="/firm/billing/invoices" class="section-link">Voir tout</a>
      @if (loading) {
        <p class="muted">Chargement…</p>
      } @else if (rows.length === 0) {
        <app-empty-state icon="pi-wallet" title="Recouvrement OK"
          description="Aucun dossier avec impayés ou taux de recouvrement faible." [showAction]="false" />
      } @else {
        @if (hasSnapshotData) {
          <p class="snapshot-hint">Certaines lignes utilisent des données snapshot rentabilité.</p>
        }
        <p-table [value]="rows" styleClass="p-datatable-sm decision-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Société</th><th class="num">CA YTD</th><th class="num">Encaissé</th><th class="num">Solde</th><th class="num">Recouvr. %</th><th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td>
                {{ r.companyName }}
                @if (r.isSnapshotData) {
                  <p-tag severity="info" value="Snapshot" class="snapshot-tag" />
                }
              </td>
              <td class="num">{{ r.billedYtdAmount | number:'1.3-3' }}</td>
              <td class="num">{{ r.collectedAmount | number:'1.3-3' }}</td>
              <td class="num">{{ r.debitBalance | number:'1.3-3' }}</td>
              <td class="num">{{ r.recoveryRatePercent | number:'1.1-1' }}%</td>
              <td>
                <a routerLink="/firm/billing/invoices"
                  [queryParams]="{ assignmentId: r.firmClientAssignmentId }"
                  pButton label="Factures" class="p-button-text p-button-sm" icon="pi pi-file"></a>
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
    .snapshot-hint {
      margin: 0;
      padding: 0.5rem 1rem;
      font-size: 0.8rem;
      color: var(--color-text-muted, #64748b);
      background: var(--color-surface-muted, #f8fafc);
    }
    .snapshot-tag { margin-left: 0.35rem; vertical-align: middle; }
  `]
})
export class FirmHonorairesAlertsTableComponent {
  @Input() rows: FirmHonorairesAlertRow[] = [];
  @Input() loading = false;

  get hasSnapshotData(): boolean {
    return this.rows.some(r => r.isSnapshotData);
  }
}
