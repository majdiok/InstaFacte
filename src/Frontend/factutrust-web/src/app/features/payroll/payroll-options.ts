export const MARITAL_STATUS_OPTIONS = [
  { value: 'Single', label: 'Célibataire' },
  { value: 'Married', label: 'Marié(e)' },
  { value: 'Divorced', label: 'Divorcé(e)' },
  { value: 'Widowed', label: 'Veuf(ve)' }
];

export const CONTRACT_TYPE_OPTIONS = [
  { value: 'Cdi', label: 'CDI' },
  { value: 'Cdd', label: 'CDD' },
  { value: 'Sivp', label: 'SIVP' },
  { value: 'Karama', label: 'Karama' },
  { value: 'Internship', label: 'Stage' },
  { value: 'Other', label: 'Autre' }
];

export const SOCIAL_REGIME_OPTIONS = [
  { value: 'Rsna', label: 'RSNA (CNSS)' },
  { value: 'SivpExonere', label: 'SIVP exonéré' },
  { value: 'Rsa', label: 'RSA (salariés agricoles)' },
  { value: 'None', label: 'Non assujetti' }
];

export const SUSPENSION_TYPE_OPTIONS = [
  { value: 'Disciplinary', label: 'Disciplinaire' },
  { value: 'Administrative', label: 'Administrative' },
  { value: 'Other', label: 'Autre' }
];

export const LEAVE_TYPE_OPTIONS = [
  { value: 'Paid', label: 'Congé payé' },
  { value: 'Unpaid', label: 'Congé sans solde' },
  { value: 'Sick', label: 'Maladie' },
  { value: 'Maternity', label: 'Maternité' },
  { value: 'Paternity', label: 'Congé paternité' },
  { value: 'Bereavement', label: 'Congé décès' },
  { value: 'Recovery', label: 'Récupération' },
  { value: 'Unjustified', label: 'Absence injustifiée' },
  { value: 'Other', label: 'Autre' }
];

export const OVERTIME_RATE_OPTIONS = [
  { value: 125, label: '125 %' },
  { value: 150, label: '150 %' },
  { value: 175, label: '175 % (nuit / férié)' },
  { value: 200, label: '200 %' }
];

export const WEEKLY_REGIME_OPTIONS = [
  { value: 'FortyEightHours', label: '48 h / semaine (régime général)' },
  { value: 'FortyHours', label: '40 h / semaine' }
];

/** Diviseur mensuel du taux horaire selon le régime hebdomadaire du contrat. */
export function weeklyRegimeDivisor(weeklyRegime?: string): string {
  return weeklyRegime === 'FortyHours' ? '173,33' : '208';
}

/** Taux de majoration légal proposé par défaut selon le régime hebdomadaire. */
export function defaultOvertimeRate(weeklyRegime?: string): number {
  return weeklyRegime === 'FortyHours' ? 125 : 175;
}

/**
 * Taux de majoration proposés pour un salarié : taux légaux du régime en tête,
 * complétés des taux étendus quand l'option de l'exercice les autorise.
 * Reflète la validation backend (OvertimeRatePercentExtensions.IsValid).
 */
export function overtimeRateOptions(weeklyRegime?: string, extendedRatesEnabled = false): { value: number; label: string }[] {
  const h40 = weeklyRegime === 'FortyHours';
  const options: { value: number; label: string }[] = [
    { value: 125, label: h40 ? '125 % (légal — 41e à 48e heure)' : '125 %' },
    { value: 150, label: h40 ? '150 % (légal — au-delà de 48 h)' : '150 %' }
  ];
  if (!h40)
    options.push({ value: 175, label: '175 % (taux légal 48 h)' });
  else if (extendedRatesEnabled)
    options.push({ value: 175, label: '175 % (nuit / férié)' });
  if (extendedRatesEnabled)
    options.push({ value: 200, label: '200 %' });
  return options;
}

export { TUNISIAN_GOVERNORATES } from '@core/validators/tunisian-payroll.validators';

export const TUNISIAN_GOVERNORATE_OPTIONS = [
  'Ariana', 'Béja', 'Ben Arous', 'Bizerte', 'Gabès', 'Gafsa', 'Jendouba',
  'Kairouan', 'Kasserine', 'Kébili', 'Le Kef', 'Mahdia', 'La Manouba',
  'Médenine', 'Monastir', 'Nabeul', 'Sfax', 'Sidi Bouzid', 'Siliana',
  'Sousse', 'Tataouine', 'Tozeur', 'Tunis', 'Zaghouan'
].map(g => ({ value: g, label: g }));
