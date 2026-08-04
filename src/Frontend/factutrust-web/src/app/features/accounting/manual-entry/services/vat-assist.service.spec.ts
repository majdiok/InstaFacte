import { TestBed } from '@angular/core/testing';
import { VatAssistService } from './vat-assist.service';
import { ChartOfAccountDto } from '../../services/accounting.service';

const mockAccounts: ChartOfAccountDto[] = [
  { id: '1', accountNumber: '43666', label: 'TVA déductible', accountClass: 4, natureType: 0, isSystem: true, isActive: true, level: 3 },
  { id: '2', accountNumber: '43662', label: 'TVA déductible immo', accountClass: 4, natureType: 0, isSystem: true, isActive: true, level: 3 },
  { id: '3', accountNumber: '436711', label: 'TVA collectée', accountClass: 4, natureType: 1, isSystem: true, isActive: true, level: 4 },
  { id: '4', accountNumber: '211', label: 'Immobilisation', accountClass: 2, natureType: 0, isSystem: false, isActive: true, level: 3 }
];

describe('VatAssistService', () => {
  let service: VatAssistService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [VatAssistService] });
    service = TestBed.inject(VatAssistService);
  });

  it('resolves deductible account 43666 for charges', () => {
    const r = service.resolveVatAccount('deductible', '607', mockAccounts);
    expect(r.account).toBe('43666');
  });

  it('resolves 43662 for class 2 counterpart', () => {
    const r = service.resolveVatAccount('deductible', '211', mockAccounts);
    expect(r.account).toBe('43662');
  });

  it('resolves collected account 436711', () => {
    const r = service.resolveVatAccount('collected', '707', mockAccounts);
    expect(r.account).toBe('436711');
  });

  it('computeVatAmount rounds correctly', () => {
    expect(service.computeVatAmount(1000, 19)).toBe(190);
  });

  it('returns warning when account missing', () => {
    const r = service.resolveVatAccount('collected', null, []);
    expect(r.account).toBeNull();
    expect(r.warning).toBeTruthy();
  });
});
