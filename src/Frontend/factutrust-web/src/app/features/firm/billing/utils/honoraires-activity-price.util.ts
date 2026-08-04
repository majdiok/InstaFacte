import { FirmActivityCode } from '@core/services/firm-governance.service';

/**
 * Resolves the catalog default unit price for an activity code.
 * Returns null when the code is missing or has no configured tariff.
 */
export function resolveCatalogUnitPrice(
  codes: FirmActivityCode[],
  code: string | null | undefined
): number | null {
  if (!code) return null;
  const match = codes.find(c => c.code.toUpperCase() === code.toUpperCase());
  const price = match?.defaultUnitPrice;
  if (price == null || price <= 0) return null;
  return price;
}

/**
 * Applies catalog suggestion: positive tariff → that value; otherwise 0.
 */
export function suggestUnitPriceFromCatalog(
  codes: FirmActivityCode[],
  code: string | null | undefined
): number {
  return resolveCatalogUnitPrice(codes, code) ?? 0;
}

/**
 * True when the line unit price differs from the catalog tariff (manual override).
 */
export function isUnitPriceOverride(
  codes: FirmActivityCode[],
  code: string | null | undefined,
  unitPrice: number
): boolean {
  const catalog = resolveCatalogUnitPrice(codes, code);
  if (catalog == null) return false;
  return Math.abs(catalog - unitPrice) > 0.0005;
}
