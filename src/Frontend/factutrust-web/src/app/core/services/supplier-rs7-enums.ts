import { SupplierRs7IsBracket } from './supplier.service';

/**
 * Contrat JSON de SupplierRs7IsBracket côté API InstaFact.
 * L'API sérialise les enums en chaînes PascalCase via JsonStringEnumConverter
 * (ex. rs7IsBracket: "Reduced15").
 */

const BRACKET_BY_STRING: Record<string, SupplierRs7IsBracket> = {
  unspecified: SupplierRs7IsBracket.Unspecified,
  normal25: SupplierRs7IsBracket.Normal25,
  reduced15: SupplierRs7IsBracket.Reduced15,
  reduced10: SupplierRs7IsBracket.Reduced10
};

const BRACKET_BY_INT: Record<number, SupplierRs7IsBracket> = {
  0: SupplierRs7IsBracket.Unspecified,
  1: SupplierRs7IsBracket.Normal25,
  2: SupplierRs7IsBracket.Reduced15,
  3: SupplierRs7IsBracket.Reduced10
};

const BRACKET_TO_API: Record<SupplierRs7IsBracket, string | null> = {
  [SupplierRs7IsBracket.Unspecified]: null,
  [SupplierRs7IsBracket.Normal25]: 'Normal25',
  [SupplierRs7IsBracket.Reduced15]: 'Reduced15',
  [SupplierRs7IsBracket.Reduced10]: 'Reduced10'
};

export function parseSupplierRs7IsBracket(raw: unknown): SupplierRs7IsBracket | null {
  if (raw === null || raw === undefined) {
    return null;
  }

  if (typeof raw === 'number' && Number.isInteger(raw) && raw in BRACKET_BY_INT) {
    return BRACKET_BY_INT[raw];
  }

  if (typeof raw === 'string') {
    const trimmed = raw.trim();
    if (!trimmed) {
      return null;
    }

    const normalized = trimmed.toLowerCase();
    const direct = BRACKET_BY_STRING[normalized];
    if (direct !== undefined) {
      return direct;
    }
  }

  return null;
}

export function normalizeSupplierRs7IsBracket(raw: unknown): SupplierRs7IsBracket {
  return parseSupplierRs7IsBracket(raw) ?? SupplierRs7IsBracket.Unspecified;
}

export function serializeSupplierRs7IsBracket(
  bracket: SupplierRs7IsBracket | null | undefined
): string | null {
  if (bracket === null || bracket === undefined || bracket === SupplierRs7IsBracket.Unspecified) {
    return null;
  }

  return BRACKET_TO_API[bracket];
}

export function rs7BracketFromTypeCode(code: string | null | undefined): SupplierRs7IsBracket | null {
  if (!code) {
    return null;
  }

  switch (code.trim().toUpperCase()) {
    case 'RS7_000001':
      return SupplierRs7IsBracket.Normal25;
    case 'RS7_000002':
      return SupplierRs7IsBracket.Reduced15;
    case 'RS7_000003':
      return SupplierRs7IsBracket.Reduced10;
    default:
      return null;
  }
}

export function rs7BracketLabel(bracket: SupplierRs7IsBracket): string | null {
  switch (bracket) {
    case SupplierRs7IsBracket.Normal25:
      return 'IS 25 % (RS7, 1,5 %)';
    case SupplierRs7IsBracket.Reduced15:
      return 'IS 15 % (RS7, 1 %)';
    case SupplierRs7IsBracket.Reduced10:
      return 'IS 10 % (RS7, 0,5 %)';
    default:
      return null;
  }
}

export function isRs7TypeCode(code: string | null | undefined): boolean {
  return !!code && code.trim().toUpperCase().startsWith('RS7_');
}
