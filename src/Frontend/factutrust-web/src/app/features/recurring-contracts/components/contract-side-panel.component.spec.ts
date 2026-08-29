import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ContractFinancialSummary, RecurringContractDetail } from '@core/services/recurring-contract.service';
import { ContractSidePanelComponent } from './contract-side-panel.component';

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

describe('ContractSidePanelComponent', () => {
  let fixture: ComponentFixture<ContractSidePanelComponent>;
  let component: ContractSidePanelComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ContractSidePanelComponent],
      providers: [provideNoopAnimations()]
    }).compileComponents();

    fixture = TestBed.createComponent(ContractSidePanelComponent);
    component = fixture.componentInstance;
    component.contract = detail();
  });

  describe('donut « Résumé financier » (anti-clignotement)', () => {
    it('garde la même référence de données à travers des cycles de détection de changements répétés', () => {
      component.summary = summary();
      fixture.detectChanges();

      const first = component.donutData;
      expect(first).not.toBeNull();

      // Sans mémoïsation, chaque cycle produirait une nouvelle référence et
      // p-chart (PrimeNG 19) détruirait/recréerait le chart → clignotement.
      fixture.detectChanges();
      fixture.detectChanges();
      fixture.detectChanges();

      expect(component.donutData).toBe(first);
    });

    it('recalcule les données quand un nouvel input summary arrive', () => {
      component.summary = summary();
      fixture.detectChanges();
      const first = component.donutData;

      component.summary = summary({ totalInvoicedAmount: 2400, remainingAmount: 0, percentInvoiced: 100 });
      fixture.detectChanges();

      expect(component.donutData).not.toBe(first);
      expect((component.donutData!.datasets[0] as { data: number[] }).data).toEqual([2400, 0]);
    });

    it('vide les données et affiche l’état vide quand summary est null', () => {
      component.summary = summary();
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.donut-wrap')).toBeTruthy();

      component.summary = null;
      fixture.detectChanges();

      expect(component.donutData).toBeNull();
      expect(fixture.nativeElement.querySelector('.donut-wrap')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('Résumé disponible prochainement');
    });
  });
});
