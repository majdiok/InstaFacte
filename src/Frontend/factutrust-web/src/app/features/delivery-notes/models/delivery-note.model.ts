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
}

/**
 * DTO for creating a delivery note line.
 */
export interface CreateDeliveryNoteLineDto {
  productId: string;
  orderedQuantity: number;
  notes?: string;
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

