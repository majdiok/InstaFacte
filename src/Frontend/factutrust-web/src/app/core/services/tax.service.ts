import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

export enum TaxType {
  VAT = 0,
  Stamp = 1,
  FODEC = 2,
  Consumption = 3,
  Other = 99
}

export enum TaxValueType {
  Percentage = 0,
  FixedAmount = 1
}

export enum TaxContext {
  All = 0,
  Sales = 1,
  Purchases = 2
}

export interface Tax {
  id: string;
  name: string;
  type: TaxType;
  typeDisplay: string;
  valueType: TaxValueType;
  valueTypeDisplay: string;
  value: number;
  context: TaxContext;
  contextDisplay: string;
  isAppliedToProducts: boolean;
  isActive: boolean;
  isSystem: boolean;
  displayOrder: number;
}

export interface CreateTaxRequest {
  name: string;
  type: number;
  valueType: number;
  value: number;
  context: number;
  isAppliedToProducts: boolean;
  displayOrder: number;
}

export interface UpdateTaxRequest extends CreateTaxRequest {}

export interface VatRateOption {
  id: string;
  percent: number;
  label: string;
}

export interface SetTaxActiveRequest {
  isActive: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class TaxService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/tax`;

  getTaxes(params?: { type?: number; context?: number; activeOnly?: boolean }): Observable<ApiResponse<Tax[]>> {
    let httpParams = new HttpParams();
    if (params?.type !== undefined && params.type !== null) {
      httpParams = httpParams.set('type', String(params.type));
    }
    if (params?.context !== undefined && params.context !== null) {
      httpParams = httpParams.set('context', String(params.context));
    }
    if (params?.activeOnly === true) {
      httpParams = httpParams.set('activeOnly', 'true');
    }
    return this.http.get<ApiResponse<Tax[]>>(this.baseUrl, { params: httpParams });
  }

  getTax(id: string): Observable<ApiResponse<Tax>> {
    return this.http.get<ApiResponse<Tax>>(`${this.baseUrl}/${id}`);
  }

  getVatRates(): Observable<ApiResponse<VatRateOption[]>> {
    return this.http.get<ApiResponse<VatRateOption[]>>(`${this.baseUrl}/vat-rates`);
  }

  createTax(body: CreateTaxRequest): Observable<ApiResponse<Tax>> {
    return this.http.post<ApiResponse<Tax>>(this.baseUrl, body);
  }

  updateTax(id: string, body: UpdateTaxRequest): Observable<ApiResponse<Tax>> {
    return this.http.put<ApiResponse<Tax>>(`${this.baseUrl}/${id}`, body);
  }

  setTaxActive(id: string, body: SetTaxActiveRequest): Observable<ApiResponse<Tax>> {
    return this.http.put<ApiResponse<Tax>>(`${this.baseUrl}/${id}/toggle`, body);
  }

  deleteTax(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.baseUrl}/${id}`);
  }
}
