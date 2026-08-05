import { CreateManualJournalEntryRequest } from '../../services/accounting.service';

/** Tiers auxiliaire (kind : 1 = client, 2 = fournisseur). */
export interface ThirdPartyRef {
  id: string;
  kind: number;
  name: string;
  display: string;
}

export interface EntryLine {
  /** Identité stable côté UI (table track / focus). Non envoyé à l'API. */
  clientLineId: string;
  accountNumber: string;
  lineLabel: string;
  debit: number | null;
  credit: number | null;
  thirdParty: ThirdPartyRef | null;
  /** Aide à la saisie — non persisté. */
  pieceRef?: string | null;
  /** Aide à la saisie — non persisté (yyyy-MM-dd). */
  dueDate?: string | null;
  /** Taux TVA % sélectionné — aide à la saisie. */
  vatRatePercent?: number | null;
  /** Ligne générée automatiquement par l'assistance TVA. */
  isVatGenerated?: boolean;
  /** ID interne pour liaison TVA base ↔ ligne générée. */
  vatLinkId?: string | null;
}

export function newClientLineId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `line-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/** Garantit un clientLineId (brouillons / templates anciens). */
export function ensureClientLineId(line: EntryLine): EntryLine {
  if (line.clientLineId) {
    return line;
  }
  return { ...line, clientLineId: newClientLineId() };
}

export type EntryTabId = 'standard' | 'guided' | 'template' | 'recurring';

export interface ColumnVisibility {
  piece: boolean;
  dueDate: boolean;
  lettering: boolean;
  vat: boolean;
}

export const DEFAULT_COLUMN_VISIBILITY: ColumnVisibility = {
  piece: false,
  dueDate: false,
  lettering: false,
  vat: true
};

export interface EntryHeaderState {
  journalCode: string;
  entryDate: string;
  entryLabel: string;
  pieceRef: string;
  pieceDate: string;
  periodId: string | null;
  description: string;
  currency: string;
  dueDate: string;
  paymentMethod: number | null;
  bankAccountId: string | null;
  amountTtc: number | null;
  amountHt: number | null;
  amountVat: number | null;
}

export type LineStatus = 'empty' | 'valid' | 'unknown' | 'inactive';

export interface EntryTotals {
  debit: number;
  credit: number;
}

export interface EntryValidationResult {
  valid: boolean;
  error?: string;
}

export interface BuildRequestResult {
  request: CreateManualJournalEntryRequest;
  journalCode: string;
  entryDate: string;
}

export function createEmptyLine(): EntryLine {
  return {
    clientLineId: newClientLineId(),
    accountNumber: '',
    lineLabel: '',
    debit: null,
    credit: null,
    thirdParty: null,
    pieceRef: null,
    dueDate: null,
    vatRatePercent: null,
    isVatGenerated: false,
    vatLinkId: null
  };
}

export function createDefaultLines(count = 2): EntryLine[] {
  return Array.from({ length: count }, () => createEmptyLine());
}
