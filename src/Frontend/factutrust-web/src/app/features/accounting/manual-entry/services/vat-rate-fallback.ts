import { VatRateOption } from '@core/services/tax.service';
import { VALID_VAT_RATES } from '@shared/validation/validation-rules';

/** Default Tunisian VAT rates when the tenant API is unavailable or returns no data. */
export function buildDefaultVatRateOptions(): VatRateOption[] {
  return VALID_VAT_RATES.map(percent => ({
    id: `fallback-${percent}`,
    percent,
    label: `${percent}%`
  }));
}
