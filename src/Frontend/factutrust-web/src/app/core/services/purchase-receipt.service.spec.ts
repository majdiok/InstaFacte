import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import { environment } from '@environments/environment';
import { PurchaseReceiptService } from './purchase-receipt.service';

describe('PurchaseReceiptService', () => {
  let service: PurchaseReceiptService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/purchasereceipts`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PurchaseReceiptService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('createSupplierInvoice poste avec SKIP_ERROR_TOAST (le composant gère ses propres erreurs)', () => {
    service
      .createSupplierInvoice('br-1', {
        invoiceNumber: 'FS-2026-000001',
        invoiceDate: '2026-08-04',
        paymentTermDays: 30
      })
      .subscribe();

    const req = httpMock.expectOne(`${baseUrl}/br-1/create-supplier-invoice`);
    expect(req.request.method).toBe('POST');
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(true);
    req.flush({ success: true, data: { id: 'si-1', invoiceNumber: 'FS-2026-000001' } });
  });

  it('getSupplierInvoicePrefill garde le traitement d’erreur global', () => {
    service.getSupplierInvoicePrefill('br-1').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/br-1/supplier-invoice-prefill`);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(false);
    req.flush({ success: true, data: null });
  });
});
