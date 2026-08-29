/**
 * Valeurs d'usine et libellés partagés pour le paramétrage d'exercice décalé
 * (plan « Exercices décalés », P4 frontend). Centralisés pour être réutilisés par l'écran de
 * paramétrage et comme repli civil (mois = 1) des composants de run / tableau d'amortissement
 * quand l'API de paramètres n'est pas joignable.
 */

/** Libellés des mois en français (index 0 = janvier), alignés sur l'UI comptable tunisienne. */
export const MONTH_LABELS_FR: readonly string[] = [
  'Janvier',
  'Février',
  'Mars',
  'Avril',
  'Mai',
  'Juin',
  'Juillet',
  'Août',
  'Septembre',
  'Octobre',
  'Novembre',
  'Décembre'
];

/** Formats de libellé d'exercice autorisés (décision D2). */
export const FISCAL_YEAR_LABEL_FORMATS = ['N/N+1', 'N'] as const;
export type FiscalYearLabelFormat = (typeof FISCAL_YEAR_LABEL_FORMATS)[number];

/** Forme éditable du paramétrage (consommée par les formulaires UI). */
export interface FixedAssetSettingsForm {
  fiscalYearStartMonth: number;
  fiscalYearLabelFormat: string;
}

/** Valeurs d'usine : exercice civil (janvier) au format N/N+1 — comportement historique préservé. */
export function defaultFiscalYearSettings(): FixedAssetSettingsForm {
  return { fiscalYearStartMonth: 1, fiscalYearLabelFormat: 'N/N+1' };
}

/**
 * Normalise un payload brut (API ou JSON) en `FixedAssetSettingsForm` sûr : borne le mois dans
 * [1, 12] (repli janvier si invalide) et valide le format (repli N/N+1 si invalide).
 */
export function normalizeFiscalYearSettings(raw: {
  fiscalYearStartMonth?: number;
  fiscalYearLabelFormat?: string;
} | null | undefined): FixedAssetSettingsForm {
  const startMonth = raw?.fiscalYearStartMonth;
  const fmt = raw?.fiscalYearLabelFormat;
  return {
    fiscalYearStartMonth:
      typeof startMonth === 'number' && Number.isInteger(startMonth) && startMonth >= 1 && startMonth <= 12
        ? startMonth
        : 1,
    fiscalYearLabelFormat: fmt === 'N' || fmt === 'N/N+1' ? fmt : 'N/N+1'
  };
}
