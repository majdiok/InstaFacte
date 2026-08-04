import {
  isUnitPriceOverride,
  resolveCatalogUnitPrice,
  suggestUnitPriceFromCatalog
} from './honoraires-activity-price.util';
import { FirmActivityCode } from '@core/services/firm-governance.service';

describe('honoraires-activity-price.util', () => {
  const codes: FirmActivityCode[] = [
    {
      id: '1',
      code: 'CONSEIL',
      label: 'Conseil',
      category: 5,
      categoryDisplay: 'Conseil',
      isBillableByDefault: true,
      defaultUnitPrice: 200,
      isActive: true,
      sortOrder: 10
    },
    {
      id: '2',
      code: 'CAC',
      label: 'Audit',
      category: 4,
      categoryDisplay: 'Audit',
      isBillableByDefault: true,
      defaultUnitPrice: null,
      isActive: true,
      sortOrder: 20
    }
  ];

  it('resolveCatalogUnitPrice returns configured tariff', () => {
    expect(resolveCatalogUnitPrice(codes, 'conseil')).toBe(200);
  });

  it('resolveCatalogUnitPrice returns null for missing tariff', () => {
    expect(resolveCatalogUnitPrice(codes, 'CAC')).toBeNull();
  });

  it('resolveCatalogUnitPrice returns null for unknown code', () => {
    expect(resolveCatalogUnitPrice(codes, 'UNKNOWN')).toBeNull();
  });

  it('suggestUnitPriceFromCatalog falls back to 0', () => {
    expect(suggestUnitPriceFromCatalog(codes, 'CAC')).toBe(0);
    expect(suggestUnitPriceFromCatalog(codes, 'CONSEIL')).toBe(200);
  });

  it('isUnitPriceOverride detects manual deviation', () => {
    expect(isUnitPriceOverride(codes, 'CONSEIL', 200)).toBeFalse();
    expect(isUnitPriceOverride(codes, 'CONSEIL', 250)).toBeTrue();
    expect(isUnitPriceOverride(codes, 'CAC', 150)).toBeFalse();
  });
});
