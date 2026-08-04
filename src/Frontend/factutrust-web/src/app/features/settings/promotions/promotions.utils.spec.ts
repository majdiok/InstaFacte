import {
  buildScopePreview,
  filterPromotions,
  promotionLifecycle,
  promotionPeriodProgress,
  promotionScopeKind
} from './promotions.utils';
import { Promotion } from '@core/services/pricing.service';

function promo(overrides: Partial<Promotion> = {}): Promotion {
  return {
    id: '1',
    name: 'Soldes été',
    productId: null,
    productCategoryId: null,
    clientId: null,
    discountType: 'Percentage',
    discountPercent: 10,
    discountAmount: null,
    minQuantity: 1,
    startsOn: '2026-07-01',
    endsOn: '2026-07-31',
    isActive: true,
    priority: 0,
    isRunningToday: true,
    scopeLabel: 'tous les produits · tous les clients',
    ...overrides
  };
}

describe('promotions.utils', () => {
  const today = new Date('2026-07-15');

  it('computes lifecycle states', () => {
    expect(promotionLifecycle(promo(), today)).toBe('running');
    expect(promotionLifecycle(promo({ isActive: false }), today)).toBe('disabled');
    expect(promotionLifecycle(promo({ startsOn: '2026-08-01' }), today)).toBe('upcoming');
    expect(promotionLifecycle(promo({ endsOn: '2026-07-01' }), today)).toBe('expired');
  });

  it('filters by search, status and scope', () => {
    const items = [
      promo({ name: 'Alpha' }),
      promo({
        id: '2',
        name: 'Client VIP',
        clientId: 'c1',
        scopeLabel: 'tous les produits · un client'
      }),
      promo({
        id: '3',
        name: 'Catégorie',
        productCategoryId: 'cat1',
        scopeLabel: 'une catégorie · tous les clients'
      })
    ];

    expect(
      filterPromotions(items, { search: 'vip', status: 'all', scope: 'all', today }).length
    ).toBe(1);

    expect(
      filterPromotions(items, { search: '', status: 'running', scope: 'client', today }).length
    ).toBe(1);

    expect(promotionScopeKind(items[2])).toBe('category');
  });

  it('builds scope preview labels', () => {
    expect(buildScopePreview(null, null, null)).toBe('Tous les produits · Tous les clients');
    expect(buildScopePreview('P001 — Chaise', null, null)).toBe(
      '« P001 — Chaise » · Tous les clients'
    );
    expect(buildScopePreview(null, 'Salon', null)).toBe('Catégorie « Salon » · Tous les clients');
    expect(buildScopePreview(null, null, 'Client VIP')).toBe('Tous les produits · Client « Client VIP »');
    expect(buildScopePreview('P001 — Chaise', null, 'Client VIP')).toBe(
      '« P001 — Chaise » · Client « Client VIP »'
    );
    expect(buildScopePreview(null, 'Salon', 'Client VIP')).toBe(
      'Catégorie « Salon » · Client « Client VIP »'
    );
  });

  it('computes period progress within bounds', () => {
    const progress = promotionPeriodProgress(promo(), today);
    expect(progress.percent).toBeGreaterThan(0);
    expect(progress.percent).toBeLessThanOrEqual(100);
    expect(progress.tone).toBe('success');
  });
});
