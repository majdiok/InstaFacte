import {
  NumberingBlockType,
  NumberingDocumentType,
  findSchemeForType,
  getDocumentTypeForTabIndex,
  normalizeNumberingScheme,
  parseNumberingBlockType,
  parseNumberingDocumentType
} from './numbering.models';

describe('numbering.models', () => {
  describe('parseNumberingDocumentType', () => {
    it('accepts numeric enum values', () => {
      expect(parseNumberingDocumentType(2)).toBe(NumberingDocumentType.Quote);
      expect(parseNumberingDocumentType(0)).toBe(NumberingDocumentType.Invoice);
    });

    it('accepts PascalCase API strings', () => {
      expect(parseNumberingDocumentType('Quote')).toBe(NumberingDocumentType.Quote);
      expect(parseNumberingDocumentType('CreditNote')).toBe(NumberingDocumentType.CreditNote);
      expect(parseNumberingDocumentType('DeliveryNote')).toBe(NumberingDocumentType.DeliveryNote);
      expect(parseNumberingDocumentType('BankDeposit')).toBe(NumberingDocumentType.BankDeposit);
    });

    it('falls back to Invoice for unknown values', () => {
      expect(parseNumberingDocumentType('Unknown')).toBe(NumberingDocumentType.Invoice);
      expect(parseNumberingDocumentType(null)).toBe(NumberingDocumentType.Invoice);
      expect(parseNumberingDocumentType(undefined)).toBe(NumberingDocumentType.Invoice);
    });
  });

  describe('parseNumberingBlockType', () => {
    it('accepts numeric enum values', () => {
      expect(parseNumberingBlockType(0)).toBe(NumberingBlockType.FreeText);
      expect(parseNumberingBlockType(9)).toBe(NumberingBlockType.Year4);
    });

    it('accepts PascalCase API strings', () => {
      expect(parseNumberingBlockType('FreeText')).toBe(NumberingBlockType.FreeText);
      expect(parseNumberingBlockType('DocumentNumberPadded6')).toBe(
        NumberingBlockType.DocumentNumberPadded6
      );
    });

    it('falls back to FreeText for unknown values', () => {
      expect(parseNumberingBlockType('Invalid')).toBe(NumberingBlockType.FreeText);
    });
  });

  describe('normalizeNumberingScheme', () => {
    it('converts API string enums to numeric enums', () => {
      const normalized = normalizeNumberingScheme({
        documentType: 'Quote' as unknown as NumberingDocumentType,
        documentTypeDisplay: 'Devis',
        fiscalYear: 2026,
        startNumber: 1,
        currentSequence: 0,
        nextSequence: 1,
        minimumStartNumber: 1,
        hasIssuedDocuments: false,
        blocks: [
          {
            type: 'FreeText' as unknown as NumberingBlockType,
            value: 'DEV',
            order: 0,
            label: 'Texte libre'
          }
        ],
        isFormatLocked: false,
        examplePreview: 'DEV-2026-000001'
      });

      expect(normalized.documentType).toBe(NumberingDocumentType.Quote);
      expect(normalized.blocks[0].type).toBe(NumberingBlockType.FreeText);
    });
  });

  describe('findSchemeForType', () => {
    const schemes = [
      normalizeNumberingScheme({
        documentType: NumberingDocumentType.Invoice,
        documentTypeDisplay: 'Facture',
        fiscalYear: 2026,
        startNumber: 1,
        currentSequence: 0,
        nextSequence: 1,
        minimumStartNumber: 1,
        hasIssuedDocuments: false,
        blocks: [],
        isFormatLocked: false,
        examplePreview: 'FAC-2026-000001'
      }),
      normalizeNumberingScheme({
        documentType: NumberingDocumentType.Quote,
        documentTypeDisplay: 'Devis',
        fiscalYear: 2026,
        startNumber: 1,
        currentSequence: 0,
        nextSequence: 1,
        minimumStartNumber: 1,
        hasIssuedDocuments: false,
        blocks: [
          {
            type: NumberingBlockType.FreeText,
            value: 'DEV',
            order: 0,
            label: 'Texte libre'
          }
        ],
        isFormatLocked: false,
        examplePreview: 'DEV-2026-000001'
      })
    ];

    it('finds scheme by document type', () => {
      const quote = findSchemeForType(schemes, NumberingDocumentType.Quote);
      expect(quote?.blocks[0].value).toBe('DEV');
    });

    it('returns undefined when type is missing', () => {
      expect(findSchemeForType(schemes, NumberingDocumentType.BankDeposit)).toBeUndefined();
    });
  });

  describe('getDocumentTypeForTabIndex', () => {
    it('maps tab index to document type', () => {
      expect(getDocumentTypeForTabIndex(0)).toBe(NumberingDocumentType.Invoice);
      expect(getDocumentTypeForTabIndex(2)).toBe(NumberingDocumentType.Quote);
      expect(getDocumentTypeForTabIndex(3)).toBe(NumberingDocumentType.DeliveryNote);
    });

    it('falls back to Invoice for invalid index', () => {
      expect(getDocumentTypeForTabIndex(-1)).toBe(NumberingDocumentType.Invoice);
      expect(getDocumentTypeForTabIndex(99)).toBe(NumberingDocumentType.Invoice);
    });
  });
});
