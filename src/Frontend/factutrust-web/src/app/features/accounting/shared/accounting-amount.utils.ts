export const ACCOUNTING_AMOUNT_LOCALE = 'fr-TN';
export const ACCOUNTING_AMOUNT_FRACTION_DIGITS = 3;

const MILLIME_FACTOR = 1000;

/**
 * Arrondit un montant au millime (3 décimales TND).
 */
export function roundToMillime(value: number): number {
  if (!Number.isFinite(value)) {
    return 0;
  }
  return Math.round(value * MILLIME_FACTOR) / MILLIME_FACTOR;
}

/**
 * Parse une saisie utilisateur (virgule, point, espaces milliers) en nombre ou null.
 */
export function parseAccountingAmount(input: string | number | null | undefined): number | null {
  if (input === null || input === undefined) {
    return null;
  }

  if (typeof input === 'number') {
    if (!Number.isFinite(input) || input < 0) {
      return null;
    }
    return roundToMillime(input);
  }

  const trimmed = input.trim();
  if (!trimmed) {
    return null;
  }

  const normalized = trimmed
    .replace(/\s/g, '')
    .replace(/,/g, '.');

  const parsed = Number(normalized);
  if (!Number.isFinite(parsed) || parsed < 0) {
    return null;
  }

  return roundToMillime(parsed);
}

/**
 * Formate un montant pour affichage (locale fr-TN, 3 décimales).
 */
export function formatAccountingAmount(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '';
  }

  return new Intl.NumberFormat(ACCOUNTING_AMOUNT_LOCALE, {
    minimumFractionDigits: ACCOUNTING_AMOUNT_FRACTION_DIGITS,
    maximumFractionDigits: ACCOUNTING_AMOUNT_FRACTION_DIGITS
  }).format(value);
}

/**
 * Normalise un montant stocké (arrondi millime, null si vide ou négatif).
 */
export function normalizeAccountingAmount(value: number | null | undefined): number | null {
  if (value === null || value === undefined) {
    return null;
  }
  if (!Number.isFinite(value) || value <= 0) {
    return null;
  }
  return roundToMillime(value);
}

/**
 * Indique si un montant est considéré comme saisi (strictement positif).
 */
export function hasAccountingAmount(value: number | null | undefined): boolean {
  return normalizeAccountingAmount(value) !== null;
}
