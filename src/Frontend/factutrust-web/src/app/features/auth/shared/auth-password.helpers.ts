export interface PasswordCriteria {
  minLength: boolean;
  hasUpper: boolean;
  hasLower: boolean;
  hasDigit: boolean;
  hasSpecial: boolean;
}

export type PasswordStrengthLevel = 'empty' | 'weak' | 'fair' | 'good' | 'strong';

/** Backend Identity password complexity pattern (12+ chars, upper, lower, digit, special). */
export const AUTH_PASSWORD_VALIDATORS_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$/;

export function passwordMismatch(password: string | null | undefined, confirm: string | null | undefined): boolean {
  return !!(confirm && password !== confirm);
}

export function passwordMatches(password: string | null | undefined, confirm: string | null | undefined): boolean {
  return !!(password && confirm && password === confirm);
}

export function passwordCriteria(password: string | null | undefined): PasswordCriteria {
  const p = password ?? '';
  return {
    minLength: p.length >= 12,
    hasUpper: /[A-Z]/.test(p),
    hasLower: /[a-z]/.test(p),
    hasDigit: /\d/.test(p),
    hasSpecial: /[^a-zA-Z0-9]/.test(p)
  };
}

export function passwordStrengthMetCount(criteria: PasswordCriteria): number {
  return [criteria.minLength, criteria.hasUpper, criteria.hasLower, criteria.hasDigit, criteria.hasSpecial]
    .filter(Boolean).length;
}

export function passwordStrengthLevel(password: string | null | undefined, criteria: PasswordCriteria): PasswordStrengthLevel {
  const n = passwordStrengthMetCount(criteria);
  const p = password ?? '';
  if (!p) return 'empty';
  if (n <= 2) return 'weak';
  if (n === 3) return 'fair';
  if (n === 4) return 'good';
  return 'strong';
}

export function passwordStrengthLabel(level: PasswordStrengthLevel): string {
  switch (level) {
    case 'empty': return '';
    case 'weak': return 'Force du mot de passe : faible';
    case 'fair': return 'Force du mot de passe : moyenne';
    case 'good': return 'Force du mot de passe : bonne';
    case 'strong': return 'Force du mot de passe : forte';
    default: return '';
  }
}
