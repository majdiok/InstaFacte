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

export { TUNISIAN_GOVERNORATES } from '@core/validators/tunisian-payroll.validators';

export const TUNISIAN_GOVERNORATE_OPTIONS = [
  'Ariana', 'Béja', 'Ben Arous', 'Bizerte', 'Gabès', 'Gafsa', 'Jendouba',
  'Kairouan', 'Kasserine', 'Kébili', 'Le Kef', 'Mahdia', 'La Manouba',
  'Médenine', 'Monastir', 'Nabeul', 'Sfax', 'Sidi Bouzid', 'Siliana',
  'Sousse', 'Tataouine', 'Tozeur', 'Tunis', 'Zaghouan'
].map(g => ({ value: g, label: g }));
