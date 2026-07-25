import { FormGroup } from '@angular/forms';
import { NIF_PATTERN } from '@shared/validation/validation-rules';

/**
 * Nettoie et normalise la valeur NIF du masque PrimeNG.
 */
export function cleanNifValue(value: string | null | undefined): string {
  if (!value) return '';

  let cleaned = value
    .replace(/_/g, '')
    .replace(/\s/g, '')
    .replace(/[\u2044\u2215/]/g, '/')
    .toUpperCase()
    .trim();

  if (!cleaned || cleaned === '/////' || cleaned === '///') {
    return '';
  }

  const noSlashMatch = cleaned.match(/^(\d{7})([A-Z])([A-Z])([A-Z])(\d{3})$/);
  if (noSlashMatch) {
    return `${noSlashMatch[1]}/${noSlashMatch[2]}/${noSlashMatch[3]}/${noSlashMatch[4]}/${noSlashMatch[5]}`;
  }

  const missingFirstSlash = cleaned.match(/^(\d{7})([A-Z])\/([A-Z])\/([A-Z])\/(\d{3})$/);
  if (missingFirstSlash) {
    return `${missingFirstSlash[1]}/${missingFirstSlash[2]}/${missingFirstSlash[3]}/${missingFirstSlash[4]}/${missingFirstSlash[5]}`;
  }

  return cleaned;
}

/** Remove spaces from Tunisian phone mask value. */
export function cleanPhoneValue(value: string | null | undefined): string {
  return (value?.replace(/\s/g, '') ?? '').trim();
}

export function trimOptional(value: string | null | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

export function trimRequired(value: string | null | undefined): string {
  return (value ?? '').trim();
}

export function dropdownStringValue(value: unknown): string {
  if (typeof value === 'string') return value.trim();
  if (value !== null && typeof value === 'object' && 'value' in (value as object)) {
    return String((value as { value: unknown }).value ?? '').trim();
  }
  return String(value ?? '').trim();
}

export interface CompanyRegisterValidationInput {
  taxRegime: unknown;
  governorate: unknown;
  phone: string | null | undefined;
  nif: string | null | undefined;
}

/**
 * Pre-submit validation for company registration (mirrors RegisterComponent.validateFormData).
 */
export function validateCompanyRegisterFormData(
  formValue: CompanyRegisterValidationInput,
  nifOverride?: string,
  nifTouched = false
): string[] {
  const errors: string[] = [];

  if (formValue.taxRegime !== 0 && !formValue.taxRegime) {
    errors.push('Le régime fiscal est requis');
  }

  if (!formValue.governorate) {
    errors.push('Le gouvernorat est requis');
  }

  const phone = cleanPhoneValue(formValue.phone);
  if (phone && !/^[2-57-9]\d{7}$/.test(phone)) {
    errors.push('Le numéro de téléphone doit contenir 8 chiffres et commencer par 2, 3, 4, 5, 7 ou 9');
  }

  const nifToValidate = nifOverride !== undefined
    ? nifOverride
    : cleanNifValue(formValue.nif);

  if (nifToValidate && nifToValidate.length > 0) {
    if (!NIF_PATTERN.test(nifToValidate)) {
      errors.push('Le format du NIF est invalide (format attendu: 1234567/A/B/C/000)');
    }
  } else if (nifTouched) {
    errors.push('Le matricule fiscal est requis (format: 1234567/A/B/C/000)');
  }

  return errors;
}

export interface FirmRegisterValidationInput {
  governorate: unknown;
  phone: string | null | undefined;
  nif: string | null | undefined;
}

/** Pre-submit validation for firm registration. */
export function validateFirmRegisterFormData(
  formValue: FirmRegisterValidationInput,
  nifOverride?: string,
  nifTouched = false
): string[] {
  const errors: string[] = [];

  if (!formValue.governorate) {
    errors.push('Le gouvernorat est requis');
  }

  const phone = cleanPhoneValue(formValue.phone);
  if (phone && !/^[2-57-9]\d{7}$/.test(phone)) {
    errors.push('Le numéro de téléphone doit contenir 8 chiffres et commencer par 2, 3, 4, 5, 7 ou 9');
  }

  const nifToValidate = nifOverride !== undefined
    ? nifOverride
    : cleanNifValue(formValue.nif);

  if (nifToValidate && nifToValidate.length > 0) {
    if (!NIF_PATTERN.test(nifToValidate)) {
      errors.push('Le format du NIF est invalide (format attendu: 1234567/A/B/C/000)');
    }
  } else if (nifTouched) {
    errors.push('Le matricule fiscal est requis (format: 1234567/A/B/C/000)');
  }

  return errors;
}

export function markAllFormControlsTouched(form: FormGroup): void {
  Object.keys(form.controls).forEach(key => {
    form.get(key)?.markAsTouched();
  });
}

export function scrollToFirstInvalidField(): void {
  const firstInvalidField = document.querySelector('.ng-invalid');
  if (firstInvalidField) {
    firstInvalidField.scrollIntoView({ behavior: 'smooth', block: 'center' });
    const input = firstInvalidField.querySelector('input, select, textarea') as HTMLElement;
    if (input) {
      setTimeout(() => input.focus(), 300);
    }
  }
}

export function applyNifBlurCleanup(form: FormGroup, controlName = 'nif'): void {
  const nifControl = form.get(controlName);
  if (!nifControl) return;

  const currentValue = nifControl.value || '';
  const cleaned = cleanNifValue(currentValue);

  if (cleaned !== currentValue) {
    nifControl.setValue(cleaned, { emitEvent: false });
    nifControl.updateValueAndValidity({ emitEvent: false });
    nifControl.markAsTouched();
  } else if (cleaned) {
    nifControl.updateValueAndValidity({ emitEvent: false });
  }
}
