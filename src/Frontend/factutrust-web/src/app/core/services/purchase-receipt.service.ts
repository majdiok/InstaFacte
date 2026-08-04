import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { ApiResponse, PagedResult } from './client.service';
import {
  CreateSupplierInvoiceLineRequest,
  LinkedSupplierInvoiceSummary,
  SupplierInvoicePrefill,
  SupplierInvoiceLineAssetClassification
} from './purchase-order.service';

export enum PurchaseReceiptStatus {
  Draft = 0,
  Validated = 1,
  Cancelled = 2,
  PartiallyInvoiced = 3,
  Invoiced = 4
}

export interface PurchaseReceiptListItem {
  id: string;
  number: string;
  supplierName: string;
  supplierId: string;
  receiptDate: string;
  supplierReference: string | null;
  status: PurchaseReceiptStatus;
  statusDisplay: string;
  statusCss: string;
  totalHT: number;
  totalTTC: number;
  lineCount: number;
  warehouseId: string;
  warehouseName: string | null;
  purchaseOrderId: string | null;
  purchaseOrderNumber: string | null;
  isPartialRelativeToOrdered: boolean;
}

export interface PurchaseReceiptListSummary {
  count: number;
  totalTtc: number;
  totalHt: number;
  totalVat: number;
  validatedCount: number;
  draftCount: number;
  currency: string;
}

export interface PurchaseReceiptSupplierSummary {
  id: string;
  name: string;
  nif: string | null;
  email: string;
  address: string;
}

export interface PurchaseReceiptLine {
  id: string;
  lineNumber: number;
  purchaseOrderLineId: string | null;
  productId: string;
  productCode: string;
  productName: string;
  productDescription: string | null;
  orderedQuantity: number;
  receivedQuantity: number;
  invoicedQuantity?: number;
  receivedNotInvoicedQuantity?: number;
  unit: string | null;
  unitPriceHT: number;
  discountPercent: number | null;
  vatRateDisplay: string;
  subTotal: number;
  vatAmount: number;
  total: number;
}

export interface PurchaseReceiptAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
  uploadedBy: string | null;
}

export interface PurchaseReceiptDetail {
  id: string;
  number: string;
  receiptDate: string;
  status: PurchaseReceiptStatus;
  statusDisplay: string;
  statusCss: string;
  supplierReference: string | null;
  transporterName: string | null;
  deliveryNoteNumber: string | null;
  notes: string | null;
  supplier: PurchaseReceiptSupplierSummary;
  warehouseId: string;
  warehouseName: string | null;
  purchaseOrderId: string | null;
  purchaseOrderNumber: string | null;
  lines: PurchaseReceiptLine[];
  attachments: PurchaseReceiptAttachment[];
  subTotal: number;
  totalVat: number;
  totalTTC: number;
  isPartialRelativeToOrdered: boolean;
  totalReceivedNotInvoicedQuantity?: number;
  hasReceivedNotInvoiced?: boolean;
  linkedSupplierInvoices?: LinkedSupplierInvoiceSummary[];
  validatedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  createdAt: string;
}

export interface PurchaseReceiptDetailApiDto extends Omit<PurchaseReceiptDetail, 'status'> {
  status: PurchaseReceiptStatus | string;
}

export interface PurchaseReceiptPrefillLine {
  purchaseOrderLineId: string;
  productId: string;
  productCode: string;
  productName: string;
  productDescription: string | null;
  unit: string | null;
  orderedQuantity: number;
  alreadyReceivedQuantity: number;
  pendingQuantity: number;
  unitPriceHT: number;
}

export interface PurchaseReceiptPrefill {
  purchaseOrderId: string;
  purchaseOrderNumber: string;
  supplierId: string;
  supplierName: string;
  warehouseId: string | null;
  warehouseName: string | null;
  lines: PurchaseReceiptPrefillLine[];
}

