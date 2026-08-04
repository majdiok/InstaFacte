import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse, PagedResult } from './client.service';

export enum PurchaseOrderStatus {
    Draft = 0,
    Confirmed = 1,
    PartiallyReceived = 2,
    Received = 3,
    Cancelled = 4,
    Invoiced = 5,
    PartiallyInvoiced = 6
}

export interface PurchaseOrderListItem {
    id: string;
    number: string;
    supplierName: string;
    supplierId: string;
    orderDate: string;
    expectedDeliveryDate: string | null;
    reference: string | null;
    status: PurchaseOrderStatus;
    statusDisplay: string;
    statusCss: string;
    totalHT: number;
    totalTTC: number;
    lineCount: number;
}

export interface PurchaseOrderDetail {
    id: string;
    number: string;
    orderDate: string;
    expectedDeliveryDate: string | null;
    status: PurchaseOrderStatus;
    statusDisplay: string;
    statusCss: string;
    reference: string | null;
    notes: string | null;
    supplier: SupplierSummary;
    lines: PurchaseOrderLine[];
    subTotal: number;
    totalVat: number;
    totalTTC: number;
    confirmedAt: string | null;
    receivedAt: string | null;
    invoicedAt: string | null;
    cancelledAt: string | null;
    cancellationReason: string | null;
    totalReceivedNotInvoicedQuantity?: number;
    hasReceivedNotInvoiced?: boolean;
    linkedSupplierInvoices?: LinkedSupplierInvoiceSummary[];
    createdAt: string;
    /** Destination warehouse on the PO; reception falls back to default when null. */
    warehouseId: string | null;
    warehouseName: string | null;
}

/**
 * Raw purchase-order detail payload coming from API.
 * Status can be number or enum string depending on serializer/settings.
 */
export interface PurchaseOrderDetailApiDto extends Omit<PurchaseOrderDetail, 'status'> {
    status: PurchaseOrderStatus | string;
}

export interface SupplierSummary {
    id: string;
    name: string;
    nif: string | null;
    email: string;
    address: string;
}

export interface PurchaseOrderLine {
    id: string;
    lineNumber: number;
    productId: string;
    productCode: string;
    productName: string;
    productDescription: string | null;
    quantity: number;
    receivedQuantity: number;
    invoicedQuantity?: number;
    receivedNotInvoicedQuantity?: number;
    pendingQuantity: number;
    isFullyReceived: boolean;
    unit: string | null;
    unitPriceHT: number;
    vatRateDisplay: string;
    subTotal: number;
    vatAmount: number;
    total: number;
}

export interface PurchaseOrderSearchParams {
    search?: string;
    status?: PurchaseOrderStatus;
    supplierId?: string;
    fromDate?: string;
    toDate?: string;
    page?: number;
    pageSize?: number;
}

/** Totaux agrégés (backend) de la liste des bons de commande, sur l'ensemble filtré complet. */
export interface PurchaseOrderListSummary {
    count: number;
    totalTtc: number;
    totalHt: number;
    totalVat: number;
    receivedCount: number;
    pendingCount: number;
    currency: string;
}

export interface CreatePurchaseOrderRequest {
    supplierId: string;
    orderDate: string;
    expectedDeliveryDate?: string;
    reference?: string;
    notes?: string;
    warehouseId?: string | null;
    lines: CreatePurchaseOrderLineRequest[];
}

export interface CreatePurchaseOrderLineRequest {
    productId: string;
    quantity: number;
    unitPriceHT?: number;
}

export interface ReceiveGoodsRequest {
    warehouseId?: string | null;
    lines: ReceiveGoodsLineRequest[];
}

export interface ReceiveGoodsLineRequest {
    lineId: string;
    receivedQuantity: number;
}

export interface UpdatePurchaseOrderRequest {
    expectedDeliveryDate?: string;
    reference?: string;
    notes?: string;
    lines?: UpdatePurchaseOrderLineRequest[];
}

export interface UpdatePurchaseOrderLineRequest {
    id?: string;
    productId: string;
    quantity: number;
    unitPriceHT?: number;
}

