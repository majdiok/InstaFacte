export enum NumberingDocumentType {
  Invoice = 0,
  CreditNote = 1,
  Quote = 2,
  DeliveryNote = 3,
  PurchaseOrder = 4,
  StockTransfer = 5,
  PhysicalInventory = 6,
  CashReceipt = 7,
  CashExpense = 8,
  BankDeposit = 9
}

export enum NumberingBlockType {
  FreeText = 0,
  Separator = 1,
  DocumentNumber = 2,
  DocumentNumberPadded3 = 3,
  DocumentNumberPadded4 = 4,
  DocumentNumberPadded5 = 5,
  DocumentNumberPadded6 = 6,
  Day = 7,
  Month = 8,
  Year4 = 9,
  Year2 = 10
}

export interface NumberingBlock {
  type: NumberingBlockType;
  value?: string | null;
  order: number;
  label?: string;
}

export interface NumberingScheme {
  documentType: NumberingDocumentType;
  documentTypeDisplay: string;
  fiscalYear: number;
  startNumber: number;
  currentSequence: number;
  nextSequence: number;
  minimumStartNumber: number;
  hasIssuedDocuments: boolean;
  blocks: NumberingBlock[];
  isFormatLocked: boolean;
  examplePreview: string;
}

export interface SaveNumberingSchemeRequest {
  startNumber: number;
  blocks: NumberingBlock[];
}

export interface PreviewNumberingRequest {
  startNumber: number;
  blocks: NumberingBlock[];
  fiscalYear?: number;
}

export interface PreviewNumberingResponse {
  example: string;
}

export const BLOCK_TYPE_LABELS: Record<NumberingBlockType, string> = {
  [NumberingBlockType.FreeText]: 'Texte libre',
  [NumberingBlockType.Separator]: 'Séparateur',
  [NumberingBlockType.DocumentNumber]: 'Numéro de document',
  [NumberingBlockType.DocumentNumberPadded3]: 'Numéro de document à 3 chiffres',
  [NumberingBlockType.DocumentNumberPadded4]: 'Numéro de document à 4 chiffres',
  [NumberingBlockType.DocumentNumberPadded5]: 'Numéro de document à 5 chiffres',
  [NumberingBlockType.DocumentNumberPadded6]: 'Numéro de document à 6 chiffres',
  [NumberingBlockType.Day]: 'Jour (JJ)',
  [NumberingBlockType.Month]: 'Mois (MM)',
  [NumberingBlockType.Year4]: 'Année (AAAA)',
  [NumberingBlockType.Year2]: 'Année (AA)'
};

export interface PaletteBlockTemplate {
  type: NumberingBlockType;
  value?: string | null;
}

export const PALETTE_BLOCKS: readonly PaletteBlockTemplate[] = [
  { type: NumberingBlockType.FreeText, value: '' },
  { type: NumberingBlockType.Separator, value: '-' },
  { type: NumberingBlockType.Separator, value: '/' },
  { type: NumberingBlockType.DocumentNumber, value: null },
  { type: NumberingBlockType.DocumentNumberPadded3, value: null },
  { type: NumberingBlockType.DocumentNumberPadded4, value: null },
  { type: NumberingBlockType.DocumentNumberPadded5, value: null },
  { type: NumberingBlockType.DocumentNumberPadded6, value: null },
  { type: NumberingBlockType.Day, value: null },
  { type: NumberingBlockType.Month, value: null },
  { type: NumberingBlockType.Year4, value: null },
  { type: NumberingBlockType.Year2, value: null }
];

export interface NumberingDocumentTab {
  documentType: NumberingDocumentType;
  label: string;
  shortLabel: string;
  icon: string;
}

