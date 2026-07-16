import { Pipe, PipeTransform } from '@angular/core';

// Intl.NumberFormat négocie la locale (repli « fr ») et ne dépend pas des données
// de locale Angular (registerLocaleData), contrairement à DecimalPipe.
const formatter = new Intl.NumberFormat('fr-TN', {
  minimumFractionDigits: 3,
  maximumFractionDigits: 3
});

/** Formats monetary amounts in Tunisian dinars (3 decimal places). */
export function formatPayrollAmount(value: number | null | undefined, showCurrency = true): string {
  if (value == null || Number.isNaN(value)) return '—';
  const formatted = formatter.format(value);
  return showCurrency ? `${formatted} TND` : formatted;
}

@Pipe({ name: 'payrollAmount', standalone: true })
export class PayrollAmountPipe implements PipeTransform {
  transform(value: number | null | undefined, showCurrency = true): string {
    return formatPayrollAmount(value, showCurrency);
  }
}
