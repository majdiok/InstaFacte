import { effectiveLineDiscountPercent, EMPTY_LINE_PROMOTION, lineTotalWithPromotion, mapResolvedPricePromotion } from './document-line-pricing.helper';
import { ResolvedPrice } from '@core/services/pricing.service';

describe('document-line-pricing helpers', () => {
  it('mapResolvedPricePromotion maps API fields', () => {
    const resolved: ResolvedPrice = {
      unitPriceHT: 100,
      currency: 'TND',
      source: 'Catalog',
      isNegotiated: false,
      promotionDiscountPercent: 10,
      promotionName: 'solde été',
      promotionId: 'promo-1',
      promotionEligible: true,
      promotionMinQuantityRequired: null
    };

    expect(mapResolvedPricePromotion(resolved)).toEqual({
      promotionDiscountPercent: 10,
      promotionName: 'solde été',
      promotionId: 'promo-1',
      promotionEligible: true,
      promotionMinQuantityRequired: null
    });
  });

  it('effectiveLineDiscountPercent prefers manual discount', () => {
    const promo = { ...EMPTY_LINE_PROMOTION, promotionEligible: true, promotionDiscountPercent: 10 };
    expect(effectiveLineDiscountPercent(5, promo)).toBe(5);
    expect(effectiveLineDiscountPercent(null, promo)).toBe(10);
  });

  it('lineTotalWithPromotion applies promotion preview', () => {
    const promo = { ...EMPTY_LINE_PROMOTION, promotionEligible: true, promotionDiscountPercent: 10 };
    expect(lineTotalWithPromotion(3, 100, null, promo)).toBe(270);
  });
});
