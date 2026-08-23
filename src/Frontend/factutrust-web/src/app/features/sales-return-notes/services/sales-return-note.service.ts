import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from '../../../core/services/auth.service';
import {
  CreateSalesReturnNoteDto,
  EligibleDeliveryNoteDto,
  SalesReturnNoteDetailDto,
  SalesReturnNoteListDto,
  SalesReturnNoteListSummary,
  SalesReturnNotePrefillDto,
  SalesReturnNoteStatus,
  UpdateSalesReturnNoteDto
} from '../models/sales-return-note.model';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface SalesReturnNoteSearchParams {
  search?: string;
  status?: SalesReturnNoteStatus | null;
  clientId?: string;
  deliveryNoteId?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class SalesReturnNoteService {
  private readonly API_URL = `${environment.apiUrl}/sales-return-notes`;

  constructor(private http: HttpClient) {}

  getList(params: SalesReturnNoteSearchParams = {}): Observable<ApiResponse<PagedResult<SalesReturnNoteListDto>>> {
    return this.http.get<ApiResponse<PagedResult<SalesReturnNoteListDto>>>(this.API_URL, {
      params: this.toParams(params)
    });
  }

  getSummary(params: SalesReturnNoteSearchParams = {}): Observable<ApiResponse<SalesReturnNoteListSummary>> {
    return this.http.get<ApiResponse<SalesReturnNoteListSummary>>(`${this.API_URL}/summary`, {
      params: this.toParams(params)
    });
  }

  getById(id: string): Observable<ApiResponse<SalesReturnNoteDetailDto>> {
    return this.http.get<ApiResponse<SalesReturnNoteDetailDto>>(`${this.API_URL}/${id}`);
  }

  getEligibleDeliveryNotes(clientId?: string, search?: string): Observable<ApiResponse<EligibleDeliveryNoteDto[]>> {
    let httpParams = new HttpParams();
    if (clientId) httpParams = httpParams.set('clientId', clientId);
    if (search) httpParams = httpParams.set('search', search);
    return this.http.get<ApiResponse<EligibleDeliveryNoteDto[]>>(`${this.API_URL}/eligible-delivery-notes`, {
      params: httpParams
    });
  }

  prefillFromDeliveryNote(deliveryNoteId: string): Observable<ApiResponse<SalesReturnNotePrefillDto>> {
    return this.http.get<ApiResponse<SalesReturnNotePrefillDto>>(
      `${this.API_URL}/from-delivery-note/${deliveryNoteId}`
    );
  }

  create(dto: CreateSalesReturnNoteDto): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(this.API_URL, dto);
  }

  update(id: string, dto: UpdateSalesReturnNoteDto): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(`${this.API_URL}/${id}`, dto);
  }

  confirm(id: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.API_URL}/${id}/confirm`, {});
  }

  delete(id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.API_URL}/${id}`);
  }

  downloadPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.API_URL}/${id}/pdf`, { responseType: 'blob' });
  }

  private toParams(params: SalesReturnNoteSearchParams): HttpParams {
    let httpParams = new HttpParams();
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.status !== undefined && params.status !== null) {
      httpParams = httpParams.set('status', params.status.toString());
    }
    if (params.clientId) httpParams = httpParams.set('clientId', params.clientId);
    if (params.deliveryNoteId) httpParams = httpParams.set('deliveryNoteId', params.deliveryNoteId);
    if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return httpParams;
  }
}
