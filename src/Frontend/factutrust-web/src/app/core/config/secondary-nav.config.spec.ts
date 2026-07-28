import {
  SECONDARY_NAV_SECTION_ORDER,
  secondaryNavDisplayLabel
} from './secondary-nav.config';

describe('secondary-nav.config', () => {
  it('keeps the validated desktop section order', () => {
    expect([...SECONDARY_NAV_SECTION_ORDER]).toEqual([
      'Ventes',
      'Achats',
      'Stock',
      'Trésorerie',
      'Fiches',
      'RH & Paie',
      'Comptabilité',
      'Fiscal / TEJ'
    ]);
  });

  it('maps Fiscal / TEJ to TEJ for display only', () => {
    expect(secondaryNavDisplayLabel('Fiscal / TEJ')).toBe('TEJ');
    expect(secondaryNavDisplayLabel('Ventes')).toBe('Ventes');
  });
});
