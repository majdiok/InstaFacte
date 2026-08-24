import {
  buildOrderStockCacheKey,
  canSellProduct
} from './pos-stock-guard';

describe('canSellProduct', () => {
  const managed = {
    name: 'Chemise',
    isStockManaged: true,
    quantityAvailable: 2
  };

  it('allows unmanaged products', () => {
    const result = canSellProduct(
      { name: 'Prestation', isStockManaged: false, quantityAvailable: 0 },
      1,
      0
    );
    expect(result.ok).toBeTrue();
  });

  it('allows when catalog quantity is unknown', () => {
    const result = canSellProduct(
      { name: 'Chemise', isStockManaged: true, quantityAvailable: null },
      1,
      0
    );
    expect(result.ok).toBeTrue();
  });

  it('refuses when available is 0', () => {
    const result = canSellProduct({ ...managed, quantityAvailable: 0 }, 1, 0);
    expect(result.ok).toBeFalse();
    expect(result.message).toContain('Chemise');
    expect(result.message).toContain('disponible 0');
  });

  it('refuses when requested exceeds available', () => {
    const result = canSellProduct(managed, 1, 2);
    expect(result.ok).toBeFalse();
    expect(result.message).toContain('demandé 3');
    expect(result.message).toContain('disponible 2');
  });

  it('allows when requested equals available', () => {
    expect(canSellProduct(managed, 1, 1).ok).toBeTrue();
  });
});

describe('buildOrderStockCacheKey', () => {
  it('changes when the warehouse changes', () => {
    const lines = [{ productId: 'a', quantity: 1 }];
    expect(buildOrderStockCacheKey('wh-1', lines)).not.toEqual(
      buildOrderStockCacheKey('wh-2', lines)
    );
  });

  it('changes when cart quantities change', () => {
    const warehouse = 'wh-1';
    expect(
      buildOrderStockCacheKey(warehouse, [{ productId: 'a', quantity: 1 }])
    ).not.toEqual(
      buildOrderStockCacheKey(warehouse, [{ productId: 'a', quantity: 2 }])
    );
  });

  it('is stable regardless of line order', () => {
    const a = { productId: 'a', quantity: 1 };
    const b = { productId: 'b', quantity: 2 };
    expect(buildOrderStockCacheKey('wh', [a, b])).toEqual(
      buildOrderStockCacheKey('wh', [b, a])
    );
  });
});
