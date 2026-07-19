import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { FirmAssignmentService, FirmClientDossier } from '@core/services/firm-assignment.service';
import { FirmContextService } from '@core/services/firm-context.service';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-firm-clients',
  standalone: true,
  imports: [CommonModule, TableModule, PageHeaderComponent, EmptyStateComponent],
  template: `
    <app-page-header
      title="Dossiers clients"
      subtitle="Sociétés dont vous gérez la comptabilité">
    </app-page-header>

    @if (!loading() && clients().length === 0) {
      <app-empty-state
        icon="pi-briefcase"
        title="Aucun dossier client"
        description="Acceptez une invitation depuis la page Invitations pour commencer à gérer un dossier.">
      </app-empty-state>
    } @else {
      <div class="fc-card">
        <p-table [value]="clients()" [loading]="loading()">
          <ng-template pTemplate="header">
            <tr>
              <th>Société</th>
              <th>Active depuis</th>
              <th class="fc-actions-col"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.companyName }}</td>
              <td>{{ row.activeSince | date:'dd/MM/yyyy' }}</td>
              <td class="fc-actions">
                <button type="button" class="fc-btn fc-btn--primary" (click)="openDossier(row)">
                  <i class="pi pi-folder-open"></i> Ouvrir la comptabilité
                </button>
                @if (auth.isFirmManager()) {
                  <button type="button" class="fc-btn fc-btn--danger-outline" (click)="revoke(row)">
                    <i class="pi pi-times"></i> Résilier
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
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-2, 8px);
    }
    .fc-actions-col { width: 1%; }
    .fc-actions { display: flex; gap: 0.5rem; justify-content: flex-end; }
    .fc-btn {
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
    .fc-btn--primary { background: var(--color-primary-600, #2563eb); color: #fff; }
    .fc-btn--primary:hover { background: var(--color-primary-700, #1d4ed8); }
    .fc-btn--danger-outline {
      background: transparent;
      color: var(--color-danger-600, #dc2626);
      border-color: var(--color-danger-300, #fca5a5);
    }
    .fc-btn--danger-outline:hover { background: var(--color-danger-50, #fef2f2); }
  `]
})
export class FirmClientsComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly assignments = inject(FirmAssignmentService);
  private readonly firmContext = inject(FirmContextService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly confirmation = inject(ConfirmationService);

  readonly clients = signal<FirmClientDossier[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.assignments.getActiveClients().subscribe({
      next: r => {
        if (r.success) this.clients.set(r.data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  async openDossier(row: FirmClientDossier): Promise<void> {
    try {
      await this.firmContext.switchClient(row.companyTenantId);
      await this.router.navigate(['/accounting/chart']);
    } catch {
      this.toast.add({ severity: 'error', summary: 'Erreur', detail: "Impossible d'ouvrir le dossier." });
    }
  }

  revoke(row: FirmClientDossier): void {
    this.confirmation.confirm({
      header: 'Résilier le dossier',
      message: `Résilier la liaison avec « ${row.companyName} » ? Vous perdrez l'accès à sa comptabilité.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Résilier',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.assignments.revokeClient(row.assignmentId).subscribe({
          next: r => {
            if (r.success) {
              this.toast.add({ severity: 'success', summary: 'Dossier résilié' });
              this.load();
            } else {
              this.toast.add({ severity: 'error', summary: 'Action impossible', detail: r.message ?? 'Une erreur est survenue.' });
            }
          },
          error: () => this.toast.add({ severity: 'error', summary: 'Action impossible', detail: 'Une erreur est survenue.' })
        });
      }
    });
  }
}
