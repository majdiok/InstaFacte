import {
  isCoherentTriplet,
  isValidAccountFormat,
  isValidAccountTriplet,
  isValidAssetAccount,
  isValidDepreciationAccount,
  isValidExpenseAccount,
  validateAccountTriplet
} from './fixed-asset-account-rules';

// T10 (bug C1) — parité client/serveur : miroir des règles FixedAssetAccountRules (T8 / B5).
// Préfixes, format, cohérence corporel/incorporel et messages/codes d'erreur stables.
describe('fixed-asset-account-rules (T10 / C1)', () => {
  describe('isValidAccountFormat', () => {
    it('accepts digits only, 2 to 20 characters', () => {
      expect(isValidAccountFormat('21')).toBeTrue();
      expect(isValidAccountFormat('2244')).toBeTrue();
      expect(isValidAccountFormat('68112')).toBeTrue();
      expect(isValidAccountFormat('28241234567890123456')).toBeTrue(); // 20 digits
    });

    it('rejects non-digit or out-of-range lengths', () => {
      expect(isValidAccountFormat('')).toBeFalse();
      expect(isValidAccountFormat(null)).toBeFalse();
      expect(isValidAccountFormat(undefined)).toBeFalse();
      expect(isValidAccountFormat('2')).toBeFalse(); // too short
      expect(isValidAccountFormat('21A')).toBeFalse();
      expect(isValidAccountFormat('21 28')).toBeFalse();
      expect(isValidAccountFormat('282412345678901234567')).toBeFalse(); // 21 digits
    });
  });

  describe('isValidAssetAccount', () => {
    it('accepts 21x / 22x and 271, rejects 28x / 29x', () => {
      expect(isValidAssetAccount('2130')).toBeTrue();
      expect(isValidAssetAccount('2244')).toBeTrue();
      expect(isValidAssetAccount('271')).toBeTrue();
      expect(isValidAssetAccount('2813')).toBeFalse();
      expect(isValidAssetAccount('2920')).toBeFalse();
      expect(isValidAssetAccount('')).toBeFalse();
    });
  });

  describe('isValidDepreciationAccount', () => {
    it('accepts 281x / 282x only', () => {
      expect(isValidDepreciationAccount('2813')).toBeTrue();
      expect(isValidDepreciationAccount('2828')).toBeTrue();
      expect(isValidDepreciationAccount('2800')).toBeFalse();
      expect(isValidDepreciationAccount('2130')).toBeFalse();
    });
  });

  describe('isValidExpenseAccount', () => {
    it('accepts 6811 prefix (68111/68112 and finer sub-accounts)', () => {
      expect(isValidExpenseAccount('68111')).toBeTrue();
      expect(isValidExpenseAccount('68112')).toBeTrue();
      expect(isValidExpenseAccount('6811')).toBeTrue();
      expect(isValidExpenseAccount('6818')).toBeFalse();
      expect(isValidExpenseAccount('6812')).toBeFalse();
    });
  });

  describe('isCoherentTriplet', () => {
    it('21x corporel → amort 281x + dotation 68111', () => {
      expect(isCoherentTriplet('2120', '2812', '68111')).toBeTrue();
      expect(isCoherentTriplet('2120', '2828', '68112')).toBeFalse();
      expect(isCoherentTriplet('2120', '2812', '68112')).toBeFalse();
    });

    it('22x incorporel → amort 282x + dotation 68112', () => {
      expect(isCoherentTriplet('2280', '2828', '68112')).toBeTrue();
      expect(isCoherentTriplet('2244', '2812', '68111')).toBeFalse();
      expect(isCoherentTriplet('2280', '2828', '68111')).toBeFalse();
    });

    it('271 (frais préliminaires) — aucune règle de cohérence', () => {
      expect(isCoherentTriplet('271', '2818', '68111')).toBeTrue();
    });
  });

  describe('validateAccountTriplet', () => {
    it('returns the asset-account error first with the stable server message', () => {
      const r = validateAccountTriplet('2813', '2812', '68111');
      expect(r.valid).toBeFalse();
      expect(r.field).toBe('assetAccount');
      expect(r.message).toBe(
        "Compte d'actif invalide : chiffres uniquement, préfixe attendu 21x ou 22x (271 toléré), jamais 28x ni 29x."
      );
    });

    it('returns the depreciation-account error when the asset is valid', () => {
      const r = validateAccountTriplet('2244', '2130', '68112');
      expect(r.valid).toBeFalse();
      expect(r.field).toBe('depreciationAccount');
      expect(r.message).toBe(
        "Compte d'amortissement invalide : chiffres uniquement, préfixe attendu 281x ou 282x."
      );
    });

    it('returns the expense-account error when asset and depreciation are valid', () => {
      const r = validateAccountTriplet('2244', '2824', '6818');
      expect(r.valid).toBeFalse();
      expect(r.field).toBe('expenseAccount');
      expect(r.message).toBe(
        'Compte de dotation invalide : chiffres uniquement, préfixe attendu 68111/68112 (6811 toléré).'
      );
    });

    it('returns the coherence error on the asset-account field (212 + 68112 incohérence)', () => {
      const r = validateAccountTriplet('2120', '2812', '68112');
      expect(r.valid).toBeFalse();
      expect(r.field).toBe('assetAccount');
      expect(r.message).toBe(
        'Incohérence comptable : un actif 21x doit être amorti en 281x avec une dotation 68111 ; un actif 22x doit être amorti en 282x avec une dotation 68112.'
      );
    });

    it('accepts a valid coherent triplet (224/2824/68112)', () => {
      const r = validateAccountTriplet('2244', '2824', '68112');
      expect(r.valid).toBeTrue();
      expect(r.field).toBeNull();
    });

    it('rejects a non-numeric account (frontend guard before server)', () => {
      const r = validateAccountTriplet('INVALID', '2812', '68111');
      expect(r.valid).toBeFalse();
      expect(r.field).toBe('assetAccount');
    });

    it('isValidAccountTriplet mirrors validateAccountTriplet', () => {
      expect(isValidAccountTriplet('2244', '2824', '68112')).toBeTrue();
      expect(isValidAccountTriplet('2120', '2812', '68112')).toBeFalse();
    });
  });
});
