import { InvoiceType, InvoiceWizardState } from '../models/invoice-wizard.models';

/** Default FODEC rate (%) — aligned with backend AccountingSettings.FodecRatePercent. */
export const DEFAULT_FODEC_RATE_PERCENT = 1;

export function roundTnd(n: number): number {
  return Math.round(n * 1000) / 1000;
}

export interface WizardTotalsCheckResult {
  isValid: boolean;
  htMatch: boolean;
  fodecMatch: boolean;
  vatMatch: boolean;
  ttcMatch: boolean;
}

/** Canonical totals consistency check (wizard service + validation service). */
export function computeWizardTotalsCheck(
  state: InvoiceWizardState,
  tolerance = 0.01
): WizardTotalsCheckResult {
  const lines = state.lines;
  const totals = state.totals;
  const sign = state.metadata.type === InvoiceType.CreditNote ? -1 : 1;

  const calculatedHT = sign * lines.reduce((sum, l) => sum + l.totalHT, 0);
  const calculatedFodec = sign * lines.reduce((sum, l) => sum + l.fodecAmount, 0);
  const calculatedVAT = sign * lines.reduce((sum, l) => sum + l.vatAmount, 0);
  const linesTtcSum = sign * lines.reduce((sum, l) => sum + l.totalTTC, 0);
  const calculatedTTC = roundTnd(linesTtcSum + totals.fiscalStampAmount);

  const htMatch = Math.abs(calculatedHT - totals.totalHT) < tolerance;
  const fodecMatch = Math.abs(calculatedFodec - totals.totalFodec) < tolerance;
  const vatMatch = Math.abs(calculatedVAT - totals.totalVat) < tolerance;
  const ttcMatch = Math.abs(calculatedTTC - totals.totalTTC) < tolerance;

  return {
    isValid: htMatch && fodecMatch && vatMatch && ttcMatch,
    htMatch,
    fodecMatch,
    vatMatch,
    ttcMatch
  };
}

/** Line-level FODEC then VAT on (HT + FODEC), rounded to millimes. */
export function calculateLineFodecAmounts(
  totalHT: number,
  vatRate: number,
  isFodecApplicable: boolean,
  fodecRatePercent: number
): { fodecAmount: number; vatAmount: number; totalTTC: number } {
  const fodecAmount =
    isFodecApplicable && fodecRatePercent > 0
      ? roundTnd(totalHT * fodecRatePercent / 100)
      : 0;
  const vatBase = totalHT + fodecAmount;
  const vatAmount = roundTnd(vatBase * vatRate / 100);
  const totalTTC = roundTnd(totalHT + fodecAmount + vatAmount);
  return { fodecAmount, vatAmount, totalTTC };
}
