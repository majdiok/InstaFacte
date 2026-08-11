/**
 * Normalizes a line designation to a string.
 * Handles legacy/corrupted values where PrimeNG autocomplete stored a product object.
 */
export function normalizeDesignation(value: unknown): string {
  if (typeof value === 'string') {
    return value;
  }
  if (value && typeof value === 'object' && 'name' in value) {
    return String((value as { name: unknown }).name ?? '');
  }
  return '';
}

/**
 * Resolves designation from edit state: prefer explicit string, then selected product or typed text.
 */
export function resolveDesignation(
  designation: unknown,
  selectedProduct: unknown
): string {
  const fromDesignation = normalizeDesignation(designation);
  if (fromDesignation) {
    return fromDesignation;
  }
  return normalizeDesignation(selectedProduct);
}
