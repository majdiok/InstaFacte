import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AccountingService } from './accounting.service';
import { environment } from '@environments/environment';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('AccountingService', () => {
  let service: AccountingService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/accounting`;

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
});
    service = TestBed.inject(AccountingService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getChartOfAccounts', () => {
    it('should call GET chart-of-accounts', () => {
      service.getChartOfAccounts().subscribe(res => {
        expect(res.success).toBe(true);
        expect(res.data!.length).toBe(1);
      });

      const req = httpMock.expectOne(`${base}/chart-of-accounts`);
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: [{ id: '1', accountNumber: '4111', label: 'Clients', accountClass: 4, isSystem: true, isActive: true, level: 4 }] });
    });
  });

  describe('getJournal', () => {
    it('should call GET journal with date params', () => {
      const from = new Date(2026, 0, 1);
      const to = new Date(2026, 2, 31);

      service.getJournal(undefined, from, to).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/journal`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('from')).toBe('2026-01-01');
      expect(req.request.params.get('to')).toBe('2026-03-31');
      expect(req.request.params.has('journalCode')).toBe(false);
      req.flush({ success: true, data: [] });
    });

    it('should pass journalCode when provided', () => {
      const from = new Date(2026, 0, 1);
      const to = new Date(2026, 2, 31);

      service.getJournal('JV', from, to).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/journal`);
      expect(req.request.params.get('journalCode')).toBe('JV');
      req.flush({ success: true, data: [] });
    });
  });

  describe('getLedger', () => {
    it('should call GET ledger with account and dates', () => {
      service.getLedger('4111', new Date(2026, 0, 1), new Date(2026, 2, 31)).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/ledger`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('accountNumber')).toBe('4111');
      req.flush({ success: true, data: [] });
    });
  });

  describe('getBalance', () => {
    it('should call GET balance with date params', () => {
      service.getBalance(new Date(2026, 0, 1), new Date(2026, 2, 31)).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/balance`);
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: [] });
    });
  });

  describe('getClientAging', () => {
    it('should call GET aging/clients', () => {
      service.getClientAging().subscribe();

      const req = httpMock.expectOne(`${base}/aging/clients`);
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: [] });
    });
  });

  describe('getSupplierAging', () => {
    it('should call GET aging/suppliers', () => {
      service.getSupplierAging().subscribe();

      const req = httpMock.expectOne(`${base}/aging/suppliers`);
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: [] });
    });
  });

  describe('getVatDeclaration', () => {
    it('should call GET vat-declaration with year and month', () => {
      service.getVatDeclaration(2026, 3).subscribe(res => {
        expect(res.data?.filingDeadline).toBe('2026-04-22');
        expect(res.data?.collectedVatBreakdown?.length).toBe(1);
        expect(res.data?.companyName).toBe('Ma Société SARL');
        expect(res.data?.nif).toBe('1234567/A/B/C/000');
      });

      const req = httpMock.expectOne(r => r.url === `${base}/vat-declaration`);
      expect(req.request.params.get('year')).toBe('2026');
      expect(req.request.params.get('month')).toBe('3');
      req.flush({
        success: true,
        data: {
          year: 2026,
          month: 3,
          filingDeadline: '2026-04-22',
          declarationTypeDisplay: 'Déclaration mensuelle unique',
          collectedVatBreakdown: [{ ratePercent: 19, taxableBase: 1000, vatAmount: 190 }],
          deductiblePurchasesTaxableBase: 0,
          salesTaxableBase: 1000,
          salesGrossBase: 1190,
          companyName: 'Ma Société SARL',
          nif: '1234567/A/B/C/000',
          taxRegimeDisplay: 'Régime réel'
        }
      });
    });
  });

  describe('saveVatDeclaration', () => {
    it('should call POST vat-declaration', () => {
      service.saveVatDeclaration(2026, 3, false).subscribe();

      const req = httpMock.expectOne(`${base}/vat-declaration`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ year: 2026, month: 3, submit: false });
      req.flush({ success: true, data: 'new-id' });
    });
  });

  describe('closePeriod', () => {
    it('should call POST periods/:id/close', () => {
      const id = 'some-guid';
      service.closePeriod(id).subscribe();

      const req = httpMock.expectOne(`${base}/periods/${id}/close`);
      expect(req.request.method).toBe('POST');
      req.flush({ success: true, data: true });
    });
  });

  describe('reopenPeriod', () => {
    it('should call POST periods/:id/reopen', () => {
      const id = 'some-guid';
      service.reopenPeriod(id).subscribe();

      const req = httpMock.expectOne(`${base}/periods/${id}/reopen`);
      expect(req.request.method).toBe('POST');
      req.flush({ success: true, data: true });
    });
  });

  describe('closeAnnualYear', () => {
    it('should call POST periods/close-year/:year', () => {
      service.closeAnnualYear(2026).subscribe();

      const req = httpMock.expectOne(`${base}/periods/close-year/2026`);
      expect(req.request.method).toBe('POST');
      req.flush({ success: true, data: true });
    });
  });

  describe('createManualJournalEntry', () => {
    it('should call POST journal with entry data', () => {
      const request = {
        journalCode: 'JOD',
        entryDate: '2026-03-15',
        label: 'Test',
        lines: [
          { accountNumber: '4111', lineLabel: 'Debit', debit: 100, credit: 0 },
          { accountNumber: '707', lineLabel: 'Credit', debit: 0, credit: 100 }
        ]
      };

      service.createManualJournalEntry(request).subscribe();

      const req = httpMock.expectOne(`${base}/journal`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.journalCode).toBe('JOD');
      expect(req.request.body.lines.length).toBe(2);
      req.flush({ success: true, data: 'new-id' });
    });
  });

  describe('letterEntries', () => {
    it('should call POST letter with line IDs', () => {
      const ids = ['id1', 'id2'];
      service.letterEntries(ids).subscribe();

      const req = httpMock.expectOne(`${base}/letter`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ journalEntryLineIds: ids, allowPartial: false });
      req.flush({ success: true, data: true });
    });
  });

  describe('getBudgetPosts', () => {
    it('should call GET budget-posts with includeInactive param', () => {
      service.getBudgetPosts(true).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/budget-posts`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('includeInactive')).toBe('true');
      req.flush({ success: true, data: [] });
    });
  });

  describe('createBudgetPost / updateBudgetPost / toggleBudgetPost', () => {
    it('should POST a new budget post', () => {
      service.createBudgetPost({ code: '61', label: 'Services extérieurs', kind: 0, accountPrefixes: '61;62', displayOrder: 20 }).subscribe();

      const req = httpMock.expectOne(`${base}/budget-posts`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.accountPrefixes).toBe('61;62');
      req.flush({ success: true, data: 'post-id' });
    });

    it('should PUT an update and PATCH a toggle', () => {
      service.updateBudgetPost('p1', { label: 'X', kind: 0, accountPrefixes: '61', displayOrder: 10 }).subscribe();
      httpMock.expectOne(`${base}/budget-posts/p1`).flush({ success: true, data: true });

      service.toggleBudgetPost('p1').subscribe();
      const toggle = httpMock.expectOne(`${base}/budget-posts/p1/toggle`);
      expect(toggle.request.method).toBe('PATCH');
      toggle.flush({ success: true, data: true });
    });
  });

  describe('getBudgetYear / saveBudgetYear / validateInitialBudget', () => {
    it('should GET the budget grid for a fiscal year', () => {
      service.getBudgetYear(2026).subscribe();

      const req = httpMock.expectOne(`${base}/budgets/2026`);
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: { fiscalYear: 2026, status: 0, editableVersion: 0, rows: [] } });
    });

    it('should PUT the editable version lines', () => {
      service.saveBudgetYear(2026, [{ budgetPostId: 'p1', month: 1, amount: 1000 }]).subscribe();

      const req = httpMock.expectOne(`${base}/budgets/2026`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.lines.length).toBe(1);
      expect(req.request.body.lines[0].month).toBe(1);
      req.flush({ success: true, data: true });
    });

    it('should POST validate-initial', () => {
      service.validateInitialBudget(2026).subscribe();

      const req = httpMock.expectOne(`${base}/budgets/2026/validate-initial`);
      expect(req.request.method).toBe('POST');
      req.flush({ success: true, data: true });
    });
  });

  describe('getBudgetReport / exportBudgetReport', () => {
    it('should GET the report with fiscalYear and throughMonth', () => {
      service.getBudgetReport(2026, 6).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/budget-report`);
      expect(req.request.params.get('fiscalYear')).toBe('2026');
      expect(req.request.params.get('throughMonth')).toBe('6');
      req.flush({ success: true, data: { fiscalYear: 2026, throughMonth: 6, isValidated: false, rows: [], totals: {} } });
    });

    it('should download the export as blob with format param', () => {
      service.exportBudgetReport(2026, null, 'excel').subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/budget-report/export`);
      expect(req.request.params.get('format')).toBe('excel');
      expect(req.request.responseType).toBe('blob');
      req.flush(new Blob(['x']));
    });
  });

  describe('getThirdPartyDirectory', () => {
    it('should GET third-parties with defaults and omit empty filters', () => {
      service.getThirdPartyDirectory({}).subscribe(res => {
        expect(res.data!.totalCount).toBe(0);
      });

      const req = httpMock.expectOne(r => r.url === `${base}/third-parties`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('includeInactive')).toBe('false');
      expect(req.request.params.get('page')).toBe('1');
      expect(req.request.params.get('pageSize')).toBe('25');
      expect(req.request.params.has('kind')).toBe(false);
      expect(req.request.params.has('search')).toBe(false);
      req.flush({ success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 25 } });
    });

    it('should pass kind, trimmed search and includeInactive', () => {
      service.getThirdPartyDirectory({ kind: 2, search: '  beta ', includeInactive: true, page: 3, pageSize: 50 }).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/third-parties`);
      expect(req.request.params.get('kind')).toBe('2');
      expect(req.request.params.get('search')).toBe('beta');
      expect(req.request.params.get('includeInactive')).toBe('true');
      expect(req.request.params.get('page')).toBe('3');
      expect(req.request.params.get('pageSize')).toBe('50');
      req.flush({ success: true, data: { items: [], totalCount: 0, page: 3, pageSize: 50 } });
    });
  });

  describe('getThirdPartyProfile / upsertThirdPartyProfile', () => {
    it('should GET the profile of a third party', () => {
      service.getThirdPartyProfile(1, 'abc').subscribe(res => {
        expect(res.data!.collectiveAccountNumber).toBe('4111');
      });

      const req = httpMock.expectOne(`${base}/third-parties/1/abc/profile`);
      expect(req.request.method).toBe('GET');
      req.flush({
        success: true,
        data: { kind: 1, thirdPartyId: 'abc', thirdPartyName: 'Alpha', collectiveAccountNumber: '4111', hasProfile: false }
      });
    });

    it('should PUT the profile with the request body', () => {
      service.upsertThirdPartyProfile(2, 'def', {
        auxiliaryCode: 'F0001', collectiveAccountNumber: '4011', paymentTermDays: 60
      }).subscribe();

      const req = httpMock.expectOne(`${base}/third-parties/2/def/profile`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.auxiliaryCode).toBe('F0001');
      expect(req.request.body.paymentTermDays).toBe(60);
      req.flush({ success: true, data: true });
    });
  });

  describe('ensureAuxiliaryCodes', () => {
    it('should POST ensure-codes and return the created count', () => {
      service.ensureAuxiliaryCodes().subscribe(res => {
        expect(res.data).toBe(3);
      });

      const req = httpMock.expectOne(`${base}/third-parties/ensure-codes`);
      expect(req.request.method).toBe('POST');
      req.flush({ success: true, data: 3 });
    });
  });

  describe('exports (csv / excel / pdf)', () => {
    const from = new Date(2026, 0, 1);
    const to = new Date(2026, 2, 31);
    const blob = new Blob(['x'], { type: 'application/octet-stream' });

    it('exportJournal should GET journal/export with format and blob responseType', () => {
      service.exportJournal('JV', from, to, 'pdf').subscribe(b => expect(b).toBeTruthy());
      const req = httpMock.expectOne(r => r.url === `${base}/journal/export`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('format')).toBe('pdf');
      expect(req.request.params.get('journalCode')).toBe('JV');
      expect(req.request.responseType).toBe('blob');
      req.flush(blob);
    });

    it('exportLedger should GET ledger/export with account and excel format', () => {
      service.exportLedger('4111', from, to, 'excel').subscribe();
      const req = httpMock.expectOne(r => r.url === `${base}/ledger/export`);
      expect(req.request.params.get('accountNumber')).toBe('4111');
      expect(req.request.params.get('format')).toBe('excel');
      expect(req.request.responseType).toBe('blob');
      req.flush(blob);
    });

    it('exportBalance should GET balance/export with csv format', () => {
      service.exportBalance(from, to, 'csv').subscribe();
      const req = httpMock.expectOne(r => r.url === `${base}/balance/export`);
      expect(req.request.params.get('format')).toBe('csv');
      req.flush(blob);
    });

    it('exportAuxiliaryBalance should include kind and format', () => {
      service.exportAuxiliaryBalance(2, from, to, 'excel').subscribe();
      const req = httpMock.expectOne(r => r.url === `${base}/auxiliary-balance/export`);
      expect(req.request.params.get('kind')).toBe('2');
      expect(req.request.params.get('format')).toBe('excel');
      req.flush(blob);
    });

    it('exportClientAging should GET aging/clients/export with format', () => {
      service.exportClientAging('pdf').subscribe();
      const req = httpMock.expectOne(r => r.url === `${base}/aging/clients/export`);
      expect(req.request.params.get('format')).toBe('pdf');
      expect(req.request.responseType).toBe('blob');
      req.flush(blob);
    });

    it('exportBalanceSheet should GET balance-sheet/export with fiscalYear and format', () => {
      service.exportBalanceSheet(2025, 'pdf').subscribe();
      const req = httpMock.expectOne(r => r.url === `${base}/balance-sheet/export`);
      expect(req.request.params.get('fiscalYear')).toBe('2025');
      expect(req.request.params.get('format')).toBe('pdf');
      req.flush(blob);
    });

    it('exportIncomeStatement should GET income-statement/export with fiscalYear and format', () => {
      service.exportIncomeStatement(2025, 'excel').subscribe();
      const req = httpMock.expectOne(r => r.url === `${base}/income-statement/export`);
      expect(req.request.params.get('fiscalYear')).toBe('2025');
      expect(req.request.params.get('format')).toBe('excel');
      req.flush(blob);
    });
  });
});
