import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { EntryReferenceStore } from './entry-reference.store';
import { AccountingService } from '../../services/accounting.service';
import { TaxService } from '@core/services/tax.service';
import { BankAccountService } from '@core/services/bank-account.service';
import { ClientService } from '@core/services/client.service';
import { SupplierService } from '@core/services/supplier.service';
import { VALID_VAT_RATES } from '@shared/validation/validation-rules';
import { ApiResponse } from '@core/services/auth.service';
import { VatRateOption } from '@core/services/tax.service';

function mockVatResponse(data: VatRateOption[]): ApiResponse<VatRateOption[]> {
  return { success: true, data, message: null, errors: [] };
}

describe('EntryReferenceStore', () => {
  let store: EntryReferenceStore;
  let taxService: jasmine.SpyObj<Pick<TaxService, 'getVatRates'>>;

  const accountingMock = {
    getChartOfAccounts: () => of({ success: true, data: [{ accountNumber: '607', label: 'Achats', isActive: true }] }),
    getPeriods: () => of({ success: true, data: [] }),
    getJournals: () => of({ success: true, data: [] })
  };

  beforeEach(() => {
    taxService = jasmine.createSpyObj('TaxService', ['getVatRates']);

    TestBed.configureTestingModule({
      providers: [
        EntryReferenceStore,
        { provide: AccountingService, useValue: accountingMock },
        { provide: TaxService, useValue: taxService },
        { provide: BankAccountService, useValue: { list: () => of({ success: true, data: [] }) } },
        { provide: ClientService, useValue: { getClients: () => of({ success: true, data: { items: [] } }) } },
        { provide: SupplierService, useValue: { getSuppliers: () => of({ success: true, data: { items: [] } }) } }
      ]
    });

    store = TestBed.inject(EntryReferenceStore);
  });

  it('loads VAT rates from API when available', () => {
    const apiRates = [{ id: 'a1', percent: 19, label: '19%' }];
    taxService.getVatRates.and.returnValue(of(mockVatResponse(apiRates)));

    store.loadAll();

    expect(taxService.getVatRates).toHaveBeenCalledWith({ skipGlobalErrorUi: true });
    expect(store.vatRates()).toEqual(apiRates);
    expect(store.vatRatesFromFallback()).toBe(false);
    expect(store.loading()).toBe(false);
  });

  it('uses fallback VAT rates when API returns empty data', () => {
    taxService.getVatRates.and.returnValue(of(mockVatResponse([])));

    store.loadAll();

    expect(store.vatRates().map(r => r.percent)).toEqual([...VALID_VAT_RATES]);
    expect(store.vatRatesFromFallback()).toBe(true);
  });

  it('uses fallback VAT rates when API fails', () => {
    taxService.getVatRates.and.returnValue(throwError(() => new Error('403')));

    store.loadAll();

    expect(store.vatRates().map(r => r.percent)).toEqual([...VALID_VAT_RATES]);
    expect(store.vatRatesFromFallback()).toBe(true);
    expect(store.loading()).toBe(false);
  });

  it('loads accounts even when VAT API returns null', () => {
    taxService.getVatRates.and.returnValue(of({ success: false, data: [], message: null, errors: [] }));

    store.loadAll();

    expect(store.accounts().length).toBe(1);
    expect(store.accounts()[0].accountNumber).toBe('607');
    expect(store.loading()).toBe(false);
  });
});
