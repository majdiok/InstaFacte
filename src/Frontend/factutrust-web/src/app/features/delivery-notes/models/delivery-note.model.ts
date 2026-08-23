import type { DocumentLineAllocations } from '@core/services/stock.service';

/**
 * Delivery Note enums matching backend DeliveryNoteStatus.
 */
export enum DeliveryNoteStatus {
  Draft = 'Draft',
  Confirmed = 'Confirmed',
  InTransit = 'InTransit',
  Delivered = 'Delivered',
  PartiallyDelivered = 'PartiallyDelivered',
  Failed = 'Failed',
  Cancelled = 'Cancelled',
  Invoiced = 'Invoiced',
  Refused = 'Refused'
}

/**
 * Summary DTO for delivery note list views.
 */
export interface DeliveryNoteListDto {
  id: string;
  number: string;
  issueDate: string;
  deliveryDate?: string;
  clientId: string;
  clientName: string;
  status: DeliveryNoteStatus;
  statusDisplay: string;
  deliveryAddress: string;
  lineCount: number;
  totalOrderedQuantity: number;
  totalDeliveredQuantity: number;
  totalHT: number;
  totalVAT: number;
  totalTTC: number;
  isSigned: boolean;
  invoiceId?: string;
  invoiceNumber?: string;
}

/**
 * DTO for delivery note lines.
 */
export interface DeliveryNoteLineDto {
  id: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  designation: string;
  description?: string;
  unit: string;
  unitPriceHT: number;
  vatRatePercent: number;
  orderedQuantity: number;
  deliveredQuantity: number;
  rejectedQuantity: number;
  rejectionReason?: string;
  pendingQuantity: number;
  totalHT: number;
  totalVAT: number;
  totalTTC: number;
  isFullyDelivered: boolean;
  notes?: string;
  /** Remise de ligne en % (null = aucune). Reprise telle quelle sur la facture générée. */
  discountPercent?: number | null;
  /** Montant de la remise sur la quantité commandée. */
  discountAmount: number;
  /** Assujettissement FODEC figé à la création de la ligne. */
  isFodecApplicable: boolean;
  fodecRatePercent: number;
  /** FODEC sur la quantité commandée (assiette : HT après remise). */
  fodecAmount: number;
  /** Quantité déjà retournée via des bons de retour confirmés. */
  returnedQuantity?: number;
  /** Quantité encore facturable (livré − retourné). */
  invoiceableQuantity?: number;
}

/**
 * Detailed DTO for delivery note detail view.
 */
export interface DeliveryNoteDetailDto {
  id: string;
  number: string;
  issueDate: string;
  deliveryDate?: string;
  status: DeliveryNoteStatus;
  statusDisplay: string;
  clientId: string;
  clientName: string;
  clientEmail?: string;
  reference?: string;
  notes?: string;
  deliveryAddress: string;
  deliveryCity?: string;
  deliveryPostalCode?: string;
  recipientName?: string;
  signedAt?: string;
  failureReason?: string;
  failedAt?: string;
  cancellationReason?: string;
  cancelledAt?: string;
  allowGroupInvoicing: boolean;
  invoiceId?: string;
  invoiceNumber?: string;
  invoicedAt?: string;
  totalHT: number;
  totalVAT: number;
  totalTTC: number;
  lines: DeliveryNoteLineDto[];
  createdAt: string;
  updatedAt?: string;
  warehouseId?: string;
  warehouseName?: string;
  totalReturnedQuantity?: number;
  totalInvoiceableQuantity?: number;
  hasInvoiceableQuantity?: boolean;
  returnNotes?: DeliveryNoteLinkedReturnNoteDto[];
}

export interface DeliveryNoteLinkedReturnNoteDto {
  id: string;
  number: string;
  status: string;
  statusDisplay: string;
  returnDate: string;
  totalReturnedQuantity: number;
}

/**
 * DTO for creating a delivery note line.
 */
export interface CreateDeliveryNoteLineDto {
  productId: string;
  orderedQuantity: number;
  notes?: string;
  /** Remise de ligne en % (0–100). Omise = aucune remise. */
  discountPercent?: number | null;
}

/**
 * DTO for creating a new delivery note.
 */
export interface CreateDeliveryNoteDto {
  clientId: string;
  issueDate: string;
  deliveryAddress: string;
  deliveryCity?: string;
  deliveryPostalCode?: string;
  reference?: string;
  notes?: string;
  allowGroupInvoicing: boolean;
  warehouseId?: string | null;
  lines: CreateDeliveryNoteLineDto[];
}

/**
 * DTO for recording delivery quantities per line.
 */
export interface RecordDeliveryLineDto {
  lineId: string;
  deliveredQuantity: number;
  rejectedQuantity: number;
  rejectionReason?: string;
}

/**
 * DTO for recording delivery completion.
 */
export interface RecordDeliveryDto {
  deliveryDate: string;
  recipientName: string;
  recipientSignature?: string;
  lines?: RecordDeliveryLineDto[];
  lineAllocations?: DocumentLineAllocations[];
}

/**
 * Request model for cancellation.
 */
export interface CancelDeliveryNoteRequest {
  reason: string;
}

/**
 * DTO for generating an invoice from a delivery note.
 */
export interface GenerateInvoiceFromDeliveryNoteDto {
  issueDate?: string;
  dueDate?: string;
  reference?: string;
  notes?: string;
}

export function isDeliveryNoteEligibleForReturn(status: DeliveryNoteStatus): boolean {
  return status === DeliveryNoteStatus.Delivered || status === DeliveryNoteStatus.PartiallyDelivered;
}

export function canGenerateInvoiceFromDeliveryNote(note: {
  status: DeliveryNoteStatus;
  invoiceId?: string;
  hasInvoiceableQuantity?: boolean;
}): boolean {
  return isDeliveryNoteEligibleForReturn(note.status)
    && !note.invoiceId
    && note.hasInvoiceableQuantity !== false;
}

export function canCreateReturnNoteFromDeliveryNote(note: {
  status: DeliveryNoteStatus;
  invoiceId?: string;
  hasInvoiceableQuantity?: boolean;
}): boolean {
  return canGenerateInvoiceFromDeliveryNote(note);
}

