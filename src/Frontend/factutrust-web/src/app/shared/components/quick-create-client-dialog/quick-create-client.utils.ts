/**
 * Normalizes a Tunisian NIF (Matricule Fiscal) input value.
 * Strips mask placeholders, uppercases, and inserts missing slashes.
 */
export function cleanNif(value: string | null | undefined): string {
  let normalized = (value ?? '').replace(/_/g, '').trim().replace(/\s+/g, '').toUpperCase();
  if (!normalized || normalized === '/////' || normalized === '///') {
    normalized = '';
  }
  if (normalized.length > 0 && !normalized.includes('/')) {
    const match = normalized.match(/^(\d{7})([A-Z0-9])([A-Z0-9])([A-Z0-9])(\d{3})$/);
    if (match) {
      normalized = `${match[1]}/${match[2]}/${match[3]}/${match[4]}/${match[5]}`;
    }
  }
  const missingFirstSlash = normalized.match(/^(\d{7})([A-Z])\/([A-Z])\/([A-Z])\/(\d{3})$/);
  if (missingFirstSlash) {
    normalized = `${missingFirstSlash[1]}/${missingFirstSlash[2]}/${missingFirstSlash[3]}/${missingFirstSlash[4]}/${missingFirstSlash[5]}`;
  }
  return normalized;
}
