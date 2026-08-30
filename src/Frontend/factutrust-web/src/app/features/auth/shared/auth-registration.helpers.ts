import { FormGroup } from '@angular/forms';
import { NIF_PATTERN } from '@shared/validation/validation-rules';
import type { RegisterRequest } from '@core/services/auth.service';

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

/** After a wizard step change, bring the form header back into view (page scroll). */
export function scrollAuthWizardStepIntoView(): void {
  queueMicrotask(() => {
    const target =
      document.querySelector('.form-header-text') ??
      document.querySelector('.auth-form-card');
    target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });
}

/**
 * Company registration form values (raw form value shape), used to assemble a
 * `RegisterRequest` with today's exact cleaning/coercion rules.
 */
export interface CompanyRegisterFormValue {
  email: string | null | undefined;
  password: string | null | undefined;
  confirmPassword: string | null | undefined;
  firstName: string | null | undefined;
  lastName: string | null | undefined;
  companyName: string | null | undefined;
  phone: string | null | undefined;
  taxRegime: unknown;
  companyEmail: string | null | undefined;
  website: string | null | undefined;
  street: string | null | undefined;
  streetLine2: string | null | undefined;
  city: string | null | undefined;
  postalCode: string | null | undefined;
  governorate: unknown;
  warehouseName: string | null | undefined;
}

/**
 * Additive helper extracting today's `RegisterComponent.onSubmit()` request-assembly
 * semantics (trims, phone/NIF cleaning, tax-regime dropdown coercion) so the new
 * registration wizard can reuse it verbatim. The legacy `register.component.ts` is
 * NOT refactored to call this — it keeps its own inline logic untouched.
 */
export function buildCompanyRegisterRequest(
  formValue: CompanyRegisterFormValue,
  cleanedNif: string
): Omit<RegisterRequest, 'companySegment' | 'businessDomain' | 'enabledModules'> {
  return {
    email: trimRequired(formValue.email),
    password: formValue.password || '',
    confirmPassword: formValue.confirmPassword || '',
    firstName: trimRequired(formValue.firstName),
    lastName: trimRequired(formValue.lastName),
    companyName: trimRequired(formValue.companyName),
    phone: cleanPhoneValue(formValue.phone),
    nif: cleanedNif,
    taxRegime: typeof formValue.taxRegime === 'number'
      ? formValue.taxRegime
      : (typeof formValue.taxRegime === 'object' && formValue.taxRegime !== null && 'value' in formValue.taxRegime
        ? Number((formValue.taxRegime as { value: unknown }).value)
        : 0),
    companyEmail: trimRequired(formValue.companyEmail),
    website: trimOptional(formValue.website),
    street: trimRequired(formValue.street),
    streetLine2: trimOptional(formValue.streetLine2),
    city: trimRequired(formValue.city),
    postalCode: trimOptional(formValue.postalCode),
    governorate: dropdownStringValue(formValue.governorate),
    warehouseName: trimOptional(formValue.warehouseName)
  };
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
