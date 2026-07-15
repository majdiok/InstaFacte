import {
    mapSupplierInvoiceDetailFromApi,
    mapSupplierInvoiceListItemFromApi,
    normalizeSupplierInvoiceStatus,
    SupplierInvoiceStatus,
    isSupplierInvoicePaid,
    isSupplierInvoiceOpenForPayment,
    SupplierInvoiceDetail,
    SupplierInvoiceListItem
} from './supplier-invoice.service';

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
