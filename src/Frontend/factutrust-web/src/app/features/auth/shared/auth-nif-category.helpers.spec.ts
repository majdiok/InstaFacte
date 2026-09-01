import {
  parseNifCategory,
  evaluateNifSegmentSuggestion,
  NIF_CATEGORY_LABELS_FR,
  NIF_CATEGORY_SUGGESTED_SEGMENT
} from './auth-nif-category.helpers';

describe('auth-nif-category.helpers (plan §3.2)', () => {
  describe('parseNifCategory', () => {
    it('parses a valid NIF with category A (personne physique)', () => {
      const result = parseNifCategory('1234567/A/B/C/000');
      expect(result).not.toBeNull();
      expect(result!.category).toBe('A');
      expect(result!.categoryLabelFr).toBe('Personne physique');
    });

    it('parses category D (association) and includes the suggested segment', () => {
      const result = parseNifCategory('1234567/D/B/C/000');
      expect(result).not.toBeNull();
      expect(result!.category).toBe('D');
      expect(result!.categoryLabelFr).toBe('Association');
      expect(result!.suggestedSegmentCode).toBe('association');
    });

    it('normalizes lowercase input (mirrors cleanNifValue)', () => {
      const result = parseNifCategory('1234567/d/b/c/000');
      expect(result).not.toBeNull();
      expect(result!.category).toBe('D');
      expect(result!.categoryLabelFr).toBe('Association');
    });

    it('does not include suggestedSegmentCode for unmapped categories', () => {
      const result = parseNifCategory('1234567/C/B/C/000');
      expect(result).not.toBeNull();
      expect(result!.category).toBe('C');
      expect(result!.suggestedSegmentCode).toBeUndefined();
    });

    it('returns null for an incomplete NIF (still typing)', () => {
      expect(parseNifCategory('1234567/')).toBeNull();
      expect(parseNifCategory('1234')).toBeNull();
    });

    it('returns null for empty/null/undefined input', () => {
      expect(parseNifCategory('')).toBeNull();
      expect(parseNifCategory(null)).toBeNull();
      expect(parseNifCategory(undefined)).toBeNull();
    });

    it('returns null for an invalid format', () => {
      expect(parseNifCategory('invalid-nif')).toBeNull();
      expect(parseNifCategory('1234567/1/B/C/000')).toBeNull();
    });

    it('covers all seven taxpayer categories (A–G)', () => {
      const categories = ['A', 'B', 'C', 'D', 'E', 'F', 'G'] as const;
      for (const cat of categories) {
        const result = parseNifCategory(`1234567/${cat}/B/C/000`);
        expect(result).not.toBeNull();
        expect(result!.category).toBe(cat);
        expect(result!.categoryLabelFr).toBe(NIF_CATEGORY_LABELS_FR[cat]);
      }
    });
  });

  describe('evaluateNifSegmentSuggestion', () => {
    const resolver = (code: string) =>
      ({
        entreprise: 'Entreprise',
        commerce: 'Commerce',
        services: 'Services',
        'btp-construction': 'BTP',
        association: 'Association',
        'etablissement-educatif': 'Établissement éducatif'
      }[code]);

    it('returns the mismatch hint when the NIF says association but segment is commerce', () => {
      const result = evaluateNifSegmentSuggestion('1234567/D/B/C/000', 'commerce', resolver);
      expect(result).not.toBeNull();
      expect(result!.autoSelect).toBeFalse();
      expect(result!.hintMessage).toBe(
        'Votre NIF indique une association ; votre segment est Commerce — confirmer ?'
      );
    });

    it('returns no hint when the segment matches the NIF suggestion', () => {
      const result = evaluateNifSegmentSuggestion('1234567/D/B/C/000', 'association', resolver);
      expect(result).not.toBeNull();
      expect(result!.autoSelect).toBeFalse();
      expect(result!.hintMessage).toBeNull();
    });

    it('returns autoSelect true when no segment is chosen (defensive branch)', () => {
      const result = evaluateNifSegmentSuggestion('1234567/D/B/C/000', '', resolver);
      expect(result).not.toBeNull();
      expect(result!.autoSelect).toBeTrue();
      expect(result!.hintMessage).toBeNull();
    });

    it('returns null when the NIF has no suggested segment (e.g. category C)', () => {
      expect(evaluateNifSegmentSuggestion('1234567/C/B/C/000', 'commerce', resolver)).toBeNull();
    });

    it('returns null for an unparseable NIF', () => {
      expect(evaluateNifSegmentSuggestion('invalid', 'commerce', resolver)).toBeNull();
      expect(evaluateNifSegmentSuggestion('', 'commerce', resolver)).toBeNull();
      expect(evaluateNifSegmentSuggestion(null, 'commerce', resolver)).toBeNull();
    });

    it('uses the raw segment code as label when the resolver returns undefined', () => {
      const result = evaluateNifSegmentSuggestion('1234567/D/B/C/000', 'unknown-seg', () => undefined);
      expect(result).not.toBeNull();
      expect(result!.hintMessage).toContain('unknown-seg');
    });
  });

  describe('NIF_CATEGORY_SUGGESTED_SEGMENT', () => {
    it('only maps category D to the association segment', () => {
      expect(NIF_CATEGORY_SUGGESTED_SEGMENT.D).toBe('association');
      expect(NIF_CATEGORY_SUGGESTED_SEGMENT.A).toBeUndefined();
      expect(NIF_CATEGORY_SUGGESTED_SEGMENT.B).toBeUndefined();
      expect(NIF_CATEGORY_SUGGESTED_SEGMENT.C).toBeUndefined();
      expect(NIF_CATEGORY_SUGGESTED_SEGMENT.E).toBeUndefined();
      expect(NIF_CATEGORY_SUGGESTED_SEGMENT.F).toBeUndefined();
      expect(NIF_CATEGORY_SUGGESTED_SEGMENT.G).toBeUndefined();
    });
  });
});
