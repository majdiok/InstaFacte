import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DashboardPanelComponent } from '@shared/components/dashboard/dashboard-panel.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { FirmAtRiskDossierRow } from '@core/services/firm-dashboard.service';

@Component({
  selector: 'app-firm-at-risk-dossiers-table',
  standalone: true,
  imports: [CommonModule, RouterModule, TableModule, TagModule, ButtonModule, DashboardPanelComponent, EmptyStateComponent],
  template: `
    <app-dashboard-panel title="Dossiers à risque" [hasActions]="true" [flush]="true">
      <a panel-actions routerLink="/firm/clients" class="section-link">Voir tout</a>
      @if (loading) {
        <p class="muted">Chargement…</p>
      } @else if (rows.length === 0) {
        <app-empty-state icon="pi-check-circle" title="Portefeuille sain"
          description="Aucun dossier nécessitant une action immédiate." [showAction]="false" />
      } @else {
        <p-table [value]="rows" styleClass="p-datatable-sm decision-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Société</th><th>Signaux</th><th>Dernière activité</th><th>DP</th><th>Responsable</th><th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td>{{ r.companyName }}</td>
              <td class="signals">
                @for (s of r.signals; track s) {
                  <p-tag [value]="s" [severity]="signalSeverity(s)" />
                }
              </td>
              <td>{{ r.lastJournalEntryDate ? (r.lastJournalEntryDate | date:'shortDate') : '—' }}</td>
              <td>
                @if (r.hasPermanentFile) {
                  {{ r.permanentFileCompletionPercent ?? 0 }}%
                  @if (r.permanentFileMissingItemsCount > 0) {
                    <span class="muted">({{ r.permanentFileMissingItemsCount }} manquant)</span>
                  }
                } @else {
                  <p-tag severity="warn" value="Sans DP" />
                }
              </td>
              <td>{{ r.assignedAccountantName ?? '—' }}</td>
              <td>
                @if (!r.hasPermanentFile || (r.permanentFileCompletionPercent ?? 0) < 100) {
                  <a routerLink="/firm/governance/permanent-files"
                    [queryParams]="{ assignmentId: r.assignmentId }"
                    pButton label="DP" class="p-button-text p-button-sm" icon="pi pi-folder-open"></a>
                }
                <a [routerLink]="['/firm/open', r.companyTenantId]"
                  pButton label="Ouvrir" class="p-button-text p-button-sm" icon="pi pi-calculator"></a>
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </app-dashboard-panel>
  `,
  styles: [`
    .section-link { font-size: 0.875rem; color: var(--color-primary, #3862f5); text-decoration: none; }
    .muted { color: var(--color-text-muted, #64748b); font-size: 0.8rem; }
    .signals { display: flex; flex-wrap: wrap; gap: 0.25rem; }
  `]
})
export class FirmAtRiskDossiersTableComponent {
  @Input() rows: FirmAtRiskDossierRow[] = [];
  @Input() loading = false;

  signalSeverity(signal: string): 'danger' | 'warn' | 'info' {
    if (signal.includes('Inactif')) return 'danger';
    if (signal.includes('DP') || signal.includes('Sans')) return 'warn';
    return 'info';
  }
}
