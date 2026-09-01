import type { Company } from '@core/services/company.service';

/**
 * Phase 3 (plan §3.5) — pure helper computing the company profile completion percentage.
 * Counts the deferrable/enrichment fields that have been filled; returns a rounded integer
 * percentage (0–100). A field counts as "filled" when it's a non-blank string.
 *
 * The required legal fields (companyName, nif, taxRegime, governorat…) are always present
 * after registration, so only the *optional enrichment* fields drive the completion ratio —
 * matching the plan's "relance douce" intent: nudge users to add their logo, RIB, etc.
 */
const PROFILE_COMPLETION_FIELDS: readonly (keyof Company | 'address.streetLine2' | 'address.postalCode')[] = [
  'logoUrl',
  'rib',
  'iban',
  'bankName',
  'website',
  'tradeName',
  'commerceRegistry',
  'cnssEmployerNumber',
  'invoiceFooter',
  'address.streetLine2',
  'address.postalCode'
];

export function computeCompanyProfileCompletion(company: Company | null | undefined): number {
  if (!company) return 0;

  let filled = 0;
  for (const field of PROFILE_COMPLETION_FIELDS) {
    if (field.includes('.')) {
      const [parent, child] = field.split('.') as ['address', 'streetLine2' | 'postalCode'];
      const value = company[parent]?.[child];
      if (value && value.trim()) filled++;
    } else {
      const value = company[field as keyof Company];
      if (typeof value === 'string' && value.trim()) filled++;
    }
  }

  return Math.round((filled / PROFILE_COMPLETION_FIELDS.length) * 100);
}