export interface CreatePurchaseReceiptLineRequest {
  productId: string;
  receivedQuantity: number;
  unitPriceHT?: number | null;
  orderedQuantity: number;
  purchaseOrderLineId?: string | null;
  discountPercent?: number | null;
}

export interface CreatePurchaseReceiptRequest {
  supplierId: string;
  warehouseId: string;
  receiptDate: string;
  purchaseOrderId?: string | null;
  supplierReference?: string | null;
  transporterName?: string | null;
  deliveryNoteNumber?: string | null;
  notes?: string | null;
  lines: CreatePurchaseReceiptLineRequest[];
}

export interface UpdatePurchaseReceiptRequest {
  receiptDate: string;
  warehouseId: string;
  supplierReference?: string | null;
  transporterName?: string | null;
  deliveryNoteNumber?: string | null;
  notes?: string | null;
  lines: CreatePurchaseReceiptLineRequest[];
}

export interface PurchaseReceiptSearchParams {
  search?: string;
  status?: PurchaseReceiptStatus;
  supplierId?: string;
  purchaseOrderId?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export interface CancelPurchaseReceiptRequest {
  reason: string;
}

@Injectable({
  providedIn: 'root'
})
export class PurchaseReceiptService {
  private readonly API_URL = `${environment.apiUrl}/purchasereceipts`;
  private http = inject(HttpClient);

  getPurchaseReceipts(
    params: PurchaseReceiptSearchParams = {}
  ): Observable<ApiResponse<PagedResult<PurchaseReceiptListItem>>> {
    return this.http.get<ApiResponse<PagedResult<PurchaseReceiptListItem>>>(this.API_URL, {
      params: this.buildListParams(params)
    });
  }

  getPurchaseReceiptsSummary(
    params: PurchaseReceiptSearchParams = {}
  ): Observable<ApiResponse<PurchaseReceiptListSummary>> {
    let httpParams = new HttpParams();
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.status !== undefined) httpParams = httpParams.set('status', params.status.toString());
    if (params.supplierId) httpParams = httpParams.set('supplierId', params.supplierId);
    if (params.purchaseOrderId) httpParams = httpParams.set('purchaseOrderId', params.purchaseOrderId);
    if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
    return this.http.get<ApiResponse<PurchaseReceiptListSummary>>(`${this.API_URL}/summary`, {
      params: httpParams
    });
  }

  getPurchaseReceipt(id: string): Observable<ApiResponse<PurchaseReceiptDetailApiDto>> {
    return this.http.get<ApiResponse<PurchaseReceiptDetailApiDto>>(`${this.API_URL}/${id}`);
  }

  prefillFromPurchaseOrder(purchaseOrderId: string): Observable<ApiResponse<PurchaseReceiptPrefill>> {
    return this.http.get<ApiResponse<PurchaseReceiptPrefill>>(
      `${this.API_URL}/prefill-from-po/${purchaseOrderId}`
    );
  }

  createPurchaseReceipt(request: CreatePurchaseReceiptRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(this.API_URL, request);
  }

