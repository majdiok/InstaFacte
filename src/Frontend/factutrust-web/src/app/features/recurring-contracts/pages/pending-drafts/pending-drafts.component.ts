import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { PendingRecurringDraft, RecurringContractService } from '@core/services/recurring-contract.service';

@Component({
  selector: 'app-pending-drafts',
  standalone: true,
  imports: [CommonModule, RouterModule, PageHeaderComponent, ButtonComponent, EmptyStateComponent],
  template: `
    <app-page-header
      title="Brouillons récurrents à valider"
      subtitle="Factures générées automatiquement en attente de validation">
      <app-button variant="outline" routerLink="/recurring-contracts">Retour aux contrats</app-button>
    </app-page-header>

    @if (loading()) {
      <p>Chargement...</p>
    } @else if (drafts().length === 0) {
      <app-empty-state title="Aucun brouillon en attente" message="Les prochaines factures apparaîtront ici."></app-empty-state>
    } @else {
      <div class="ft-card">
        <table class="ft-table">
          <thead>
            <tr>
              <th>Contrat</th>
              <th>Client</th>
              <th>Période</th>
              <th>Montant</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (d of drafts(); track d.billingRunId) {
              <tr>
                <td>{{ d.contractNumber || d.recurringContractId }}</td>
                <td>{{ d.clientName }}</td>
                <td>{{ d.periodFrom | date:'dd/MM/yyyy' }} — {{ d.periodTo | date:'dd/MM/yyyy' }}</td>
                <td>{{ d.totalAmount | number:'1.3-3' }} TND</td>
                <td>
                  <a [routerLink]="['/invoices/wizard', d.invoiceDraftId]">Valider dans le wizard</a>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`.ft-card { padding: 1rem; }`]
})
export class PendingDraftsComponent implements OnInit {
  private readonly service = inject(RecurringContractService);
  readonly drafts = signal<PendingRecurringDraft[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.service.listPendingDrafts().subscribe({
      next: items => {
        this.drafts.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
