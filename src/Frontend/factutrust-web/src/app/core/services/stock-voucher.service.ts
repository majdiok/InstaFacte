import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse, PagedResult } from './client.service';

export type StockVoucherKindName = 'Entry' | 'Issue';
export type StockVoucherStatusName = 'Draft' | 'Validated' | 'Cancelled';

export interface StockVoucherLineAllocations {
  lineId: string;
  allocations: {
    quantity: number;
    lotNumber?: string | null;
    expiryDate?: string | null;
    serialNumber?: string | null;
  }[];
}

export interface StockVoucherListDto {
  id: string;
  number: string;
  kind: string | number;
  kindDisplay: string;
  voucherDate: string;
  status: string | number;
  statusDisplay: string;
  statusCss: string;
  reason: string | number;
  reasonDisplay: string;
  warehouseId: string;
  warehouseName: string | null;
  externalReference: string | null;
  lineCount: number;
  totalQuantity: number;
  totalValue: number;
}

export interface StockVoucherListSummaryDto {
  count: number;
  draftCount: number;
  validatedCount: number;
  cancelledCount: number;
  totalQuantity: number;
  totalValue: number;
}

export interface StockVoucherLineDto {
  id: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  unit: string | null;
  quantity: number;
  unitCost: number;
  lineValue: number;
  notes: string | null;
}

export interface StockVoucherDetailDto {
  id: string;
  number: string;
  kind: string | number;
  kindDisplay: string;
  voucherDate: string;
  status: string | number;
  statusDisplay: string;
  statusCss: string;
  reason: string | number;
  reasonDisplay: string;
  warehouseId: string;
  warehouseName: string | null;
  externalReference: string | null;
  notes: string | null;
  lines: StockVoucherLineDto[];
  totalQuantity: number;
  totalValue: number;
  validatedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  createdAt: string;
}

export interface CreateStockVoucherLineRequest {
  productId: string;
  quantity: number;
  unitCost?: number | null;
  notes?: string | null;
}

export interface CreateStockVoucherRequest {
  kind: StockVoucherKindName;
  voucherDate: string;
  warehouseId: string;
  reason: string;
  externalReference?: string | null;
  notes?: string | null;
  lines: CreateStockVoucherLineRequest[];
}

export interface UpdateStockVoucherRequest {
  voucherDate: string;
  warehouseId: string;
  reason: string;
  externalReference?: string | null;
  notes?: string | null;
  lines: CreateStockVoucherLineRequest[];
}

const MOVEMENT_REASON_NAMES: Record<number, string> = {
  1: 'Purchase',
  2: 'Sale',
  3: 'CustomerReturn',
  4: 'SupplierReturn',
  5: 'InventoryAdjustment',
  6: 'Damage',
  7: 'Transfer',
  8: 'InitialStock',
  9: 'Delivery',
  10: 'InternalUse',
  11: 'GiftOrSample',
  12: 'FoundOrOther'
};

export function movementReasonName(reason: string | number | null | undefined): string {
  if (reason == null) return '';
  if (typeof reason === 'string' && Number.isNaN(Number(reason))) return reason;
  const n = typeof reason === 'number' ? reason : Number(reason);
  return MOVEMENT_REASON_NAMES[n] ?? String(reason);
}

export function isStockVoucherEntry(kind: string | number | null | undefined): boolean {
  return kind === 'Entry' || kind === 1 || kind === '1';
}

export function isStockVoucherDraft(status: string | number | null | undefined): boolean {
  return status === 'Draft' || status === 0 || status === '0';
}

export function isStockVoucherValidated(status: string | number | null | undefined): boolean {
  return status === 'Validated' || status === 1 || status === '1';
}

export function isStockVoucherCancelled(status: string | number | null | undefined): boolean {
  return status === 'Cancelled' || status === 2 || status === '2';
}

@Injectable({ providedIn: 'root' })
export class StockVoucherService {
  private http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/StockVouchers`;

  getStockVouchers(params?: {
    kind?: StockVoucherKindName;
    search?: string;
    status?: StockVoucherStatusName;
    warehouseId?: string;
    fromDate?: string;
    toDate?: string;
    page?: number;
    pageSize?: number;
  }): Observable<ApiResponse<PagedResult<StockVoucherListDto>>> {
    let httpParams = new HttpParams();
    if (params?.kind) httpParams = httpParams.set('kind', params.kind);
    if (params?.search) httpParams = httpParams.set('search', params.search);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.warehouseId) httpParams = httpParams.set('warehouseId', params.warehouseId);
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params?.page) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get<ApiResponse<PagedResult<StockVoucherListDto>>>(this.API_URL, { params: httpParams });
  }

  getSummary(params?: {
    kind?: StockVoucherKindName;
    search?: string;
    status?: StockVoucherStatusName;
    warehouseId?: string;
    fromDate?: string;
    toDate?: string;
  }): Observable<ApiResponse<StockVoucherListSummaryDto>> {
    let httpParams = new HttpParams();
    if (params?.kind) httpParams = httpParams.set('kind', params.kind);
    if (params?.search) httpParams = httpParams.set('search', params.search);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.warehouseId) httpParams = httpParams.set('warehouseId', params.warehouseId);
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);
    return this.http.get<ApiResponse<StockVoucherListSummaryDto>>(`${this.API_URL}/summary`, { params: httpParams });
  }

  getStockVoucher(id: string): Observable<ApiResponse<StockVoucherDetailDto>> {
    return this.http.get<ApiResponse<StockVoucherDetailDto>>(`${this.API_URL}/${id}`);
  }

  create(request: CreateStockVoucherRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(this.API_URL, request);
  }

  update(id: string, request: UpdateStockVoucherRequest): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(`${this.API_URL}/${id}`, request);
  }

  validate(id: string, lineAllocations?: StockVoucherLineAllocations[]): Observable<ApiResponse<object>> {
    if (lineAllocations && lineAllocations.length > 0) {
      return this.http.post<ApiResponse<object>>(`${this.API_URL}/${id}/validate`, lineAllocations);
    }
    return this.http.patch<ApiResponse<object>>(`${this.API_URL}/${id}/validate`, {});
  }

  cancel(id: string, reason: string): Observable<ApiResponse<object>> {
    return this.http.patch<ApiResponse<object>>(`${this.API_URL}/${id}/cancel`, { reason });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }

  downloadPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.API_URL}/${id}/pdf`, { responseType: 'blob' });
  }
}
