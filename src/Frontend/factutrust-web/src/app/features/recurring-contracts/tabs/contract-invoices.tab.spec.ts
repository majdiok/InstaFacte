import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import {
  LinkedInvoice,
  RecurringContractDetail,
  RecurringContractService
} from '@core/services/recurring-contract.service';
import { ContractInvoicesTabComponent } from './contract-invoices.tab';

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

describe('ContractInvoicesTabComponent', () => {
  let fixture: ComponentFixture<ContractInvoicesTabComponent>;
  let serviceSpy: jasmine.SpyObj<RecurringContractService>;

  async function setup(invoices: LinkedInvoice[] | null): Promise<void> {
    serviceSpy = jasmine.createSpyObj<RecurringContractService>('RecurringContractService', ['getLinkedInvoices']);
    serviceSpy.getLinkedInvoices.and.returnValue(of(invoices));

    await TestBed.configureTestingModule({
      imports: [ContractInvoicesTabComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: RecurringContractService, useValue: serviceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ContractInvoicesTabComponent);
    fixture.componentInstance.contract = detail();
    fixture.detectChanges();
  }

  it('n’affiche plus le bouton « + Nouvelle facture » dans l’en-tête', async () => {
    await setup([]);

    const header = fixture.nativeElement.querySelector('.section-header') as HTMLElement;
    expect(header).toBeTruthy();
    expect(header.querySelector('app-button')).toBeNull();
    expect(header.textContent).not.toContain('Nouvelle facture');
    expect(header.textContent).toContain('Factures liées');
  });

  it('affiche l’état vide quand aucune facture n’est liée', async () => {
    await setup([]);

    expect(serviceSpy.getLinkedInvoices).toHaveBeenCalledWith('c1');
    expect(fixture.nativeElement.textContent).toContain('Aucune facture');
  });
});
