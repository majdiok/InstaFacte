import {
  canSaveSalesReturnNote,
  clampReturnedQuantity,
  isSalesReturnNoteDraft,
  SalesReturnNoteStatus
} from './sales-return-note.model';

describe('sales-return-note.model', () => {
  describe('clampReturnedQuantity', () => {
    it('returns 0 for empty, negative or non-finite values', () => {
      expect(clampReturnedQuantity(0, 10)).toBe(0);
      expect(clampReturnedQuantity(-2, 10)).toBe(0);
      expect(clampReturnedQuantity(Number.NaN, 10)).toBe(0);
    });

    it('caps the quantity at the remaining invoiceable amount', () => {
      expect(clampReturnedQuantity(12, 5)).toBe(5);
      expect(clampReturnedQuantity(3, 5)).toBe(3);
    });

    it('returns 0 when the remaining quantity is not positive', () => {
      expect(clampReturnedQuantity(2, 0)).toBe(0);
      expect(clampReturnedQuantity(2, -1)).toBe(0);
    });
  });

  describe('canSaveSalesReturnNote', () => {
    it('is false when the reason is empty (screenshot case)', () => {
      expect(canSaveSalesReturnNote('', [1])).toBe(false);
      expect(canSaveSalesReturnNote('  ab  ', [1])).toBe(false);
    });

    it('is false when every quantity is zero', () => {
      expect(canSaveSalesReturnNote('retour client', [0, 0])).toBe(false);
    });

    it('is true when motif and at least one quantity are valid', () => {
      expect(canSaveSalesReturnNote('retour client', [0, 1])).toBe(true);
    });
  });

  describe('isSalesReturnNoteDraft', () => {
    it('recognizes API string and numeric draft values', () => {
      expect(isSalesReturnNoteDraft(SalesReturnNoteStatus.Draft)).toBe(true);
      expect(isSalesReturnNoteDraft('Draft')).toBe(true);
      expect(isSalesReturnNoteDraft(0)).toBe(true);
      expect(isSalesReturnNoteDraft(SalesReturnNoteStatus.Confirmed)).toBe(false);
    });
  });
});
