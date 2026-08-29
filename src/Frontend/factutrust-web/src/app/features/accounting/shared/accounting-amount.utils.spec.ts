import {
  formatAccountingAmount,
  hasAccountingAmount,
  normalizeAccountingAmount,
  parseAccountingAmount,
  roundToMillime
} from './accounting-amount.utils';

describe('accounting-amount.utils', () => {
  describe('roundToMillime', () => {
    it('rounds to 3 decimal places', () => {
      expect(roundToMillime(70.0024)).toBe(70.002);
      expect(roundToMillime(100.0005)).toBe(100.001);
    });

    it('returns 0 for non-finite values', () => {
      expect(roundToMillime(NaN)).toBe(0);
      expect(roundToMillime(Infinity)).toBe(0);
    });
  });

  describe('parseAccountingAmount', () => {
    it('parses comma decimal separator', () => {
      expect(parseAccountingAmount('70,002')).toBe(70.002);
    });

    it('parses dot decimal separator', () => {
      expect(parseAccountingAmount('70.002')).toBe(70.002);
    });

    it('parses thousand separators with spaces', () => {
      expect(parseAccountingAmount('70 002,000')).toBe(70002);
    });

    it('parses plain numbers', () => {
      expect(parseAccountingAmount(100.5)).toBe(100.5);
    });

    // T10 (bug C2) — saisie décimale à virgule sur les champs montants des immobilisations.
    it('parses a comma decimal the way a Tunisian user types it (150,500 → 150.5)', () => {
      expect(parseAccountingAmount('150,500')).toBe(150.5);
    });

    it('round-trips through 3 millime decimals (format then parse)', () => {
      const rounded = roundToMillime(12.5685); // 12.569
      // Value kept under 1000 to avoid any locale-specific thousand separator ambiguity.
      expect(parseAccountingAmount(formatAccountingAmount(rounded))).toBe(rounded);
    });

    it('returns null for empty or invalid input', () => {
      expect(parseAccountingAmount('')).toBeNull();
      expect(parseAccountingAmount(null)).toBeNull();
      expect(parseAccountingAmount(undefined)).toBeNull();
      expect(parseAccountingAmount('abc')).toBeNull();
    });

    it('rejects negative values', () => {
      expect(parseAccountingAmount('-10')).toBeNull();
      expect(parseAccountingAmount(-5)).toBeNull();
    });
  });

  describe('formatAccountingAmount', () => {
    it('formats with comma decimal separator', () => {
      const formatted = formatAccountingAmount(70.002);
      expect(formatted).toContain('70');
      expect(formatted).toContain('002');
    });

    it('returns empty string for null', () => {
      expect(formatAccountingAmount(null)).toBe('');
    });
  });

  describe('normalizeAccountingAmount', () => {
    it('rounds positive values to millime', () => {
      expect(normalizeAccountingAmount(70.0024)).toBe(70.002);
    });

    it('returns null for zero, negative, or empty', () => {
      expect(normalizeAccountingAmount(0)).toBeNull();
      expect(normalizeAccountingAmount(-1)).toBeNull();
      expect(normalizeAccountingAmount(null)).toBeNull();
    });
  });

  describe('hasAccountingAmount', () => {
    it('detects positive amounts', () => {
      expect(hasAccountingAmount(10)).toBe(true);
      expect(hasAccountingAmount(0)).toBe(false);
      expect(hasAccountingAmount(null)).toBe(false);
    });
  });
});
