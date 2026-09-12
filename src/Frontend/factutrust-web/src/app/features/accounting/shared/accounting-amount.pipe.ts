import { Pipe, PipeTransform } from '@angular/core';
import { FUNCTIONAL_CURRENCY } from '../manual-entry/models/entry-form.model';

// Intl.NumberFormat négocie la locale (repli « fr ») et ne dépend pas des données de locale
// Angular (registerLocaleData), contrairement à DecimalPipe. Même choix que payroll-amount.pipe.
const FORMATTERS = new Map<number, Intl.NumberFormat>();

function formatterFor(decimals: number): Intl.NumberFormat {
  let formatter = FORMATTERS.get(decimals);
  if (!formatter) {
    formatter = new Intl.NumberFormat('fr-TN', {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals
    });
    FORMATTERS.set(decimals, formatter);
  }
  return formatter;
}

/** Décimales d'usage d'une devise : 3 pour le dinar (millime), 2 pour les autres par défaut. */
export function decimalsForCurrency(currency: string | null | undefined): number {
  return !currency || currency === FUNCTIONAL_CURRENCY ? 3 : 2;
}

/**
 * Formate un montant comptable avec son code devise.
 *
 * <p>
 * Écrit pour le multi-devises : le nombre de décimales suit la devise plutôt que d'être figé à 3,
 * et le code affiché n'est plus le littéral « TND » des gabarits. Un montant absent rend « — »
 * plutôt que « 0,000 », qui se confondrait avec un solde nul.
 * </p>
 *
 * @param decimals Force le nombre de décimales ; sinon déduit de la devise.
 */
export function formatAccountingAmount(
  value: number | null | undefined,
  currency: string | null | undefined = FUNCTIONAL_CURRENCY,
  showCurrency = true,
  decimals?: number
): string {
  if (value == null || Number.isNaN(value)) return '—';

  const code = currency || FUNCTIONAL_CURRENCY;
  const formatted = formatterFor(decimals ?? decimalsForCurrency(code)).format(value);
  return showCurrency ? `${formatted} ${code}` : formatted;
}

/**
 * Usage : `{{ montant | accountingAmount }}`, `{{ montant | accountingAmount : 'EUR' }}`,
 * ou `{{ montant | accountingAmount : devise : false }}` pour omettre le code.
 */
@Pipe({ name: 'accountingAmount', standalone: true })
export class AccountingAmountPipe implements PipeTransform {
  transform(
    value: number | null | undefined,
    currency: string | null | undefined = FUNCTIONAL_CURRENCY,
    showCurrency = true,
    decimals?: number
  ): string {
    return formatAccountingAmount(value, currency, showCurrency, decimals);
  }
}
