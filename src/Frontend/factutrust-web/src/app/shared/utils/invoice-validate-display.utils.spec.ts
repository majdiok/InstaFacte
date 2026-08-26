import {
  isValidateLineInsufficient,
  isValidateLineWarning,
  lineImpactDisplay,
  ValidateLineAvailabilityView
} from './invoice-validate-display.utils';

function detail(
  overrides: Partial<ValidateLineAvailabilityView> = {}
): ValidateLineAvailabilityView {
  return {
    isStockManaged: true,
    isAvailable: true,
    alertLevel: 'ok',
    humanMessage: 'Cette vente va retirer 7 unités. Il restera 45 unités.',
    availableQuantity: 52,
    remainingAfterSale: 45,
    ...overrides
  };
}

describe('lineImpactDisplay', () => {
  it('returns muted dash when availability is missing', () => {
    expect(lineImpactDisplay(undefined)).toEqual({
      tone: 'muted',
      text: '—',
      quantity: null,
      quantityKind: 'none'
    });
  });

  it('returns muted dash when the product is not stock-managed', () => {
    expect(lineImpactDisplay(detail({ isStockManaged: false }))).toEqual({
      tone: 'muted',
      text: '—',
      quantity: null,
      quantityKind: 'none'
    });
  });

  it('returns error with available quantity when stock is insufficient', () => {
    expect(lineImpactDisplay(detail({
      isAvailable: false,
      alertLevel: 'warning',
      availableQuantity: 2,
      remainingAfterSale: 0
    }))).toEqual({
      tone: 'error',
      text: 'Stock insuffisant',
      quantity: 2,
      quantityKind: 'available'
    });
  });

  it('returns the human message when warning and still available', () => {
    expect(lineImpactDisplay(detail({
      alertLevel: 'warning',
      humanMessage: 'Attention : il ne restera que 1 unité (stock bas).'
    }))).toEqual({
      tone: 'warning',
      text: 'Attention : il ne restera que 1 unité (stock bas).',
      quantity: null,
      quantityKind: 'none'
    });
  });

  it('returns remaining quantity for an ok stock-managed line', () => {
    expect(lineImpactDisplay(detail())).toEqual({
      tone: 'ok',
      text: 'Restera',
      quantity: 45,
      quantityKind: 'remaining'
    });
  });
});

describe('isValidateLineInsufficient', () => {
  it('is false when availability is missing', () => {
    expect(isValidateLineInsufficient(undefined)).toBeFalse();
  });

  it('is true when isAvailable is false', () => {
    expect(isValidateLineInsufficient(detail({
      isAvailable: false,
      alertLevel: 'warning'
    }))).toBeTrue();
  });

  it('is true when alertLevel is error', () => {
    expect(isValidateLineInsufficient(detail({ alertLevel: 'error' }))).toBeTrue();
  });

  it('is false for a mere warning on available stock', () => {
    expect(isValidateLineInsufficient(detail({ alertLevel: 'warning' }))).toBeFalse();
  });
});

describe('isValidateLineWarning', () => {
  it('is true only for warning on available stock', () => {
    expect(isValidateLineWarning(detail({ alertLevel: 'warning' }))).toBeTrue();
  });

  it('is false when stock is insufficient even if alertLevel is warning', () => {
    expect(isValidateLineWarning(detail({
      isAvailable: false,
      alertLevel: 'warning'
    }))).toBeFalse();
  });
});
