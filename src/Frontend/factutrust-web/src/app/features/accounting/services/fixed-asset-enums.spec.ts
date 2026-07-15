import {
  DepreciationMethod,
  FixedAssetStatus,
  canDisposeStatus,
  isActiveAssetStatus,
  isDraftStatus,
  isInServiceOrBeyond,
  parseDepreciationMethod,
  parseFixedAssetStatus
} from './fixed-asset-enums';

describe('fixed-asset-enums', () => {
  describe('parseFixedAssetStatus', () => {
    it('accepts PascalCase API strings', () => {
      expect(parseFixedAssetStatus('Draft')).toBe(FixedAssetStatus.Draft);
      expect(parseFixedAssetStatus('InService')).toBe(FixedAssetStatus.InService);
    });

    it('accepts lowercase strings', () => {
      expect(parseFixedAssetStatus('draft')).toBe(FixedAssetStatus.Draft);
    });

    it('accepts legacy numeric values', () => {
      expect(parseFixedAssetStatus(0)).toBe(FixedAssetStatus.Draft);
      expect(parseFixedAssetStatus(1)).toBe(FixedAssetStatus.InService);
    });

    it('returns null for invalid values', () => {
      expect(parseFixedAssetStatus('Unknown')).toBeNull();
      expect(parseFixedAssetStatus(null)).toBeNull();
    });
  });

  describe('parseDepreciationMethod', () => {
    it('accepts PascalCase API strings', () => {
      expect(parseDepreciationMethod('Linear')).toBe(DepreciationMethod.Linear);
      expect(parseDepreciationMethod('Accelerated')).toBe(DepreciationMethod.Accelerated);
    });

    it('accepts legacy numeric values', () => {
      expect(parseDepreciationMethod(0)).toBe(DepreciationMethod.Linear);
      expect(parseDepreciationMethod(1)).toBe(DepreciationMethod.Accelerated);
    });

    it('defaults to Linear for invalid values', () => {
      expect(parseDepreciationMethod('invalid')).toBe(DepreciationMethod.Linear);
    });
  });

  describe('status helpers', () => {
    it('isDraftStatus', () => {
      expect(isDraftStatus(FixedAssetStatus.Draft)).toBeTrue();
      expect(isDraftStatus(FixedAssetStatus.InService)).toBeFalse();
    });

    it('isInServiceOrBeyond', () => {
      expect(isInServiceOrBeyond(FixedAssetStatus.Draft)).toBeFalse();
      expect(isInServiceOrBeyond(FixedAssetStatus.InService)).toBeTrue();
      expect(isInServiceOrBeyond(FixedAssetStatus.Disposed)).toBeTrue();
    });

    it('isActiveAssetStatus', () => {
      expect(isActiveAssetStatus(FixedAssetStatus.InService)).toBeTrue();
      expect(isActiveAssetStatus(FixedAssetStatus.FullyDepreciated)).toBeTrue();
      expect(isActiveAssetStatus(FixedAssetStatus.Disposed)).toBeFalse();
    });

    it('canDisposeStatus', () => {
      expect(canDisposeStatus(FixedAssetStatus.InService)).toBeTrue();
      expect(canDisposeStatus(FixedAssetStatus.Draft)).toBeFalse();
      expect(canDisposeStatus(FixedAssetStatus.Disposed)).toBeFalse();
    });
  });
});
