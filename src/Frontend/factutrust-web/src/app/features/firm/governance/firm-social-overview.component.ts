import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { FirmGovernanceService, FirmSocialClientRow } from '@core/services/firm-governance.service';

@Component({
  selector: 'app-firm-social-overview',
  standalone: true,
  imports: [CommonModule, RouterModule, TableModule, ButtonModule, TagModule, PageHeaderComponent, EmptyStateComponent],
  template: `
    <app-page-header title="Suivi social & paie" subtitle="Vue cabinet multi-dossiers (CNSS, congés, paie)" />

    @if (clients().length === 0) {
      <app-empty-state
        icon="pi-users"
        title="Aucun dossier client actif"
        description="Acceptez une invitation pour commencer le suivi social multi-dossiers."
        actionLabel="Voir les invitations"
        actionRoute="/firm/invitations">
      </app-empty-state>
    } @else {
      <div class="fc-card">
        <p-table [value]="clients()">
          <ng-template pTemplate="header">
            <tr>
              <th>Société</th><th>Salariés actifs</th><th>Congés en attente</th><th>Cycles paie brouillon</th><th>DTS en attente</th><th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-c>
            <tr>
              <td>{{ c.companyName }}</td>
              <td>{{ c.employeeCount }}</td>
              <td>{{ c.pendingLeaveRequests }}</td>
              <td>{{ c.payrollRunsDraftCount }}</td>
              <td>
                @if (c.dtsPendingCount > 0) {
                  <p-tag [value]="c.dtsPendingCount + ''" severity="danger" />
                } @else {
                  {{ c.dtsPendingCount }}
                }
              </td>
              <td>
                <a [routerLink]="['/firm/open', c.companyTenantId]" pButton label="Ouvrir dossier" class="p-button-text p-button-sm" icon="pi pi-folder-open"></a>
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    }
  `,
  styles: [`
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-2, 8px);
    }
  `]
})
export class FirmSocialOverviewComponent implements OnInit {
  private readonly api = inject(FirmGovernanceService);
  clients = signal<FirmSocialClientRow[]>([]);

  ngOnInit(): void {
    this.api.getSocialOverview().subscribe({
      next: res => this.clients.set(res.data?.clients ?? [])
    });
  }
}