  updatePurchaseReceipt(id: string, request: UpdatePurchaseReceiptRequest): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(`${this.API_URL}/${id}`, request);
  }

  validatePurchaseReceipt(id: string): Observable<ApiResponse<object>> {
    return this.http.patch<ApiResponse<object>>(`${this.API_URL}/${id}/validate`, {});
  }

  cancelPurchaseReceipt(id: string, request: CancelPurchaseReceiptRequest): Observable<ApiResponse<object>> {
    return this.http.patch<ApiResponse<object>>(`${this.API_URL}/${id}/cancel`, request);
  }

  deletePurchaseReceipt(id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.API_URL}/${id}`);
  }

  downloadPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.API_URL}/${id}/pdf`, { responseType: 'blob' });
  }

  getAttachments(id: string): Observable<ApiResponse<PurchaseReceiptAttachment[]>> {
    return this.http.get<ApiResponse<PurchaseReceiptAttachment[]>>(`${this.API_URL}/${id}/attachments`);
  }

  uploadAttachment(id: string, file: File): Observable<ApiResponse<string>> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ApiResponse<string>>(`${this.API_URL}/${id}/attachments`, form);
  }

  deleteAttachment(id: string, attachmentId: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.API_URL}/${id}/attachments/${attachmentId}`);
  }

  getSupplierInvoicePrefill(id: string): Observable<ApiResponse<SupplierInvoicePrefill>> {
    return this.http.get<ApiResponse<SupplierInvoicePrefill>>(`${this.API_URL}/${id}/supplier-invoice-prefill`);
  }

  createSupplierInvoice(
    id: string,
    request: CreateSupplierInvoiceFromReceiptRequest
  ): Observable<ApiResponse<SupplierInvoiceCreationResponse>> {
    // Le composant appelant gère lui-même l'affichage des erreurs (dont la reprise
    // automatique sur 409) : on neutralise le toast global pour éviter un doublon.
    return this.http.post<ApiResponse<SupplierInvoiceCreationResponse>>(
      `${this.API_URL}/${id}/create-supplier-invoice`,
      request,
      { context: createHttpContextSkipGlobalErrorUi() }
    );
  }

  private buildListParams(params: PurchaseReceiptSearchParams): HttpParams {
    let httpParams = new HttpParams();
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.status !== undefined) httpParams = httpParams.set('status', params.status.toString());
    if (params.supplierId) httpParams = httpParams.set('supplierId', params.supplierId);
    if (params.purchaseOrderId) httpParams = httpParams.set('purchaseOrderId', params.purchaseOrderId);
    if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return httpParams;
  }
}

/**
 * Response returned by POST /purchasereceipts/{id}/create-supplier-invoice (and the PO sibling).
 * `invoiceNumber` is the number actually persisted — may differ from the caller's input when
 * the server auto-resolved (useSuggestedNumber) or retried after a concurrent duplicate.
 * Also emitted by POST /purchaseorders/{id}/create-supplier-invoice — shared shape.
 */
export interface SupplierInvoiceCreationResponse {
  id: string;
  invoiceNumber: string;
}

/**
 * Metadata attached to a 409 Conflict on create-supplier-invoice.
 * The backend serialises this dictionary as `error.data` on the ApiResponse.
 */
export interface SupplierInvoiceConflictMetadata {
  suggestedInvoiceNumber?: string | null;
  conflictingInvoiceNumber?: string | null;
}

/** Request body for creating a supplier invoice from a purchase receipt. */
export interface CreateSupplierInvoiceFromReceiptRequest {
  invoiceNumber: string;
  invoiceDate: string;
  paymentTermDays: number;
  externalReference?: string;
  notes?: string;
  sendEmail?: boolean;
  lines?: CreateSupplierInvoiceLineRequest[];
  lineAssetClassifications?: SupplierInvoiceLineAssetClassification[];
  paymentMethod?: string;
  useSuggestedNumber?: boolean;
}

export function normalizePurchaseReceiptStatus(
  status: PurchaseReceiptStatus | string
): PurchaseReceiptStatus | null {
  if (typeof status === 'number') {
    return status in PurchaseReceiptStatus ? status : null;
  }

  const statusMap: Record<string, PurchaseReceiptStatus> = {
    draft: PurchaseReceiptStatus.Draft,
    validated: PurchaseReceiptStatus.Validated,
    cancelled: PurchaseReceiptStatus.Cancelled,
    partiallyinvoiced: PurchaseReceiptStatus.PartiallyInvoiced,
    invoiced: PurchaseReceiptStatus.Invoiced
  };

  return statusMap[String(status).toLowerCase()] ?? null;
}

export function mapPurchaseReceiptDetailFromApi(
  dto: PurchaseReceiptDetailApiDto
): PurchaseReceiptDetail | null {
  const normalizedStatus = normalizePurchaseReceiptStatus(dto.status);
  if (normalizedStatus === null) {
    return null;
  }

  return {
    ...dto,
    status: normalizedStatus
  };
}
