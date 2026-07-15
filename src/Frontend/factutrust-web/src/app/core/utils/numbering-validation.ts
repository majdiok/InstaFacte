/** Matches tenant-configured document numbers (e.g. FAC-2026-000001, FAC-101-26). */
export const CONFIGURED_DOCUMENT_NUMBER_PATTERN = /^[A-Za-z0-9]+([\-/][A-Za-z0-9]+)+$/;

export function isConfiguredDocumentNumber(value: string): boolean {
  const trimmed = value.trim();
  return trimmed.length > 0 && CONFIGURED_DOCUMENT_NUMBER_PATTERN.test(trimmed);
}
