export enum SalesReturnNoteStatus {
  Draft = 'Draft',
  Confirmed = 'Confirmed'
}

export function isSalesReturnNoteDraft(status: SalesReturnNoteStatus | string | number): boolean {
  return status === SalesReturnNoteStatus.Draft || status === 'Draft' || status === 0;
}

export interface SalesReturnNoteListDto {
  id: string;
  number: string;
  returnDate: string;
  clientId: string;
  clientName: string;
  deliveryNoteId: string;
  deliveryNoteNumber: string;
  status: SalesReturnNoteStatus;
  statusDisplay: string;
  statusCss: string;
  reason: string;
  lineCount: number;
  totalReturnedQuantity: number;
  totalHT: number;
  totalVAT: number;
  totalTTC: number;
  warehouseId?: string;
  warehouseName?: string;
}

export interface SalesReturnNoteListSummary {
  count: number;
  totalTtc: number;
  totalHt: number;
  totalVat: number;
  confirmedCount: number;
  draftCount: number;
  currency: string;
}

export interface SalesReturnNoteLineDto {
  id: string;
  deliveryNoteLineId: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  designation: string;
  description?: string;
  unit: string;
  unitPriceHT: number;
  vatRatePercent: number;
  discountPercent?: number | null;
  isFodecApplicable: boolean;
  fodecRatePercent: number;
  returnedQuantity: number;
  notes?: string;
  totalHT: number;
  fodecAmount: number;
  totalVAT: number;
  totalTTC: number;
}

export interface SalesReturnNoteDetailDto {
  id: string;
  number: string;
  returnDate: string;
  status: SalesReturnNoteStatus;
  statusDisplay: string;
  statusCss: string;
  clientId: string;
  clientName: string;
  deliveryNoteId: string;
  deliveryNoteNumber: string;
  warehouseId?: string;
  warehouseName?: string;
  reason: string;
  notes?: string;
  confirmedAt?: string;
  totalHT: number;
  totalFodec: number;
  totalVAT: number;
  totalTTC: number;
  totalReturnedQuantity: number;
  lines: SalesReturnNoteLineDto[];
  createdAt: string;
  updatedAt?: string;
}

export interface CreateSalesReturnNoteLineDto {
  deliveryNoteLineId: string;
  returnedQuantity: number;
  notes?: string;
}

export interface CreateSalesReturnNoteDto {
  deliveryNoteId: string;
  returnDate: string;
  reason: string;
  notes?: string;
  lines: CreateSalesReturnNoteLineDto[];
}

export interface UpdateSalesReturnNoteDto {
  returnDate: string;
  reason: string;
  notes?: string;
  lines: CreateSalesReturnNoteLineDto[];
}

export interface EligibleDeliveryNoteDto {
  id: string;
  number: string;
  issueDate: string;
  clientId: string;
  clientName: string;
  statusDisplay: string;
  totalInvoiceableQuantity: number;
  invoiceableLineCount: number;
  warehouseId?: string;
  warehouseName?: string;
}

export interface SalesReturnNotePrefillLineDto {
  deliveryNoteLineId: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  designation: string;
  unit: string;
  unitPriceHT: number;
  deliveredQuantity: number;
  alreadyReturnedQuantity: number;
  invoiceableQuantity: number;
}

export interface SalesReturnNotePrefillDto {
  deliveryNoteId: string;
  deliveryNoteNumber: string;
  clientId: string;
  clientName: string;
  warehouseId?: string;
  warehouseName?: string;
  lines: SalesReturnNotePrefillLineDto[];
}

export interface LinkedSalesReturnNoteDto {
  id: string;
  number: string;
  status: SalesReturnNoteStatus;
  statusDisplay: string;
  returnDate: string;
  totalReturnedQuantity: number;
}

export function clampReturnedQuantity(value: number, max: number): number {
  if (!Number.isFinite(value) || value <= 0) return 0;
  if (!Number.isFinite(max) || max <= 0) return 0;
  return Math.min(value, max);
}

export function canSaveSalesReturnNote(reason: string, quantities: readonly number[]): boolean {
  if (reason.trim().length < 3) return false;
  return quantities.some(q => Number.isFinite(q) && q > 0);
}
