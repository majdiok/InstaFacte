import { ContractEvolutionPoint, ContractFinancialSummary, RecurringContractDetail } from '@core/services/recurring-contract.service';
import {
  buildKpiVm,
  daysUntil,
  donutChartData,
  durationMonths,
  evolutionChartData,
  evolutionMonthLabel,
  fixedMonthlyEstimate,
  renewalDeadlineOf
} from './contract-detail.vm';

function detail(partial: Partial<RecurringContractDetail> = {}): RecurringContractDetail {
  return {
    id: 'c1',
    number: 'CTR-2026-00042',
    clientId: 'cli-1',
    clientName: 'Ste Lamina',
    status: 'Active',
    statusDisplay: 'Actif',
    billingFrequency: 'Monthly',
    billingFrequencyDisplay: 'Mensuel',
    billingDayOfMonth: 1,
    startDate: '2026-01-01',
    endDate: '2026-12-31',
    nextBillingDate: '2026-09-01',
    autoRenew: true,
    noticePeriodDays: 30,
    currency: 'TND',
    setupFeeBilled: false,
    lines: [],
    ...partial
  };
}

function summary(partial: Partial<ContractFinancialSummary> = {}): ContractFinancialSummary {
  return {
    contractId: 'c1',
    windowFrom: '2026-01-01',
    windowTo: '2026-12-31',
    isOpenEnded: false,
    totalContractAmount: 2400,
    totalInvoicedAmount: 1584,
    remainingAmount: 816,
    percentInvoiced: 66,
    invoicedRunsCount: 8,
    totalRunsCount: 12,
    currency: 'TND',
    ...partial
  };
}

describe('contract-detail.vm', () => {
  describe('daysUntil', () => {
    const today = new Date('2026-08-26T12:00:00');

    it('compte les jours futurs', () => {
      expect(daysUntil('2026-09-01', today)).toBe(6);
    });

    it('retourne 0 le jour même', () => {
      expect(daysUntil('2026-08-26', today)).toBe(0);
    });

    it('retourne une valeur négative pour une date passée', () => {
      expect(daysUntil('2026-08-20', today)).toBe(-6);
    });
  });

  describe('durationMonths', () => {
    it('calcule une année calendaire (01/01 → 31/12 = 12 mois)', () => {
      expect(durationMonths('2026-01-01', '2026-12-31')).toBe(12);
    });

    it('calcule une durée calendaire en cours de mois (15/03 → 14/09 = 6 mois)', () => {
      expect(durationMonths('2026-03-15', '2026-09-14')).toBe(6);
    });

    it('retourne null si la fin précède le début', () => {
      expect(durationMonths('2026-12-31', '2026-01-01')).toBeNull();
    });
  });

  describe('renewalDeadlineOf', () => {
    it('préfère cancellationDeadline du détail enrichi', () => {
      const c = detail({ cancellationDeadline: '2026-11-15' });
      expect(renewalDeadlineOf(c)).toBe('2026-11-15');
    });

    it('déduit endDate − préavis sinon', () => {
      const c = detail({ endDate: '2026-12-31', noticePeriodDays: 30 });
      expect(renewalDeadlineOf(c)).toBe('2026-12-01');
    });

    it('retourne null sans fin ni prochaine facturation', () => {
      const c = detail({ endDate: null, nextBillingDate: null });
      expect(renewalDeadlineOf(c)).toBeNull();
    });
  });

  describe('buildKpiVm', () => {
    it('utilise le total du résumé financier quand il existe', () => {
      const vm = buildKpiVm(detail(), summary());
      expect(vm.contractTotalHT).toBe(2400);
      expect(vm.durationMonths).toBe(12);
      expect(vm.periodLabel).toBe('01/01/2026 → 31/12/2026');
      expect(vm.renewalDeadline).toBe('2026-12-01');
    });

    it('retombe sur estimatedMonthlyAmount puis sur la somme des lignes fixes', () => {
      const enriched = buildKpiVm(detail({ estimatedMonthlyAmount: 200 }), null);
      expect(enriched.monthlyEquivalentHT).toBe(200);

      const local = buildKpiVm(detail({
        lines: [
          { lineType: 'FixedRecurring', description: 'A', quantity: 2, unitPriceHT: 100, vatRate: 19 },
          { lineType: 'OneTimeSetup', description: 'B', quantity: 1, unitPriceHT: 500, vatRate: 19 }
        ]
      }), null);
      expect(local.monthlyEquivalentHT).toBe(200);
    });

    it('normalise la somme des lignes fixes à la fréquence (trimestriel ÷ 3)', () => {
      const vm = buildKpiVm(detail({
        billingFrequency: 'Quarterly',
        lines: [{ lineType: 'FixedRecurring', description: 'A', quantity: 1, unitPriceHT: 900, vatRate: 19 }]
      }), null);
      expect(vm.monthlyEquivalentHT).toBe(300);
    });

    it('gère un contrat sans fin (durée indéterminée)', () => {
      const vm = buildKpiVm(detail({ endDate: null }), null);
      expect(vm.durationMonths).toBeNull();
      expect(vm.periodLabel).toContain('sans fin');
      expect(vm.contractTotalHT).toBeNull();
    });

    it('estime le total depuis le détail enrichi à défaut du résumé', () => {
      const vm = buildKpiVm(detail({ currentPeriodTotalHT: 200, upcomingOccurrencesCount: 4 }), null);
      expect(vm.contractTotalHT).toBe(800);
    });
  });

  describe('fixedMonthlyEstimate', () => {
    it('retourne null sans ligne fixe', () => {
      expect(fixedMonthlyEstimate(detail())).toBeNull();
    });
  });

  describe('donutChartData', () => {
    it('facturé + reste = total du contrat', () => {
      const s = summary();
      const data = donutChartData(s);
      const values = (data.datasets[0] as { data: number[] }).data;
      expect(values[0] + values[1]).toBeCloseTo(s.totalContractAmount, 6);
      expect(data.labels).toEqual(['Déjà facturé', 'Reste à facturer']);
    });
  });

  describe('evolutionChartData', () => {
    it('conserve l’ordre chronologique et dérive les libellés FR', () => {
      const points: ContractEvolutionPoint[] = [
        { month: '2026-03', amount: 1200 },
        { month: '2026-04', amount: 0 }
      ];
      const data = evolutionChartData(points);
      expect(data.labels).toEqual(['mars 2026', 'avril 2026']);
      expect((data.datasets[0] as { data: number[] }).data).toEqual([1200, 0]);
    });

    it('évolutionMonthLabel laisse passer une valeur non parsable', () => {
      expect(evolutionMonthLabel('n/a')).toBe('n/a');
    });
  });
});
