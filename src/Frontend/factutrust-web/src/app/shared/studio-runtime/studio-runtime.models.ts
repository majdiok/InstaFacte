/**
 * Shared types for the metadata-driven Studio renderers (dynamic form / table).
 * Kept in the shared layer so the renderers don't depend on a feature module.
 * Mirrors backend FactuTrust.Domain.Enums.CustomFieldType (integer values must stay aligned).
 */
export enum CustomFieldType {
  Text = 0,
  MultilineText = 1,
  Number = 2,
  Decimal = 3,
  Boolean = 4,
  Date = 5,
  DateTime = 6,
  Select = 7,
  MultiSelect = 8,
  RelationCustom = 9,
  RelationExisting = 10,
  Money = 11,
  Percentage = 12,
  Rating = 13,
  QrCode = 14,
  Barcode = 15,
  AutoNumber = 16,
  Formula = 17,
  Lookup = 18,
  Rollup = 19,
  Attachment = 20,
  Signature = 21
}

/**
 * Normalises a wire `fieldType` to the numeric {@link CustomFieldType}. The API serializes enums as
 * PascalCase strings (global JsonStringEnumConverter) — e.g. "Date", "RelationCustom" — while this enum
 * is numeric and the renderers switch on numbers. Tolerant of the enum name, a numeric string, or a
 * number (future-proof). Unknown/missing → Text (safe default).
 */
export function parseFieldType(value: unknown): CustomFieldType {
  if (typeof value === 'number') return value as CustomFieldType;
  if (typeof value === 'string') {
    if (/^\d+$/.test(value)) return Number(value) as CustomFieldType;
    const mapped = (CustomFieldType as Record<string, unknown>)[value];
    if (typeof mapped === 'number') return mapped as CustomFieldType;
  }
  return CustomFieldType.Text;
}

export const FIELD_TYPE_OPTIONS: { value: CustomFieldType; label: string }[] = [
  { value: CustomFieldType.Text, label: 'Texte' },
  { value: CustomFieldType.MultilineText, label: 'Texte long' },
  { value: CustomFieldType.Number, label: 'Nombre entier' },
  { value: CustomFieldType.Decimal, label: 'Nombre décimal' },
  { value: CustomFieldType.Money, label: 'Monétaire' },
  { value: CustomFieldType.Percentage, label: 'Pourcentage' },
  { value: CustomFieldType.Rating, label: 'Note (étoiles)' },
  { value: CustomFieldType.Boolean, label: 'Oui / Non' },
  { value: CustomFieldType.Date, label: 'Date' },
  { value: CustomFieldType.DateTime, label: 'Date et heure' },
  { value: CustomFieldType.Select, label: 'Liste (choix unique)' },
  { value: CustomFieldType.MultiSelect, label: 'Liste (choix multiple)' },
  { value: CustomFieldType.QrCode, label: 'QR code' },
  { value: CustomFieldType.Barcode, label: 'Code-barres' },
  { value: CustomFieldType.AutoNumber, label: 'Numéro automatique' },
  { value: CustomFieldType.Formula, label: 'Formule (calcul)' },
  { value: CustomFieldType.Lookup, label: 'Recherche (champ lié)' },
  { value: CustomFieldType.Rollup, label: 'Agrégat (enfants)' },
  { value: CustomFieldType.Attachment, label: 'Pièce jointe' },
  { value: CustomFieldType.Signature, label: 'Signature' },
  { value: CustomFieldType.RelationCustom, label: 'Relation (table personnalisée)' },
  { value: CustomFieldType.RelationExisting, label: 'Relation (donnée existante)' }
];

export interface FieldValidationRules {
  minLength?: number | null;
  maxLength?: number | null;
  min?: number | null;
  max?: number | null;
  regex?: string | null;
  decimals?: number | null;
}

export interface SelectOption {
  value: string;
  label: string;
}

export interface RelationRef {
  kind: string;
  ref: string;
}

export interface CustomField {
  id: string;
  key: string;
  label: string;
  fieldType: CustomFieldType;
  isRequired: boolean;
  isUnique: boolean;
  sortOrder: number;
  rules: FieldValidationRules | null;
  options: SelectOption[] | null;
  relation: RelationRef | null;
  isActive: boolean;
  /** Per-type config parsed from OptionsJson (money.currency, rating.max, render.format/source, …). */
  config?: Record<string, any> | null;
}

// ---- Form layout (mirrors backend FormLayout) ----

export interface FormFieldRef {
  key: string;
  labelOverride?: string | null;
  width?: 'full' | 'half' | null;
}

export interface FormSection {
  title?: string | null;
  fields: FormFieldRef[];
}

export interface FormLayout {
  sections: FormSection[];
}

// ---- Report result (mirrors backend ReportResultDto) ----

export interface ReportColumn {
  key: string;
  label: string;
  kind: 'dimension' | 'measure';
  format?: string | null;
  formatOptions?: {
    statusMap?: Record<string, string>;
    lookupTable?: string | null;
    lookupDisplayColumn?: string | null;
  } | null;
  dataType?: string | null;
  numeric?: boolean;
}

export interface ReportResult {
  columns: ReportColumn[];
  rows: Record<string, unknown>[];
  totalRows: number;
  displayValues?: Record<string, string> | null;
}
