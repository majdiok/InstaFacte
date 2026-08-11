import { normalizeDesignation, resolveDesignation } from './invoice-line.utils';

describe('invoice-line.utils', () => {
  describe('normalizeDesignation', () => {
    it('returns string values as-is', () => {
      expect(normalizeDesignation('Tableau')).toBe('Tableau');
      expect(normalizeDesignation('')).toBe('');
    });

    it('extracts name from product-like objects', () => {
      expect(normalizeDesignation({ name: 'Refrigirateur beko', id: '1' })).toBe('Refrigirateur beko');
    });

    it('returns empty string for null, undefined, and numbers', () => {
      expect(normalizeDesignation(null)).toBe('');
      expect(normalizeDesignation(undefined)).toBe('');
      expect(normalizeDesignation(42)).toBe('');
    });

    it('returns empty string when object has no name', () => {
      expect(normalizeDesignation({ id: '1' })).toBe('');
      expect(normalizeDesignation({ name: null })).toBe('');
    });
  });

  describe('resolveDesignation', () => {
    it('prefers explicit designation string', () => {
      expect(resolveDesignation('Custom', { name: 'Product' })).toBe('Custom');
    });

    it('falls back to selected product name', () => {
      expect(resolveDesignation('', { name: 'Product' })).toBe('Product');
      expect(resolveDesignation(null, { name: 'Product' })).toBe('Product');
    });

    it('falls back to typed autocomplete text', () => {
      expect(resolveDesignation('', 'Custom text')).toBe('Custom text');
    });

    it('extracts name from corrupted designation object', () => {
      expect(resolveDesignation({ name: 'From object' }, null)).toBe('From object');
    });
  });
});
