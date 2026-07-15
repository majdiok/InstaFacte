import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import {
  normalizePaymentMethod,
  normalizeCashOperationType,
  normalizeCashOperationStatus,
  CashOperationType,
  CashOperationStatus,
  CashDeskService
} from './cash-desk.service';
import { environment } from '@environments/environment';

describe('cash-desk API normalization', () => {
  describe('normalizePaymentMethod', () => {
    it('should pass through numeric values', () => {
      expect(normalizePaymentMethod(0)).toBe(0);
      expect(normalizePaymentMethod(2)).toBe(2);
      expect(normalizePaymentMethod(99)).toBe(99);
    });

    it('should map camelCase JSON enum strings from API', () => {
      expect(normalizePaymentMethod('cash')).toBe(0);
      expect(normalizePaymentMethod('bankTransfer')).toBe(1);
      expect(normalizePaymentMethod('check')).toBe(2);
      expect(normalizePaymentMethod('card')).toBe(3);
      expect(normalizePaymentMethod('mobilePayment')).toBe(4);
      expect(normalizePaymentMethod('other')).toBe(99);
    });

    it('should map PascalCase strings', () => {
      expect(normalizePaymentMethod('Cash')).toBe(0);
      expect(normalizePaymentMethod('BankTransfer')).toBe(1);
      expect(normalizePaymentMethod('Check')).toBe(2);
    });

    it('should parse numeric strings', () => {
      expect(normalizePaymentMethod('2')).toBe(2);
    });
  });

  describe('normalizeCashOperationType', () => {
    it('should map debit/credit strings', () => {
      expect(normalizeCashOperationType('debit')).toBe(CashOperationType.Debit);
      expect(normalizeCashOperationType('credit')).toBe(CashOperationType.Credit);
    });
  });

  describe('normalizeCashOperationStatus', () => {
    it('should map terminee/annulee strings', () => {
      expect(normalizeCashOperationStatus('terminee')).toBe(CashOperationStatus.Terminee);
      expect(normalizeCashOperationStatus('annulee')).toBe(CashOperationStatus.Annulee);
    });
  });
});

describe('CashDeskService cache invalidation', () => {
  let service: CashDeskService;
  let httpMock: HttpTestingController;

  const balancesBody = () => ({
    success: true,
    data: {
      currency: 'TND',
      primary: [],
      secondary: [],
      primaryTotal: 0,
      secondaryTotal: 0,
      primaryCreditsTotal: 0,
      primaryDebitsTotal: 0,
      secondaryCreditsTotal: 0,
      secondaryDebitsTotal: 0
    }
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [CashDeskService]
    });
    service = TestBed.inject(CashDeskService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should issue a second HTTP GET for balances after invalidateCachesAfterCashLedgerMutation', () => {
    const base = `${environment.apiUrl}/cash-desk/balances`;

    service.getBalances(2026, 4).subscribe();
    const first = httpMock.expectOne(
      req => req.method === 'GET' && req.url === base && req.params.get('year') === '2026' && req.params.get('month') === '4'
    );
    expect(first.request.method).toBe('GET');
    first.flush(balancesBody());

    service.invalidateCachesAfterCashLedgerMutation('2026-03-15');

    service.getBalances(2026, 4).subscribe();
    const second = httpMock.expectOne(
      req => req.method === 'GET' && req.url === base && req.params.get('year') === '2026' && req.params.get('month') === '4'
    );
    expect(second).toBeTruthy();
    second.flush(balancesBody());
  });

  it('should invalidate March balance cache when mutation is in March', () => {
    const base = `${environment.apiUrl}/cash-desk/balances`;

    service.getBalances(2026, 3).subscribe();
    const firstMarch = httpMock.expectOne(
      req => req.method === 'GET' && req.url === base && req.params.get('year') === '2026' && req.params.get('month') === '3'
    );
    expect(firstMarch.request.method).toBe('GET');
    firstMarch.flush(balancesBody());

    service.invalidateCachesAfterCashLedgerMutation('2026-03-01');

    service.getBalances(2026, 3).subscribe();
    const secondMarch = httpMock.expectOne(
      req => req.method === 'GET' && req.url === base && req.params.get('year') === '2026' && req.params.get('month') === '3'
    );
    expect(secondMarch).toBeTruthy();
    secondMarch.flush(balancesBody());
  });

  it('should ignore invalid operationDateIso and keep balance cache', () => {
    const base = `${environment.apiUrl}/cash-desk/balances`;

    service.getBalances(2026, 4).subscribe();
    httpMock.expectOne(req => req.url === base).flush(balancesBody());

    service.invalidateCachesAfterCashLedgerMutation('');
    service.invalidateCachesAfterCashLedgerMutation('not-a-date');

    let replayCount = 0;
    service.getBalances(2026, 4).subscribe(() => replayCount++);
    expect(replayCount).toBe(1);
  });
});