export const DOCUMENT_TYPE_TABS: readonly NumberingDocumentTab[] = [
  { documentType: NumberingDocumentType.Invoice, label: 'Facture', shortLabel: 'FAC', icon: 'pi pi-file' },
  { documentType: NumberingDocumentType.CreditNote, label: 'Facture avoir', shortLabel: 'AVO', icon: 'pi pi-replay' },
  { documentType: NumberingDocumentType.Quote, label: 'Devis', shortLabel: 'DEV', icon: 'pi pi-file-edit' },
  { documentType: NumberingDocumentType.DeliveryNote, label: 'Bon de livraison', shortLabel: 'BL', icon: 'pi pi-truck' },
  { documentType: NumberingDocumentType.PurchaseOrder, label: 'Bon de commande', shortLabel: 'BC', icon: 'pi pi-shopping-cart' },
  { documentType: NumberingDocumentType.StockTransfer, label: 'Transfert stock', shortLabel: 'TR', icon: 'pi pi-arrows-h' },
  { documentType: NumberingDocumentType.PhysicalInventory, label: 'Inventaire physique', shortLabel: 'INVE', icon: 'pi pi-box' },
  { documentType: NumberingDocumentType.CashReceipt, label: 'Encaissement caisse', shortLabel: 'ENC', icon: 'pi pi-wallet' },
  { documentType: NumberingDocumentType.CashExpense, label: 'Décaissement caisse', shortLabel: 'DEP', icon: 'pi pi-money-bill' },
  { documentType: NumberingDocumentType.BankDeposit, label: 'Remise bancaire', shortLabel: 'REM', icon: 'pi pi-building-columns' }
];

export const DEFAULT_FREE_TEXT: Record<NumberingDocumentType, string> = {
  [NumberingDocumentType.Invoice]: 'FAC',
  [NumberingDocumentType.CreditNote]: 'AVO',
  [NumberingDocumentType.Quote]: 'DEV',
  [NumberingDocumentType.DeliveryNote]: 'BL',
  [NumberingDocumentType.PurchaseOrder]: 'BC',
  [NumberingDocumentType.StockTransfer]: 'TR',
  [NumberingDocumentType.PhysicalInventory]: 'INVE',
  [NumberingDocumentType.CashReceipt]: 'ENC',
  [NumberingDocumentType.CashExpense]: 'DEP',
  [NumberingDocumentType.BankDeposit]: 'REM'
};

export function isDocumentNumberBlock(type: NumberingBlockType): boolean {
  return (
    type === NumberingBlockType.DocumentNumber ||
    type === NumberingBlockType.DocumentNumberPadded3 ||
    type === NumberingBlockType.DocumentNumberPadded4 ||
    type === NumberingBlockType.DocumentNumberPadded5 ||
    type === NumberingBlockType.DocumentNumberPadded6
  );
}

export function cloneBlock(block: NumberingBlock, order: number): NumberingBlock {
  return {
    type: block.type,
    value: block.value,
    order,
    label: block.label ?? BLOCK_TYPE_LABELS[block.type]
  };
}

/**
 * L'API sérialise `NumberingDocumentType` en chaîne PascalCase (`JsonStringEnumConverter`),
 * alors que le front utilise l'enum numérique. Sans normalisation, `scheme.documentType === docType` échoue.
 */
const DOCUMENT_TYPE_BY_STRING: Record<string, NumberingDocumentType> = {
  Invoice: NumberingDocumentType.Invoice,
  CreditNote: NumberingDocumentType.CreditNote,
  Quote: NumberingDocumentType.Quote,
  DeliveryNote: NumberingDocumentType.DeliveryNote,
  PurchaseOrder: NumberingDocumentType.PurchaseOrder,
  StockTransfer: NumberingDocumentType.StockTransfer,
  PhysicalInventory: NumberingDocumentType.PhysicalInventory,
  CashReceipt: NumberingDocumentType.CashReceipt,
  CashExpense: NumberingDocumentType.CashExpense,
  BankDeposit: NumberingDocumentType.BankDeposit
};

