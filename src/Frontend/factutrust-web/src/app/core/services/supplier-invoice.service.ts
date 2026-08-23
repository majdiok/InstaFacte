import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map, tap } from 'rxjs/operators';
import { CashDeskService } from './cash-desk.service';
import { environment } from '@environments/environment';
import { ApiResponse, PagedResult } from './client.service';

export enum SupplierInvoiceStatus {
    Pending = 0,
    Paid = 1,
    Cancelled = 2,
    PartiallyPaid = 3
}

/**
 * Normalizes supplier-invoice status from API transport to frontend enum.
 * API uses JsonStringEnumConverter with camelCase: pending, paid, cancelled, partiallyPaid.
 * Also accepts numeric values and legacy PascalCase strings.
 */
export function normalizeSupplierInvoiceStatus(
    status: SupplierInvoiceStatus | string | number | null | undefined
): SupplierInvoiceStatus | null {
    if (status === null || status === undefined) {
        return null;
    }
    if (typeof status === 'number') {
        if (status === 0 || status === 1 || status === 2 || status === 3) {
            return status as SupplierInvoiceStatus;
        }
        return null;
    }
    if (typeof status === 'string') {
        const statusMap: Record<string, SupplierInvoiceStatus> = {
            pending: SupplierInvoiceStatus.Pending,
            paid: SupplierInvoiceStatus.Paid,
            cancelled: SupplierInvoiceStatus.Cancelled,
            partiallypaid: SupplierInvoiceStatus.PartiallyPaid
        };
        return statusMap[status.toLowerCase()] ?? null;
    }
    return null;
}

/** True when the invoice can receive payments (pending or partially paid, not settled). */
export function isSupplierInvoiceOpenForPayment(
    status: SupplierInvoiceStatus | string | number | null | undefined
): boolean {
    const n = normalizeSupplierInvoiceStatus(status);
    return n === SupplierInvoiceStatus.Pending || n === SupplierInvoiceStatus.PartiallyPaid;
}

/**
 * Helpers for supplier invoice status. Accepts enum, camelCase / PascalCase strings, and numbers.
 */
export function isSupplierInvoicePaid(
    status: SupplierInvoiceStatus | string | number | null | undefined
): boolean {
    return normalizeSupplierInvoiceStatus(status) === SupplierInvoiceStatus.Paid;
}

export function isSupplierInvoicePending(
    status: SupplierInvoiceStatus | string | number | null | undefined
): boolean {
    return normalizeSupplierInvoiceStatus(status) === SupplierInvoiceStatus.Pending;
}

export function isSupplierInvoiceCancelled(
    status: SupplierInvoiceStatus | string | number | null | undefined
): boolean {
    return normalizeSupplierInvoiceStatus(status) === SupplierInvoiceStatus.Cancelled;
}

export interface RecordSupplierPaymentRequest {
    paymentDate: string;
    amount?: number;
    method?: number;
    reference?: string;
    notes?: string;
    /** Échéance de la traite (yyyy-MM-dd). Requis lorsque method = 5 (Traite). */
    effetDueDate?: string;
}

/** Requête de paiement d'un effet fournisseur à échéance. */
export interface SettleSupplierEffetRequest {
    settlementDate: string;
}

export interface SupplierPaymentItem {
    id: string;
    amount: number;
    currency: string;
    paymentDate: string;
    method: number;
    methodDisplay: string;
    reference: string | null;
    notes: string | null;
    createdAt: string;
    /** Effet de commerce : échéance, statut (0 en portefeuille, 1 payé, 2 impayé). */
    effetDueDate?: string | null;
    effetStatus?: number | null;
    effetStatusDisplay?: string | null;
}

export interface SupplierInvoiceListItem {
    id: string;
    invoiceNumber: string;
    invoiceDate: string;
    dueDate: string;
    supplierName: string;
    supplierId: string;
    purchaseOrderNumber: string | null;
    purchaseOrderId: string | null;
    status: SupplierInvoiceStatus;
    statusDisplay: string;
    statusCss: string;
    totalHT: number;
    totalTTC: number;
    lineCount: number;
    totalPaid: number;
    remainingAmount: number;
    paidAt: string | null;
    warehouseId?: string | null;
    warehouseName?: string | null;
    sourcePurchaseReceiptId?: string | null;
    hasFixedAssetLines?: boolean;
}

