import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { environment } from '@environments/environment';
import { HonorairesDocumentType, HonorairesInvoiceStatus } from '../models/honoraires-invoice-status';
import { HonorairesService } from './honoraires.service';

describe('HonorairesService', () => {
  let service: HonorairesService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/honoraires`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        HonorairesService,
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(HonorairesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('listInvoices normalizes API string enums to numeric values', () => {
    let result: { status: number; type: number } | undefined;

    service.listInvoices({ type: HonorairesDocumentType.Invoice, page: 1, pageSize: 10 }).subscribe(page => {
      result = page.items[0];
    });

    const req = httpMock.expectOne(r => r.url === `${base}/invoices`);
    req.flush({
      success: true,
      data: {
        items: [
          {
            id: 'inv-1',
            number: 'FAC-2026-000001',
            issueDate: '2026-08-04',
            status: 'Validated',
            statusDisplay: 'Validée',
            type: 'Invoice',
            clientName: 'Ste Fatima',
            firmClientAssignmentId: 'a1',
            totalAmount: 190.4,
            amountDue: 190.4,
            currency: 'TND'
          }
        ],
        totalCount: 1,
        page: 1,
        pageSize: 10
      }
    });

    expect(result?.status).toBe(HonorairesInvoiceStatus.Validated);
    expect(result?.type).toBe(HonorairesDocumentType.Invoice);
  });

  it('getInvoice normalizes API string enums to numeric values', () => {
    let status: number | undefined;
    let type: number | undefined;

    service.getInvoice('inv-1').subscribe(inv => {
      status = inv.status;
      type = inv.type;
    });

    const req = httpMock.expectOne(`${base}/invoices/inv-1`);
    req.flush({
      success: true,
      data: {
        id: 'inv-1',
        issueDate: '2026-08-04',
        status: 'Validated',
        statusDisplay: 'Validée',
        type: 'Invoice',
        isCreditNote: false,
        firmClientAssignmentId: 'a1',
        clientName: 'Ste Fatima',
        currency: 'TND',
        subTotal: 160,
        totalVat: 30.4,
        withholdingAmount: 0,
        totalAmount: 190.4,
        amountPaid: 0,
        amountDue: 190.4,
        isRecurring: false,
        lines: [],
        payments: []
      }
    });

    expect(status).toBe(HonorairesInvoiceStatus.Validated);
    expect(type).toBe(HonorairesDocumentType.Invoice);
  });
});
