import {
  calculateMarginPercent,
  calculateSaleTtc,
  calculateUnitPriceFromMargin,
  deriveUnitPriceFromTtc,
  recalculatePricing
} from './product-pricing.utils';

describe('product-pricing.utils', () => {
  it('calculates Axeane example HT from margin', () => {
    expect(calculateUnitPriceFromMargin(45000, 12)).toBe(50400);
  });

  it('calculates sale TTC with 13% VAT', () => {
    expect(calculateSaleTtc(50400, 13, false)).toBe(56952);
  });

  it('round-trips HT from TTC', () => {
    const ttc = calculateSaleTtc(50, 13, false);
    expect(deriveUnitPriceFromTtc(ttc, 13, false)).toBe(50);
  });

  it('recalculates from margin driver', () => {
    const result = recalculatePricing(
      {
        purchasePrice: 45000,
        profitMarginPercent: 12,
        unitPriceHt: 0,
        saleTtc: 0,
        vatRatePercent: 13,
        isFodecApplicable: false
      },
      'margin'
    );

    expect(result.unitPriceHt).toBe(50400);
    expect(result.saleTtc).toBe(56952);
  });

  it('recalculates margin from HT', () => {
    const result = recalculatePricing(
      {
        purchasePrice: 45000,
        profitMarginPercent: null,
        unitPriceHt: 50400,
        saleTtc: 56952,
        vatRatePercent: 13,
        isFodecApplicable: false
      },
      'unitPriceHt'
    );

    expect(calculateMarginPercent(45000, result.unitPriceHt)).toBe(12);
  });
});