export interface CreateStandaloneSupplierInvoiceLineRequest {
    productId: string;
    quantity: number;
    unitPriceHt?: number;
    discountPercent?: number;
    isFixedAsset?: boolean;
    assetAccountNumber?: string;
    depreciationRateCategoryId?: string;
}

export interface CreateStandaloneSupplierInvoiceRequest {
    supplierId: string;
    invoiceNumber?: string;
    invoiceDate: string;
    paymentTermDays?: number;
    externalReference?: string;
    notes?: string;
    paymentMethod?: string;
    warehouseId?: string;
    useSuggestedNumber?: boolean;
    lines: CreateStandaloneSupplierInvoiceLineRequest[];
}

export interface SupplierInvoiceCreationResponse {
    id: string;
    invoiceNumber: string;
}

export interface SupplierInvoiceDetail {
    id: string;
    invoiceNumber: string;
    invoiceDate: string;
    dueDate: string;
    status: SupplierInvoiceStatus;
    statusDisplay: string;
    statusCss: string;
    externalReference: string | null;
    notes: string | null;
    supplier: { id: string; name: string; nif: string | null; email: string; address: string };
    purchaseOrderId: string | null;
    purchaseOrderNumber: string | null;
    sourcePurchaseReceiptId?: string | null;
    sourcePurchaseReceiptNumber?: string | null;
    lines: SupplierInvoiceLineItem[];
    subTotal: number;
    totalVat: number;
    totalTTC: number;
    totalPaid: number;
    remainingAmount: number;
    payments: SupplierPaymentItem[];
    paidAt: string | null;
    paymentReference: string | null;
    /** Mode de paiement prévu (informatif), ex. « Effet de commerce ». */
    paymentMethod: string | null;
    cancelledAt: string | null;
    cancellationReason: string | null;
    createdAt: string;
    isSubjectToWithholding?: boolean;
    withholdingRate?: number | null;
    withholdingAmount?: number | null;
    netAmountAfterWithholding?: number | null;
}

export interface SupplierInvoiceLineItem {
    id: string;
    lineNumber: number;
    productId: string;
    productCode: string;
    productName: string;
    productDescription: string | null;
    quantity: number;
    unit: string | null;
    unitPriceHT: number;
    vatRateDisplay: string;
    subTotal: number;
    vatAmount: number;
    total: number;
    isFixedAsset?: boolean;
    assetAccountNumber?: string | null;
    depreciationRateCategoryId?: string | null;
    fixedAssetId?: string | null;
    fixedAssetInventoryNumber?: string | null;
}

export interface SupplierInvoiceSearchParams {
    search?: string;
    status?: SupplierInvoiceStatus;
    supplierId?: string;
    fromDate?: string;
    toDate?: string;
    page?: number;
    pageSize?: number;
    unpaidOnly?: boolean;
}

/** Totaux agrégés (backend) de la liste fournisseurs, sur l'ensemble filtré complet. */
export interface SupplierInvoiceListSummary {
    count: number;
    totalTtc: number;
    totalHt: number;
    totalVat: number;
    totalPaid: number;
    totalRemaining: number;
    overdueCount: number;
    currency: string;
}

export function mapSupplierInvoiceListItemFromApi(dto: SupplierInvoiceListItem): SupplierInvoiceListItem {
    const normalized = normalizeSupplierInvoiceStatus(dto.status as SupplierInvoiceStatus | string | number);
    return {
        ...dto,
        status:
            normalized !== null
                ? normalized
                : typeof dto.status === 'number' && dto.status >= 0 && dto.status <= 3
                  ? (dto.status as SupplierInvoiceStatus)
                  : SupplierInvoiceStatus.Pending
    };
}

export function mapSupplierInvoiceDetailFromApi(dto: SupplierInvoiceDetail): SupplierInvoiceDetail | null {
    const normalized = normalizeSupplierInvoiceStatus(dto.status as SupplierInvoiceStatus | string | number);
    if (normalized === null) {
        return null;
    }
    return { ...dto, status: normalized };
}

@Injectable({
    providedIn: 'root'
})
export class SupplierInvoiceService {
    private readonly API_URL = `${environment.apiUrl}/supplierinvoices`;
    private http = inject(HttpClient);
    private readonly cashDesk = inject(CashDeskService);

    create(payload: CreateStandaloneSupplierInvoiceRequest): Observable<ApiResponse<SupplierInvoiceCreationResponse>> {
        return this.http.post<ApiResponse<SupplierInvoiceCreationResponse>>(this.API_URL, payload);
    }

