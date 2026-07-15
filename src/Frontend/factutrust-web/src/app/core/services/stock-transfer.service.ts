import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

export interface StockTransferListDto {
  id: string;
  number: string;
  transferDate: string;
  status: string;
  statusDisplay: string;
  statusCss: string;
  reference?: string;
  sourceWarehouseId: string;
  sourceWarehouseName: string;
  destinationWarehouseId: string;
  destinationWarehouseName: string;
  lineCount: number;
  totalRequestedQuantity: number;
  totalTransferredQuantity: number;
  createdAt: string;
}

export interface StockTransferDetailDto {
  id: string;
  number: string;
  transferDate: string;
  status: string;
  statusDisplay: string;
  statusCss: string;
  reference?: string;
  notes?: string;
  sourceWarehouseId: string;
  sourceWarehouseName: string;
  sourceWarehouseAddress?: string;
  destinationWarehouseId: string;
  destinationWarehouseName: string;
  destinationWarehouseAddress?: string;
  lines: StockTransferLineDto[];
  totalRequestedQuantity: number;
  totalTransferredQuantity: number;
  confirmedAt?: string;
  completedAt?: string;
  cancelledAt?: string;
  cancellationReason?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface StockTransferLineDto {
  id: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  requestedQuantity: number;
  transferredQuantity: number;
  isFullyTransferred: boolean;
  notes?: string;
}

export interface CreateStockTransferRequest {
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  transferDate: string;
  reference?: string;
  notes?: string;
  lines: CreateStockTransferLineRequest[];
}

export interface CreateStockTransferLineRequest {
  productId: string;
  requestedQuantity: number;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class StockTransferService {
  private http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/StockTransfers`;

  getStockTransfers(params?: {
    status?: string;
    warehouseId?: string;
    fromDate?: string;
    toDate?: string;
  }): Observable<ApiResponse<StockTransferListDto[]>> {
    let httpParams = new HttpParams();
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.warehouseId) httpParams = httpParams.set('warehouseId', params.warehouseId);
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);

    return this.http.get<ApiResponse<StockTransferListDto[]>>(this.API_URL, { params: httpParams });
  }

  getStockTransfer(id: string): Observable<ApiResponse<StockTransferDetailDto>> {
    return this.http.get<ApiResponse<StockTransferDetailDto>>(`${this.API_URL}/${id}`);
  }

  createStockTransfer(request: CreateStockTransferRequest): Observable<ApiResponse<{ id: string }>> {
    return this.http.post<ApiResponse<{ id: string }>>(this.API_URL, request);
  }

  confirmStockTransfer(id: string): Observable<ApiResponse<void>> {
    return this.http.patch<ApiResponse<void>>(`${this.API_URL}/${id}/confirm`, {});
  }

  startTransitStockTransfer(id: string): Observable<ApiResponse<void>> {
    return this.http.patch<ApiResponse<void>>(`${this.API_URL}/${id}/start-transit`, {});
  }

  completeStockTransfer(id: string): Observable<ApiResponse<void>> {
    return this.http.patch<ApiResponse<void>>(`${this.API_URL}/${id}/complete`, {});
  }

  cancelStockTransfer(id: string, reason: string): Observable<ApiResponse<void>> {
    return this.http.patch<ApiResponse<void>>(`${this.API_URL}/${id}/cancel`, { reason });
  }

  downloadPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.API_URL}/${id}/pdf`, { responseType: 'blob' });
  }
}