@Injectable({
    providedIn: 'root'
})
export class PurchaseOrderService {
    private readonly API_URL = `${environment.apiUrl}/purchaseorders`;
    private http = inject(HttpClient);

    getPurchaseOrders(params: PurchaseOrderSearchParams = {}): Observable<ApiResponse<PagedResult<PurchaseOrderListItem>>> {
        let httpParams = new HttpParams();

        if (params.search) httpParams = httpParams.set('search', params.search);
        if (params.status !== undefined) httpParams = httpParams.set('status', params.status.toString());
        if (params.supplierId) httpParams = httpParams.set('supplierId', params.supplierId);
        if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
        if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
        if (params.page) httpParams = httpParams.set('page', params.page.toString());
        if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

        return this.http.get<ApiResponse<PagedResult<PurchaseOrderListItem>>>(this.API_URL, { params: httpParams });
    }

    /** Totaux agrégés respectant les mêmes filtres que {@link getPurchaseOrders} (calcul backend). */
    getPurchaseOrdersSummary(params: PurchaseOrderSearchParams = {}): Observable<ApiResponse<PurchaseOrderListSummary>> {
        let httpParams = new HttpParams();
        if (params.search) httpParams = httpParams.set('search', params.search);
        if (params.status !== undefined) httpParams = httpParams.set('status', params.status.toString());
        if (params.supplierId) httpParams = httpParams.set('supplierId', params.supplierId);
        if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
        if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
        return this.http.get<ApiResponse<PurchaseOrderListSummary>>(`${this.API_URL}/summary`, { params: httpParams });
    }

    getPurchaseOrder(id: string): Observable<ApiResponse<PurchaseOrderDetailApiDto>> {
        return this.http.get<ApiResponse<PurchaseOrderDetailApiDto>>(`${this.API_URL}/${id}`);
    }

    createPurchaseOrder(request: CreatePurchaseOrderRequest): Observable<ApiResponse<string>> {
        return this.http.post<ApiResponse<string>>(this.API_URL, request);
    }

    confirmPurchaseOrder(id: string): Observable<ApiResponse<object>> {
        return this.http.patch<ApiResponse<object>>(`${this.API_URL}/${id}/confirm`, {});
    }

    receiveGoods(id: string, request: ReceiveGoodsRequest): Observable<ApiResponse<object>> {
        return this.http.patch<ApiResponse<object>>(`${this.API_URL}/${id}/receive`, request);
    }

    cancelPurchaseOrder(id: string, reason: string): Observable<ApiResponse<object>> {
        return this.http.patch<ApiResponse<object>>(`${this.API_URL}/${id}/cancel`, { reason });
    }

    updatePurchaseOrder(id: string, request: UpdatePurchaseOrderRequest): Observable<ApiResponse<object>> {
        return this.http.put<ApiResponse<object>>(`${this.API_URL}/${id}`, request);
    }

    deletePurchaseOrder(id: string): Observable<ApiResponse<object>> {
        return this.http.delete<ApiResponse<object>>(`${this.API_URL}/${id}`);
    }

    downloadPdf(id: string): Observable<Blob> {
        return this.http.get(`${this.API_URL}/${id}/pdf`, { responseType: 'blob' });
    }

    sendByEmail(id: string): Observable<ApiResponse<object>> {
        return this.http.post<ApiResponse<object>>(`${this.API_URL}/${id}/send-email`, {});
    }

    createSupplierInvoice(id: string, request: CreateSupplierInvoiceFromPORequest): Observable<ApiResponse<SupplierInvoiceCreationResponse>> {
        return this.http.post<ApiResponse<SupplierInvoiceCreationResponse>>(
            `${this.API_URL}/${id}/create-supplier-invoice`,
            request
        );
    }

    getSupplierInvoicePrefill(id: string): Observable<ApiResponse<SupplierInvoicePrefill>> {
        return this.http.get<ApiResponse<SupplierInvoicePrefill>>(`${this.API_URL}/${id}/supplier-invoice-prefill`);
    }
}

export interface LinkedSupplierInvoiceSummary {
    id: string;
    invoiceNumber: string;
    invoiceDate: string;
    status: number;
    statusDisplay: string;
    totalTTC: number;
}

