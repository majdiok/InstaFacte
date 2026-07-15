import {
  mapPurchaseOrderDetailFromApi,
  normalizePurchaseOrderStatus,
  PurchaseOrderStatus,
  PurchaseOrderDetailApiDto
} from './purchase-order.service';

function buildRawDto(status: PurchaseOrderStatus | string): PurchaseOrderDetailApiDto {
  return {
    id: 'po-1',
    number: 'BC-2026-000001',
    orderDate: '2026-04-26',
    expectedDeliveryDate: null,
    status,
    statusDisplay: 'Confirmée',
    statusCss: 'status-confirmed',
    reference: null,
    notes: null,
    supplier: {
      id: 'supplier-1',
      name: 'Fournisseur Test',
      nif: null,
      email: 'supplier@test.com',
      address: 'Tunis'
    },
    lines: [],
    subTotal: 0,
    totalVat: 0,
    totalTTC: 0,
    confirmedAt: null,
    receivedAt: null,
    cancelledAt: null,
    cancellationReason: null,
    createdAt: '2026-04-26T00:00:00Z',
    warehouseId: null,
    warehouseName: null
  };
}

describe('purchase-order status mapping', () => {
  it('normalizes camelCase enum values', () => {
    expect(normalizePurchaseOrderStatus('confirmed')).toBe(PurchaseOrderStatus.Confirmed);
    expect(normalizePurchaseOrderStatus('partiallyReceived')).toBe(PurchaseOrderStatus.PartiallyReceived);
  });

  it('keeps backward compatibility with PascalCase values', () => {
    expect(normalizePurchaseOrderStatus('Confirmed')).toBe(PurchaseOrderStatus.Confirmed);
    expect(normalizePurchaseOrderStatus('Draft')).toBe(PurchaseOrderStatus.Draft);
  });

  it('returns null for unknown enum values', () => {
    expect(normalizePurchaseOrderStatus('unknownState')).toBeNull();
  });

  it('supports numeric enum values and rejects unknown numbers', () => {
    expect(normalizePurchaseOrderStatus(PurchaseOrderStatus.Received)).toBe(PurchaseOrderStatus.Received);
    expect(normalizePurchaseOrderStatus(999 as PurchaseOrderStatus)).toBeNull();
  });

  it('maps raw dto with known status', () => {
    const mapped = mapPurchaseOrderDetailFromApi(buildRawDto('confirmed'));
    expect(mapped).not.toBeNull();
    expect(mapped!.status).toBe(PurchaseOrderStatus.Confirmed);
  });

  it('maps dto when status is numeric enum', () => {
    const mapped = mapPurchaseOrderDetailFromApi(buildRawDto(PurchaseOrderStatus.PartiallyReceived));
    expect(mapped).not.toBeNull();
    expect(mapped!.status).toBe(PurchaseOrderStatus.PartiallyReceived);
  });

  it('returns null when dto status is unknown', () => {
    const mapped = mapPurchaseOrderDetailFromApi(buildRawDto('badStatus'));
    expect(mapped).toBeNull();
  });

  it('maps warehouse fields from API dto', () => {
    const raw = buildRawDto('confirmed');
    raw.warehouseId = 'wh-11111111-1111-1111-1111-111111111111';
    raw.warehouseName = 'Entrepôt central';
    const mapped = mapPurchaseOrderDetailFromApi(raw);
    expect(mapped).not.toBeNull();
    expect(mapped!.warehouseId).toBe('wh-11111111-1111-1111-1111-111111111111');
    expect(mapped!.warehouseName).toBe('Entrepôt central');
  });
});
