import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { FirmAssignmentService, FirmClientAssignment } from '@core/services/firm-assignment.service';
import { AuthService } from '@core/services/auth.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { FirmInvitationActionsService } from '../shared/firm-invitation-actions.service';

@Component({
  selector: 'app-firm-invitations',
  standalone: true,
  imports: [CommonModule, TableModule, PageHeaderComponent, EmptyStateComponent],
  template: `
    <app-page-header
      title="Invitations"
      subtitle="Demandes de liaison envoyées par les sociétés clientes">
    </app-page-header>

    @if (!loading() && items().length === 0) {
      <app-empty-state
        icon="pi-inbox"
        title="Aucune invitation en attente"
        description="Les demandes de liaison des sociétés apparaîtront ici.">
      </app-empty-state>
    } @else {
      <div class="fi-card">
        <p-table [value]="items()" [loading]="loading()">
          <ng-template pTemplate="header">
            <tr>
              <th>Société</th>
              <th>Demandée le</th>
              <th>Notes</th>
              <th class="fi-actions-col"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.companyName }}</td>
              <td>{{ row.requestedAt | date:'dd/MM/yyyy HH:mm' }}</td>
              <td>{{ row.notes || '—' }}</td>
              <td class="fi-actions">
                @if (auth.isFirmManager()) {
                  <button type="button" class="fi-btn fi-btn--success" (click)="accept(row)">
                    <i class="pi pi-check"></i> Accepter
                  </button>
                  <button type="button" class="fi-btn fi-btn--danger-outline" (click)="reject(row)">
                    <i class="pi pi-times"></i> Refuser
                  </button>
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    }
  `,
  styles: [`
    :host { display: block; }
    .fi-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-2, 8px);
    }
    .fi-actions-col { width: 1%; }
    .fi-actions { display: flex; gap: 0.5rem; justify-content: flex-end; }
    .fi-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.45rem 0.85rem;
      border-radius: var(--radius-md, 8px);
      font-size: 0.85rem;
      font-weight: 500;
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 0.15s ease;
    }
    .fi-btn--success { background: var(--color-success-600, #16a34a); color: #fff; }
    .fi-btn--success:hover { background: var(--color-success-700, #15803d); }
    .fi-btn--danger-outline {
      background: transparent;
      color: var(--color-danger-600, #dc2626);
      border-color: var(--color-danger-300, #fca5a5);
    }
    .fi-btn--danger-outline:hover { background: var(--color-danger-50, #fef2f2); }
  `]
})
export class FirmInvitationsComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly assignments = inject(FirmAssignmentService);
  private readonly actions = inject(FirmInvitationActionsService);

  readonly items = signal<FirmClientAssignment[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.assignments.getIncomingInvitations().subscribe({
      next: r => {
        if (r.success) this.items.set(r.data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  async accept(row: FirmClientAssignment): Promise<void> {
    if (await this.actions.accept(row)) this.load();
  }

  async reject(row: FirmClientAssignment): Promise<void> {
    if (await this.actions.reject(row)) this.load();
  }
}
