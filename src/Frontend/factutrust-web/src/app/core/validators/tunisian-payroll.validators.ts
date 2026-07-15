import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

const CIN_PATTERN = /^\d{8}$/;
const CNSS_PATTERN = /^\d{10}$/;
const RIB_PATTERN = /^\d{20}$/;
const TN_PHONE_PATTERN = /^(\+?216)?[2-57-9]\d{7}$/;

export const TUNISIAN_GOVERNORATES = [
  'Ariana', 'Béja', 'Ben Arous', 'Bizerte', 'Gabès', 'Gafsa', 'Jendouba',
  'Kairouan', 'Kasserine', 'Kébili', 'Le Kef', 'Mahdia', 'La Manouba',
  'Médenine', 'Monastir', 'Nabeul', 'Sfax', 'Sidi Bouzid', 'Siliana',
  'Sousse', 'Tataouine', 'Tozeur', 'Tunis', 'Zaghouan'
] as const;

function optionalPattern(pattern: RegExp, message: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = control.value;
    if (raw == null || String(raw).trim() === '') return null;
    const cleaned = String(raw).replace(/\s/g, '').replace(/-/g, '');
    return pattern.test(cleaned) ? null : { tunisianFormat: message };
  };
}

export const tunisianPayrollValidators = {
  cin: optionalPattern(CIN_PATTERN, 'Le CIN doit comporter 8 chiffres.'),
  cnss: optionalPattern(CNSS_PATTERN, 'Le numéro CNSS doit comporter 10 chiffres.'),
  rib: optionalPattern(RIB_PATTERN, 'Le RIB doit comporter 20 chiffres.'),
  phone: optionalPattern(TN_PHONE_PATTERN, 'Numéro de téléphone tunisien invalide.'),
  governorate: (control: AbstractControl): ValidationErrors | null => {
    const raw = control.value;
    if (raw == null || String(raw).trim() === '') return null;
    const match = TUNISIAN_GOVERNORATES.some(g => g.toLowerCase() === String(raw).trim().toLowerCase());
    return match ? null : { tunisianFormat: 'Gouvernorat tunisien invalide.' };
  }
};

export function formatValidationError(errors: ValidationErrors | null): string | null {
  if (!errors) return null;
  if (errors['tunisianFormat']) return errors['tunisianFormat'] as string;
  if (errors['required']) return 'Ce champ est obligatoire.';
  if (errors['email']) return 'Adresse e-mail invalide.';
  if (errors['min']) return 'La valeur est trop faible.';
  return 'Valeur invalide.';
}
