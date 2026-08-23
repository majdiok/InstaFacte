import {
    mapSupplierInvoiceDetailFromApi,
    mapSupplierInvoiceListItemFromApi,
    normalizeSupplierInvoiceStatus,
    SupplierInvoiceStatus,
    isSupplierInvoicePaid,
    isSupplierInvoiceOpenForPayment,
    SupplierInvoiceDetail,
    SupplierInvoiceListItem,
    SupplierInvoiceService,
    CreateStandaloneSupplierInvoiceRequest
} from './supplier-invoice.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { CashDeskService } from './cash-desk.service';

function buildMinimalListItem(
    status: SupplierInvoiceStatus | string | number
): SupplierInvoiceListItem {
    return {
        id: 'inv-1',
        invoiceNumber: 'FS-2026-TEST',
        invoiceDate: '2026-04-26',
        dueDate: '2026-05-26',
        supplierName: 'Fournisseur',
        supplierId: 's1',
        purchaseOrderNumber: 'BC-1',
        purchaseOrderId: 'po-1',
        status: status as SupplierInvoiceStatus,
        statusDisplay: '—',
        statusCss: 'x',
        totalHT: 0,
        totalTTC: 0,
        lineCount: 0,
        totalPaid: 0,
        remainingAmount: 0,
        paidAt: null
    };
}

function buildMinimalDetail(status: SupplierInvoiceStatus | string): SupplierInvoiceDetail {
    return {
        id: 'inv-1',
        invoiceNumber: 'FS-2026-TEST',
        invoiceDate: '2026-04-26',
        dueDate: '2026-05-26',
        status: status as SupplierInvoiceStatus,
        statusDisplay: '—',
        statusCss: 'x',
        externalReference: null,
        notes: null,
        supplier: { id: 's1', name: 'F', nif: null, email: 'e@e.com', address: 'A' },
        purchaseOrderId: 'po-1',
        purchaseOrderNumber: 'BC-1',
        lines: [],
        subTotal: 0,
        totalVat: 0,
        totalTTC: 100,
        totalPaid: 0,
        remainingAmount: 100,
        payments: [],
        paidAt: null,
        paymentMethod: null,
        paymentReference: null,
        cancelledAt: null,
        cancellationReason: null,
        createdAt: '2026-04-26T00:00:00Z'
    };
}

describe('supplier-invoice status mapping', () => {
    it('normalizes camelCase API enum strings', () => {
        expect(normalizeSupplierInvoiceStatus('pending')).toBe(SupplierInvoiceStatus.Pending);
        expect(normalizeSupplierInvoiceStatus('partiallyPaid')).toBe(SupplierInvoiceStatus.PartiallyPaid);
        expect(normalizeSupplierInvoiceStatus('paid')).toBe(SupplierInvoiceStatus.Paid);
        expect(normalizeSupplierInvoiceStatus('cancelled')).toBe(SupplierInvoiceStatus.Cancelled);
    });

    it('keeps backward compatibility with PascalCase strings', () => {
        expect(normalizeSupplierInvoiceStatus('Pending')).toBe(SupplierInvoiceStatus.Pending);
        expect(normalizeSupplierInvoiceStatus('PartiallyPaid')).toBe(SupplierInvoiceStatus.PartiallyPaid);
    });

    it('supports numeric values and rejects invalid numbers', () => {
        expect(normalizeSupplierInvoiceStatus(3)).toBe(SupplierInvoiceStatus.PartiallyPaid);
        expect(normalizeSupplierInvoiceStatus(999)).toBeNull();
    });

    it('returns null for unknown string values', () => {
        expect(normalizeSupplierInvoiceStatus('badStatus')).toBeNull();
    });

    it('isSupplierInvoicePaid recognizes camelCase paid', () => {
        expect(isSupplierInvoicePaid('paid')).toBe(true);
        expect(isSupplierInvoicePaid('Paid')).toBe(true);
        expect(isSupplierInvoicePaid(SupplierInvoiceStatus.Paid)).toBe(true);
    });

    it('isSupplierInvoiceOpenForPayment is true for pending and partiallyPaid (camelCase)', () => {
        expect(isSupplierInvoiceOpenForPayment('pending')).toBe(true);
        expect(isSupplierInvoiceOpenForPayment('partiallyPaid')).toBe(true);
        expect(isSupplierInvoiceOpenForPayment('paid')).toBe(false);
    });

    it('mapSupplierInvoiceListItemFromApi maps partiallyPaid to enum', () => {
        const raw = buildMinimalListItem('partiallyPaid' as unknown as SupplierInvoiceStatus);
        const mapped = mapSupplierInvoiceListItemFromApi(raw);
        expect(mapped.status).toBe(SupplierInvoiceStatus.PartiallyPaid);
    });

    it('mapSupplierInvoiceDetailFromApi maps known status', () => {
        const mapped = mapSupplierInvoiceDetailFromApi(buildMinimalDetail('partiallyPaid'));
        expect(mapped).not.toBeNull();
        expect(mapped!.status).toBe(SupplierInvoiceStatus.PartiallyPaid);
    });

    it('mapSupplierInvoiceDetailFromApi returns null for unknown status', () => {
        const dto = buildMinimalDetail(SupplierInvoiceStatus.Pending);
        const mapped = mapSupplierInvoiceDetailFromApi({ ...dto, status: 'bad' as unknown as SupplierInvoiceStatus });
        expect(mapped).toBeNull();
    });
});

describe('SupplierInvoiceService.create', () => {
    let service: SupplierInvoiceService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                SupplierInvoiceService,
                provideHttpClient(),
                provideHttpClientTesting(),
                { provide: CashDeskService, useValue: { invalidateCachesAfterCashLedgerMutation: () => undefined } }
            ]
        });
        service = TestBed.inject(SupplierInvoiceService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('POSTs the standalone payload to the collection endpoint', () => {
        const payload: CreateStandaloneSupplierInvoiceRequest = {
            supplierId: 's1',
            invoiceDate: '2026-08-17',
            paymentTermDays: 30,
            useSuggestedNumber: true,
            lines: [{ productId: 'p1', quantity: 1, unitPriceHt: 100, discountPercent: 5 }]
        };

        service.create(payload).subscribe(res => {
            expect(res.success).toBeTrue();
            expect(res.data?.id).toBe('inv-1');
            expect(res.data?.invoiceNumber).toBe('FS-2026-000001');
        });

        const req = http.expectOne(`${environment.apiUrl}/supplierinvoices`);
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual(payload);
        req.flush({ success: true, data: { id: 'inv-1', invoiceNumber: 'FS-2026-000001' } });
    });
});
