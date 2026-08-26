import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { ContractKpiVm } from '../contract-detail.vm';
import { formatContractAmount } from '../recurring-contracts.ui-utils';

/** Bande de 5 cartes KPI de la fiche contrat (maquette détail). */
@Component({
  selector: 'app-contract-kpi-strip',
  standalone: true,
  imports: [CommonModule, StatCardComponent],
  template: `
    <div class="kpi-grid">
      <app-stat-card
        label="Montant du contrat"
        [value]="formatContractAmount(kpi.contractTotalHT, currency)"
        icon="pi-wallet"
        variant="primary"
        tone="primary">
        @if (kpi.monthlyEquivalentHT != null) {
          <span class="kpi-sub">≈ {{ formatContractAmount(kpi.monthlyEquivalentHT, currency) }}/mois</span>
        }
      </app-stat-card>

      <app-stat-card
        label="Prochaine échéance"
        [value]="kpi.nextBillingDate ? ((kpi.nextBillingDate | date:'dd/MM/yyyy') ?? '—') : '—'"
        icon="pi-calendar"
        variant="primary"
        tone="violet">
        @if (kpi.daysUntilNext != null) {
          @if (kpi.daysUntilNext >= 0) {
            <span class="kpi-sub">Dans {{ kpi.daysUntilNext }} j</span>
          } @else {
            <span class="kpi-sub kpi-sub--late">En retard de {{ -kpi.daysUntilNext }} j</span>
          }
        }
      </app-stat-card>

      <app-stat-card
        label="Fréquence"
        [value]="kpi.frequencyLabel || '—'"
        icon="pi-sync"
        variant="primary"
        tone="teal">
        <span class="kpi-sub">Le {{ kpi.billingDayOfMonth }} du mois</span>
      </app-stat-card>

      <app-stat-card
        label="Durée"
        [value]="kpi.durationMonths != null ? kpi.durationMonths + ' mois' : 'Indéterminée'"
        icon="pi-hourglass"
        variant="primary"
        tone="orange">
        <span class="kpi-sub">{{ kpi.periodLabel }}</span>
      </app-stat-card>

      <app-stat-card
        label="Renouvellement"
        [value]="kpi.autoRenew ? 'Automatique' : 'Manuel'"
        icon="pi-refresh"
        [variant]="kpi.autoRenew ? 'success' : 'warning'">
        <span class="kpi-sub">Préavis {{ kpi.noticePeriodDays }} j</span>
      </app-stat-card>
    </div>
  `,
  styles: [`
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: var(--spacing-4);
      margin-bottom: var(--spacing-5);
    }

    .kpi-sub {
      display: block;
      margin-top: var(--spacing-1);
      font-size: var(--font-size-xs);
      color: var(--color-text-secondary);
    }

    .kpi-sub--late { color: var(--color-error-600, #dc2626); }
  `]
})
export class ContractKpiStripComponent {
  @Input({ required: true }) kpi!: ContractKpiVm;
  @Input() currency = 'TND';

  protected readonly formatContractAmount = formatContractAmount;
}
