import {
  DEFAULT_FREE_TEXT,
  NumberingBlockType,
  NumberingDocumentType,
  findSchemeForType,
  getDefaultBlocksForType,
  getDocumentTypeForTabIndex,
  normalizeNumberingScheme,
  type NumberingScheme
} from './models/numbering.models';

function resolveBlocksForTab(
  schemes: readonly NumberingScheme[],
  tabIndex: number
): { documentType: NumberingDocumentType; freeText: string } {
  const documentType = getDocumentTypeForTabIndex(tabIndex);
  const scheme = findSchemeForType(schemes, documentType);
  const blocks = scheme?.blocks ?? getDefaultBlocksForType(documentType);
  const freeText =
    blocks.find((b) => b.type === NumberingBlockType.FreeText)?.value ??
    DEFAULT_FREE_TEXT[documentType];
  return { documentType, freeText };
}

describe('numbering tab state', () => {
  const apiSchemes = [
    {
      documentType: 'Invoice',
      documentTypeDisplay: 'Facture',
      fiscalYear: 2026,
      startNumber: 1,
      currentSequence: 0,
      nextSequence: 1,
      minimumStartNumber: 1,
      hasIssuedDocuments: false,
      blocks: [{ type: 'FreeText', value: 'FAC', order: 0, label: 'Texte libre' }],
      isFormatLocked: false,
      examplePreview: 'FAC-2026-000001'
    },
    {
      documentType: 'Quote',
      documentTypeDisplay: 'Devis',
      fiscalYear: 2026,
      startNumber: 1,
      currentSequence: 0,
      nextSequence: 1,
      minimumStartNumber: 1,
      hasIssuedDocuments: false,
      blocks: [{ type: 'FreeText', value: 'DEV', order: 0, label: 'Texte libre' }],
      isFormatLocked: false,
      examplePreview: 'DEV-2026-000001'
    },
    {
      documentType: 'DeliveryNote',
      documentTypeDisplay: 'Bon de livraison',
      fiscalYear: 2026,
      startNumber: 1,
      currentSequence: 0,
      nextSequence: 1,
      minimumStartNumber: 1,
      hasIssuedDocuments: false,
      blocks: [{ type: 'FreeText', value: 'BL', order: 0, label: 'Texte libre' }],
      isFormatLocked: false,
      examplePreview: 'BL-2026-000001'
    }
  ].map((scheme) => normalizeNumberingScheme(scheme as never));

  it('resolves Quote (tab 2) instead of Invoice when tab index changes', () => {
    const invoice = resolveBlocksForTab(apiSchemes, 0);
    const quote = resolveBlocksForTab(apiSchemes, 2);

    expect(invoice.documentType).toBe(NumberingDocumentType.Invoice);
    expect(invoice.freeText).toBe('FAC');
    expect(quote.documentType).toBe(NumberingDocumentType.Quote);
    expect(quote.freeText).toBe('DEV');
  });

  it('resolves DeliveryNote (tab 3) with BL prefix', () => {
    const deliveryNote = resolveBlocksForTab(apiSchemes, 3);
    expect(deliveryNote.documentType).toBe(NumberingDocumentType.DeliveryNote);
    expect(deliveryNote.freeText).toBe('BL');
  });

  it('uses default blocks when scheme is absent for tab type', () => {
    const bankDeposit = resolveBlocksForTab(apiSchemes, 9);
    expect(bankDeposit.documentType).toBe(NumberingDocumentType.BankDeposit);
    expect(bankDeposit.freeText).toBe('REM');
  });

  it('maps each tab index to the correct save target document type', () => {
    const expectedTypes = [
      NumberingDocumentType.Invoice,
      NumberingDocumentType.CreditNote,
      NumberingDocumentType.Quote,
      NumberingDocumentType.DeliveryNote,
      NumberingDocumentType.PurchaseOrder,
      NumberingDocumentType.StockTransfer,
      NumberingDocumentType.PhysicalInventory,
      NumberingDocumentType.CashReceipt,
      NumberingDocumentType.CashExpense,
      NumberingDocumentType.BankDeposit
    ];

    for (let index = 0; index < expectedTypes.length; index++) {
      expect(getDocumentTypeForTabIndex(index)).toBe(expectedTypes[index]);
    }
  });
});
