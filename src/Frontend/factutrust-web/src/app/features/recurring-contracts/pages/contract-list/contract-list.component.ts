import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { RecurringContractListItem, RecurringContractService } from '@core/services/recurring-contract.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';

@Component({
  selector: 'app-contract-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageHeaderComponent, ButtonComponent, EmptyStateComponent],
  template: `
    <app-page-header
      title="Contrats récurrents"
      subtitle="Abonnements et facturation périodique B2B">
      <div class="actions">
        <app-button variant="outline" icon="pi-file-edit" routerLink="pending-drafts">
          Brouillons à valider
        </app-button>
        @if (canCreate()) {
          <app-button variant="primary" icon="pi-plus" routerLink="new">Nouveau contrat</app-button>
        }
      </div>
    </app-page-header>

    <div class="ft-card filters">
      <input class="ft-input" [(ngModel)]="search" (ngModelChange)="load()" placeholder="Rechercher..." />
      <select class="ft-input" [(ngModel)]="statusFilter" (ngModelChange)="load()">
        <option [ngValue]="null">Tous les statuts</option>
        <option [ngValue]="0">Brouillon</option>
        <option [ngValue]="1">Actif</option>
        <option [ngValue]="2">Suspendu</option>
        <option [ngValue]="3">Résilié</option>
      </select>
    </div>

    @if (loading()) {
      <p>Chargement...</p>
    } @else if (items().length === 0) {
      <app-empty-state title="Aucun contrat" message="Créez votre premier contrat récurrent."></app-empty-state>
    } @else {
      <div class="ft-card table-wrap">
        <table class="ft-table">
          <thead>
            <tr>
              <th>Numéro</th>
              <th>Client</th>
              <th>Statut</th>
              <th>Périodicité</th>
              <th>Prochaine facturation</th>
              <th>Montant estimé/mois</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.id) {
              <tr>
                <td>{{ item.number || '—' }}</td>
                <td>{{ item.clientName }}</td>
                <td><span class="badge">{{ item.statusDisplay }}</span></td>
                <td>{{ item.billingFrequencyDisplay }}</td>
                <td>{{ item.nextBillingDate ? (item.nextBillingDate | date:'dd/MM/yyyy') : '—' }}</td>
                <td>{{ item.estimatedMonthlyAmount | number:'1.3-3' }} {{ item.currency }}</td>
                <td><a [routerLink]="[item.id]">Voir</a></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .actions { display: flex; gap: .5rem; flex-wrap: wrap; }
    .filters { display: flex; gap: 1rem; margin-bottom: 1rem; padding: 1rem; }
    .filters .ft-input { flex: 1; min-width: 200px; }
    .table-wrap { overflow-x: auto; }
    .badge { padding: .2rem .5rem; border-radius: 4px; background: var(--surface-100); font-size: .85rem; }
  `]
})
export class ContractListComponent implements OnInit {
  private readonly service = inject(RecurringContractService);
  private readonly auth = inject(AuthService);

  readonly items = signal<RecurringContractListItem[]>([]);
  readonly loading = signal(true);
  search = '';
  statusFilter: number | null = null;

  ngOnInit(): void { this.load(); }

  canCreate(): boolean {
    return this.auth.hasPermission(PERMISSIONS.recurringContracts.create);
  }

  load(): void {
    this.loading.set(true);
    this.service.list({
      search: this.search || undefined,
      status: this.statusFilter ?? undefined,
      page: 1,
      pageSize: 50
    }).subscribe({
      next: res => {
        this.items.set(res.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