    previewNumber(invoiceDate: Date): Observable<ApiResponse<string>> {
        const year = invoiceDate.getFullYear();
        const month = String(invoiceDate.getMonth() + 1).padStart(2, '0');
        const day = String(invoiceDate.getDate()).padStart(2, '0');
        const dateParam = `${year}-${month}-${day}`;
        const params = new HttpParams().set('invoiceDate', dateParam);
        return this.http.get<ApiResponse<string>>(`${this.API_URL}/preview-number`, { params });
    }

    getSupplierInvoices(params: SupplierInvoiceSearchParams = {}): Observable<ApiResponse<PagedResult<SupplierInvoiceListItem>>> {
        let httpParams = new HttpParams();
        if (params.search) httpParams = httpParams.set('search', params.search);
        if (params.status !== undefined) httpParams = httpParams.set('status', params.status.toString());
        if (params.supplierId) httpParams = httpParams.set('supplierId', params.supplierId);
        if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
        if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
        if (params.page) httpParams = httpParams.set('page', params.page.toString());
        if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
        if (params.unpaidOnly === true) httpParams = httpParams.set('unpaidOnly', 'true');
        return this.http.get<ApiResponse<PagedResult<SupplierInvoiceListItem>>>(this.API_URL, { params: httpParams }).pipe(
            map((res) => {
                if (!res.success || !res.data) {
                    return res;
                }
                return {
                    ...res,
                    data: {
                        ...res.data,
                        items: res.data.items.map((item) => mapSupplierInvoiceListItemFromApi(item))
                    }
                };
            })
        );
    }

    /** Totaux agrégés respectant les mêmes filtres que {@link getSupplierInvoices} (calcul backend). */
    getSupplierInvoicesSummary(params: SupplierInvoiceSearchParams = {}): Observable<ApiResponse<SupplierInvoiceListSummary>> {
        let httpParams = new HttpParams();
        if (params.search) httpParams = httpParams.set('search', params.search);
        if (params.status !== undefined) httpParams = httpParams.set('status', params.status.toString());
        if (params.supplierId) httpParams = httpParams.set('supplierId', params.supplierId);
        if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
        if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
        if (params.unpaidOnly === true) httpParams = httpParams.set('unpaidOnly', 'true');
        return this.http.get<ApiResponse<SupplierInvoiceListSummary>>(`${this.API_URL}/summary`, { params: httpParams });
    }

    getSupplierInvoice(id: string): Observable<ApiResponse<SupplierInvoiceDetail>> {
        return this.http.get<ApiResponse<SupplierInvoiceDetail>>(`${this.API_URL}/${id}`).pipe(
            map((res) => {
                if (!res.success || !res.data) {
                    return res;
                }
                const mapped = mapSupplierInvoiceDetailFromApi(res.data);
                return {
                    ...res,
                    data: mapped ?? {
                        ...res.data,
                        status: normalizeSupplierInvoiceStatus(res.data.status) ?? SupplierInvoiceStatus.Pending
                    }
                };
            })
        );
    }

    recordPayment(id: string, request: RecordSupplierPaymentRequest): Observable<ApiResponse<object>> {
        return this.http.post<ApiResponse<object>>(`${this.API_URL}/${id}/record-payment`, request).pipe(
            tap(res => {
                if (res.success && request.paymentDate?.trim()) {
                    this.cashDesk.invalidateCachesAfterCashLedgerMutation(request.paymentDate.trim());
                }
            })
        );
    }

    /** Paie un effet de commerce fournisseur à échéance. */
    settleEffet(invoiceId: string, paymentId: string, request: SettleSupplierEffetRequest): Observable<ApiResponse<object>> {
        return this.http.post<ApiResponse<object>>(
            `${this.API_URL}/${invoiceId}/payments/${paymentId}/settle-effet`, request).pipe(
            tap(res => {
                if (res.success && request.settlementDate?.trim()) {
                    this.cashDesk.invalidateCachesAfterCashLedgerMutation(request.settlementDate.trim());
                }
            })
        );
    }

    getPayments(id: string): Observable<ApiResponse<SupplierPaymentItem[]>> {
        return this.http.get<ApiResponse<SupplierPaymentItem[]>>(`${this.API_URL}/${id}/payments`);
    }

    cancel(id: string, reason: string): Observable<ApiResponse<object>> {
        return this.http.patch<ApiResponse<object>>(`${this.API_URL}/${id}/cancel`, { reason });
    }
}
