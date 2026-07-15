import { Pipe, PipeTransform } from '@angular/core';

/**
 * Formate un montant en dinar tunisien (TND) selon la locale `fr-TN`.
 *
 * Options :
 *  - `compact: true` → `1,2 K TND`, `4,5 M TND` (pour KPI cards).
 *  - `withSymbol: false` → renvoie uniquement le nombre formaté (sans `TND`).
 *  - `fractionDigits` → forcer le nombre de décimales (par défaut 3 selon DGI).
 *
 * Exemples :
 *  - `49 | ftTndCurrency` → `49,000 TND`
 *  - `49 | ftTndCurrency:{ fractionDigits: 0 }` → `49 TND`
 *  - `1234 | ftTndCurrency:{ compact: true }` → `1,2 K TND`
 */
@Pipe({
  name: 'ftTndCurrency',
  standalone: true,
  pure: true
})
export class FtTndCurrencyPipe implements PipeTransform {
  transform(
    value: number | string | null | undefined,
    options?: {
      compact?: boolean;
      withSymbol?: boolean;
      fractionDigits?: number;
    }
  ): string {
    if (value === null || value === undefined || value === '') {
      return '—';
    }

    const numeric = typeof value === 'number' ? value : Number(value);
    if (Number.isNaN(numeric)) {
      return '—';
    }

    const compact = options?.compact ?? false;
    const withSymbol = options?.withSymbol ?? true;
    const fractionDigits = options?.fractionDigits ?? 3;

    const numberFormatOptions: Intl.NumberFormatOptions = compact
      ? {
          notation: 'compact',
          compactDisplay: 'short',
          maximumFractionDigits: 1
        }
      : {
          minimumFractionDigits: fractionDigits,
          maximumFractionDigits: fractionDigits
        };

    const formatted = new Intl.NumberFormat('fr-TN', numberFormatOptions).format(numeric);

    return withSymbol ? `${formatted} TND` : formatted;
  }
}
