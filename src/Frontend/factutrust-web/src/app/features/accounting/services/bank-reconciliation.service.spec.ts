import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { BankReconciliationService, BankStatementExtractionMethod, BankStatementFileFormat, normalizeExtractionMethod } from './bank-reconciliation.service';
import { environment } from '@environments/environment';

describe('BankReconciliationService', () => {
  let service: BankReconciliationService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/bank-reconciliation`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(BankReconciliationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getStatements', () => {
    it('should call GET statements without params when no filters', () => {
      service.getStatements().subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/statements`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.keys().length).toBe(0);
      req.flush({ success: true, data: [] });
    });

    it('should pass account and date filters', () => {
      service.getStatements('08 123', new Date(2026, 4, 1), new Date(2026, 4, 31)).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/statements`);
      expect(req.request.params.get('accountNumber')).toBe('08 123');
      expect(req.request.params.get('from')).toBe('2026-05-01');
      expect(req.request.params.get('to')).toBe('2026-05-31');
      req.flush({ success: true, data: [] });
    });
  });

  describe('normalizeExtractionMethod', () => {
    it('should map camelCase API strings to enum values', () => {
      expect(normalizeExtractionMethod('textParser')).toBe(BankStatementExtractionMethod.TextParser);
      expect(normalizeExtractionMethod('ocrLlm')).toBe(BankStatementExtractionMethod.OcrLlm);
      expect(normalizeExtractionMethod(1)).toBe(BankStatementExtractionMethod.TextParser);
    });
  });

  describe('previewStatementFile', () => {
    it('should POST multipart form with file and format', () => {
      const file = new File(['date,libelle,montant\n2026-05-03,Virement,100\n'], 'releve.csv', { type: 'text/csv' });

      service.previewStatementFile(file, BankStatementFileFormat.Csv).subscribe();

      const req = httpMock.expectOne(`${base}/statements/import-file`);
      expect(req.request.method).toBe('POST');
      const form = req.request.body as FormData;
      expect(form.get('file')).toBe(file);
      expect(form.get('format')).toBe('0');
      req.flush({ success: true, data: { totalLines: 1, validLines: 1, totalDebit: 0, totalCredit: 100, canImport: true, issues: [], lines: [] } });
    });

    it('should POST PDF format as enum value 2', () => {
      const file = new File(['%PDF'], 'releve.pdf', { type: 'application/pdf' });

      service.previewStatementFile(file, BankStatementFileFormat.Pdf).subscribe();

      const req = httpMock.expectOne(`${base}/statements/import-file`);
      const form = req.request.body as FormData;
      expect(form.get('format')).toBe('2');
      req.flush({
        success: true,
        data: {
          totalLines: 1,
          validLines: 1,
          totalDebit: 0,
          totalCredit: 100,
          canImport: true,
          issues: [],
          lines: [],
          extractionMethod: 0,
          chartOfAccountNumber: '5321001'
        }
      });
    });
  });

  describe('importStatement', () => {
    it('should POST statement payload', () => {
      service.importStatement({
        bankName: 'BIAT',
        accountNumber: '08 123',
        statementDate: '2026-05-31',
        periodStart: '2026-05-01',
        periodEnd: '2026-05-31',
        openingBalance: 1000,
        closingBalance: 1100,
        lines: [{ transactionDate: '2026-05-03', reference: '', description: 'Virement', amount: 100, isDebit: false }]
      }).subscribe();

      const req = httpMock.expectOne(`${base}/statements`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.bankName).toBe('BIAT');
      expect(req.request.body.lines.length).toBe(1);
      req.flush({ success: true, data: { id: 's1', lines: [] } });
    });
  });

  describe('reconcileLine / unreconcileLine', () => {
    it('should POST reconcile with both ids', () => {
      service.reconcileLine({ bankStatementLineId: 'b1', journalEntryLineId: 'j1' }).subscribe();

      const req = httpMock.expectOne(`${base}/reconcile`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.bankStatementLineId).toBe('b1');
      expect(req.request.body.journalEntryLineId).toBe('j1');
      req.flush({ success: true, data: true });
    });

    it('should POST unreconcile with line id in url', () => {
      service.unreconcileLine('b1').subscribe();

      const req = httpMock.expectOne(`${base}/unreconcile/b1`);
      expect(req.request.method).toBe('POST');
      req.flush({ success: true, data: true });
    });
  });

  describe('importStatement with skipAlreadyImported', () => {
    it('should include the dedup flag in the payload', () => {
      service.importStatement({
        bankName: 'BIAT',
        accountNumber: '08 123',
        statementDate: '2026-05-31',
        periodStart: '2026-05-01',
        periodEnd: '2026-05-31',
        openingBalance: 1000,
        closingBalance: 1100,
        skipAlreadyImported: true,
        lines: []
      }).subscribe();

      const req = httpMock.expectOne(`${base}/statements`);
      expect(req.request.body.skipAlreadyImported).toBe(true);
      req.flush({ success: true, data: { id: 's1', lines: [], skippedDuplicateCount: 0 } });
    });
  });

  describe('autoAssociate', () => {
    it('should POST to the auto-associate endpoint', () => {
      service.autoAssociate('s1').subscribe();

      const req = httpMock.expectOne(`${base}/statements/s1/auto-associate`);
      expect(req.request.method).toBe('POST');
      req.flush({ success: true, data: { associations: [], summary: {} } });
    });
  });

  describe('applyAssociations', () => {
    it('should POST the confirmed pairs', () => {
      service.applyAssociations('s1', [{ bankStatementLineId: 'b1', journalEntryLineId: 'j1' }]).subscribe();

      const req = httpMock.expectOne(`${base}/statements/s1/apply-associations`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.pairs.length).toBe(1);
      expect(req.request.body.pairs[0].bankStatementLineId).toBe('b1');
      req.flush({ success: true, data: { appliedCount: 1, failures: [] } });
    });
  });

  describe('createEntryForLine', () => {
    it('should POST the counterparty + journal to the create-entry endpoint', () => {
      service.createEntryForLine('s1', 'b1', { journalCode: 'BQ', counterpartyAccount: '6580000' }).subscribe();

      const req = httpMock.expectOne(`${base}/statements/s1/lines/b1/create-entry`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.counterpartyAccount).toBe('6580000');
      expect(req.request.body.journalCode).toBe('BQ');
      req.flush({ success: true, data: true });
    });
  });
});
