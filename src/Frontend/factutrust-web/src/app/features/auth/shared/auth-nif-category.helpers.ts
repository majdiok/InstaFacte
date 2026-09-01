import { cleanNifValue } from './auth-registration.helpers';
import type { CompanySegmentCode } from '../register-wizard/registration-catalog';

/**
 * V1 (plan §3.2, no external API) — client-side NIF taxpayer-category parsing, mirroring
 * `FactuTrust.Domain.ValueObjects.NIF` / `TaxpayerCategories` exactly: on a normalized
 * `NNNNNNN/L/A/M/NNN` NIF, the taxpayer category is the single letter `L` right after the
 * first `/` (index 8 of the normalized string).
 */
export type NifTaxpayerCategory = 'A' | 'B' | 'C' | 'D' | 'E' | 'F' | 'G';

/** Full-format NIF pattern, capturing the taxpayer category letter. Mirrors `NIF.FullFormatPattern`. */
export const NIF_CATEGORY_PATTERN = /^\d{7}\/([A-Z])\/[A-Z]\/[A-Z]\/\d{3}$/;

/** Mirrors `TaxpayerCategories.GetDescription()` (backend `NIF.cs`) — French labels, one per category. */
export const NIF_CATEGORY_LABELS_FR: Record<NifTaxpayerCategory, string> = {
  A: 'Personne physique',
  B: 'Société de personnes',
  C: 'Société de capitaux',
  D: 'Association',
  E: 'Établissement public',
  F: 'Personne morale étrangère',
  G: 'Autre'
};

/**
 * Categories that map cleanly onto one of the six `CompanySegmentCode` values (plan's own
 * example: category D ⇒ segment "association"). Deliberately narrow — the other categories
 * (personne physique, société, établissement public…) don't correspond 1:1 to a single
 * segment, so no suggestion is offered for them in v1.
 */
export const NIF_CATEGORY_SUGGESTED_SEGMENT: Partial<Record<NifTaxpayerCategory, CompanySegmentCode>> = {
  D: 'association'
};

export interface NifCategoryInfo {
  category: NifTaxpayerCategory;
  categoryLabelFr: string;
  suggestedSegmentCode?: CompanySegmentCode;
}

/**
 * Parses the taxpayer category out of a raw (possibly PrimeNG-mask-shaped) NIF value.
 * Returns `null` when the value is empty/nullish or doesn't yet match the full
 * `NNNNNNN/L/A/M/NNN` format (e.g. while the user is still typing).
 */
export function parseNifCategory(value: string | null | undefined): NifCategoryInfo | null {
  const normalized = cleanNifValue(value);
  if (!normalized) return null;

  const match = NIF_CATEGORY_PATTERN.exec(normalized);
  if (!match) return null;

  const category = match[1] as NifTaxpayerCategory;
  const label = NIF_CATEGORY_LABELS_FR[category];
  if (!label) return null;

  const suggestedSegmentCode = NIF_CATEGORY_SUGGESTED_SEGMENT[category];
  return suggestedSegmentCode
    ? { category, categoryLabelFr: label, suggestedSegmentCode }
    : { category, categoryLabelFr: label };
}

export interface NifSegmentSuggestion {
  category: NifCategoryInfo;
  /** True when there is no current segment yet and it should be pre-filled silently. */
  autoSelect: boolean;
  /** Non-blocking coherence hint (plan §3.2) — `null` when nothing to warn about. */
  hintMessage: string | null;
}

/**
 * Pure combinator (no Angular deps, unit-testable in isolation) behind the wizard's
 * NIF ⇄ segment coherence hint (plan §3.2): "Votre NIF indique une association ; votre
 * segment est Commerce — confirmer ?".
 *
 * `currentSegmentCode` empty/undefined ⇒ nothing chosen yet: signal `autoSelect: true` (kept
 * for defensiveness/future-proofing — the current wizard step order requires picking a segment
 * before reaching the NIF field, so this branch isn't reachable through the UI today).
 */
export function evaluateNifSegmentSuggestion(
  nif: string | null | undefined,
  currentSegmentCode: string | null | undefined,
  segmentLabelResolver: (code: string) => string | undefined
): NifSegmentSuggestion | null {
  const category = parseNifCategory(nif);
  if (!category?.suggestedSegmentCode) {
    return null;
  }

  if (!currentSegmentCode) {
    return { category, autoSelect: true, hintMessage: null };
  }

  if (currentSegmentCode === category.suggestedSegmentCode) {
    return { category, autoSelect: false, hintMessage: null };
  }

  const currentLabel = segmentLabelResolver(currentSegmentCode) || currentSegmentCode;
  const hintMessage =
    `Votre NIF indique une ${category.categoryLabelFr.toLowerCase()} ; ` +
    `votre segment est ${currentLabel} — confirmer ?`;
  return { category, autoSelect: false, hintMessage };
}