export interface SupplierInvoicePrefillLine {
    sourceLineId: string;
    lineNumber: number;
    productCode: string;
    productName: string;
    unit: string | null;
    receivedQuantity: number;
    invoicedQuantity: number;
    quantityToInvoice: number;
    maxQuantityToInvoice: number;
    unitPriceHT: number;
    vatRateDisplay: string;
    subTotalHT: number;
}

export interface SupplierInvoicePrefill {
    purchaseOrderId?: string | null;
    purchaseOrderNumber?: string | null;
    purchaseReceiptId?: string | null;
    purchaseReceiptNumber?: string | null;
    supplierId: string;
    supplierName: string;
    paymentTermDays: number;
    lines: SupplierInvoicePrefillLine[];
    subTotalHT: number;
    totalVat: number;
    totalTTC: number;
    currency: string;
    suggestedInvoiceNumber?: string | null;
}

export interface CreateSupplierInvoiceLineRequest {
    sourceLineId: string;
    quantityToInvoice: number;
}

export interface SupplierInvoiceLineAssetClassification {
    lineNumber: number;
    isFixedAsset: boolean;
    depreciationRateCategoryId?: string;
    assetAccountNumber?: string;
}

/** Request body for creating a supplier invoice from a purchase order. */
export interface CreateSupplierInvoiceFromPORequest {
    invoiceNumber: string;
    invoiceDate: string;
    paymentTermDays: number;
    externalReference?: string;
    notes?: string;
    sendEmail?: boolean;
    lines?: CreateSupplierInvoiceLineRequest[];
    lineAssetClassifications?: SupplierInvoiceLineAssetClassification[];
    /** Mode de paiement prévu (informatif), ex. « Effet de commerce ». */
    paymentMethod?: string;
    useSuggestedNumber?: boolean;
}

/** @deprecated Use CreateSupplierInvoiceFromPORequest for createSupplierInvoice. */
export interface CreateSupplierInvoiceRequest extends CreateSupplierInvoiceFromPORequest { }

/**
 * Response returned by POST /purchaseorders/{id}/create-supplier-invoice (and the PR sibling).
 * `invoiceNumber` is the number actually persisted — may differ from the caller's input when
 * the server auto-resolved (useSuggestedNumber) or retried after a concurrent duplicate.
 */
export interface SupplierInvoiceCreationResponse {
    id: string;
    invoiceNumber: string;
}

/** Metadata attached to a 409 Conflict on create-supplier-invoice; serialised as `error.data`. */
export interface SupplierInvoiceConflictMetadata {
    suggestedInvoiceNumber?: string | null;
    conflictingInvoiceNumber?: string | null;
}

/**
 * Normalizes purchase-order status from API transport format to frontend enum.
 * Supports camelCase (API contract), PascalCase (legacy), and numeric values.
 */
export function normalizePurchaseOrderStatus(status: PurchaseOrderStatus | string): PurchaseOrderStatus | null {
    if (typeof status === 'number') {
        return status in PurchaseOrderStatus ? status : null;
    }

    const statusMap: Record<string, PurchaseOrderStatus> = {
        draft: PurchaseOrderStatus.Draft,
        confirmed: PurchaseOrderStatus.Confirmed,
        partiallyreceived: PurchaseOrderStatus.PartiallyReceived,
        received: PurchaseOrderStatus.Received,
        cancelled: PurchaseOrderStatus.Cancelled,
        invoiced: PurchaseOrderStatus.Invoiced,
        partiallyinvoiced: PurchaseOrderStatus.PartiallyInvoiced
    };

    return statusMap[status.toLowerCase()] ?? null;
}

/**
 * Maps raw API DTO to frontend view model.
 * Returns null when status is unknown so callers can apply safe UI fallback.
 */
export function mapPurchaseOrderDetailFromApi(
    dto: PurchaseOrderDetailApiDto
): PurchaseOrderDetail | null {
    const normalizedStatus = normalizePurchaseOrderStatus(dto.status);
    if (normalizedStatus === null) {
        return null;
    }

    return {
        ...dto,
        status: normalizedStatus,
        warehouseId: dto.warehouseId ?? null,
        warehouseName: dto.warehouseName ?? null
    };
}