const BLOCK_TYPE_BY_STRING: Record<string, NumberingBlockType> = {
  FreeText: NumberingBlockType.FreeText,
  Separator: NumberingBlockType.Separator,
  DocumentNumber: NumberingBlockType.DocumentNumber,
  DocumentNumberPadded3: NumberingBlockType.DocumentNumberPadded3,
  DocumentNumberPadded4: NumberingBlockType.DocumentNumberPadded4,
  DocumentNumberPadded5: NumberingBlockType.DocumentNumberPadded5,
  DocumentNumberPadded6: NumberingBlockType.DocumentNumberPadded6,
  Day: NumberingBlockType.Day,
  Month: NumberingBlockType.Month,
  Year4: NumberingBlockType.Year4,
  Year2: NumberingBlockType.Year2
};

export function parseNumberingDocumentType(raw: unknown): NumberingDocumentType {
  if (typeof raw === 'number' && Number.isInteger(raw) && raw in DEFAULT_FREE_TEXT) {
    return raw as NumberingDocumentType;
  }
  if (typeof raw === 'string') {
    const value = DOCUMENT_TYPE_BY_STRING[raw];
    if (value !== undefined) {
      return value;
    }
  }
  return NumberingDocumentType.Invoice;
}

export function parseNumberingBlockType(raw: unknown): NumberingBlockType {
  if (typeof raw === 'number' && Number.isInteger(raw) && raw in BLOCK_TYPE_LABELS) {
    return raw as NumberingBlockType;
  }
  if (typeof raw === 'string') {
    const value = BLOCK_TYPE_BY_STRING[raw];
    if (value !== undefined) {
      return value;
    }
  }
  return NumberingBlockType.FreeText;
}

export function normalizeNumberingBlock(block: NumberingBlock): NumberingBlock {
  const type = parseNumberingBlockType(block.type);
  return {
    ...block,
    type,
    label: block.label ?? BLOCK_TYPE_LABELS[type]
  };
}

export function normalizeNumberingScheme(scheme: NumberingScheme): NumberingScheme {
  const documentType = parseNumberingDocumentType(scheme.documentType);
  return {
    ...scheme,
    documentType,
    blocks: scheme.blocks.map((block, index) =>
      normalizeNumberingBlock({
        ...block,
        order: block.order ?? index
      })
    )
  };
}

export function findSchemeForType(
  schemes: readonly NumberingScheme[],
  documentType: NumberingDocumentType
): NumberingScheme | undefined {
  return schemes.find((s) => s.documentType === documentType);
}

export function getDocumentTypeForTabIndex(index: number): NumberingDocumentType {
  return DOCUMENT_TYPE_TABS[index]?.documentType ?? NumberingDocumentType.Invoice;
}

/** Default format blocks mirroring backend NumberingSchemeDefaults. */
export function getDefaultBlocksForType(documentType: NumberingDocumentType): NumberingBlock[] {
  const prefix = DEFAULT_FREE_TEXT[documentType];

  if (documentType === NumberingDocumentType.PhysicalInventory) {
    return [
      { type: NumberingBlockType.FreeText, value: prefix, order: 0, label: BLOCK_TYPE_LABELS[NumberingBlockType.FreeText] },
      { type: NumberingBlockType.Separator, value: '-', order: 1, label: BLOCK_TYPE_LABELS[NumberingBlockType.Separator] },
      { type: NumberingBlockType.DocumentNumberPadded6, value: null, order: 2, label: BLOCK_TYPE_LABELS[NumberingBlockType.DocumentNumberPadded6] }
    ];
  }

  return [
    { type: NumberingBlockType.FreeText, value: prefix, order: 0, label: BLOCK_TYPE_LABELS[NumberingBlockType.FreeText] },
    { type: NumberingBlockType.Separator, value: '-', order: 1, label: BLOCK_TYPE_LABELS[NumberingBlockType.Separator] },
    { type: NumberingBlockType.Year4, value: null, order: 2, label: BLOCK_TYPE_LABELS[NumberingBlockType.Year4] },
    { type: NumberingBlockType.Separator, value: '-', order: 3, label: BLOCK_TYPE_LABELS[NumberingBlockType.Separator] },
    { type: NumberingBlockType.DocumentNumberPadded6, value: null, order: 4, label: BLOCK_TYPE_LABELS[NumberingBlockType.DocumentNumberPadded6] }
  ];
}
