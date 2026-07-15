import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import {
  NumberingDocumentType,
  NumberingScheme,
  PreviewNumberingRequest,
  PreviewNumberingResponse,
  SaveNumberingSchemeRequest,
  normalizeNumberingScheme
} from '@features/settings/numbering/models/numbering.models';

@Injectable({
  providedIn: 'root'
})
export class NumberingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/settings/numbering`;
  private readonly skipGlobalErrorUi = new HttpContext().set(SKIP_ERROR_TOAST, true);

  getSchemes(fiscalYear?: number): Observable<ApiResponse<NumberingScheme[]>> {
    let params = new HttpParams();
    if (fiscalYear !== undefined && fiscalYear !== null) {
      params = params.set('fiscalYear', String(fiscalYear));
    }
    return this.http.get<ApiResponse<NumberingScheme[]>>(this.baseUrl, {
      params,
      context: this.skipGlobalErrorUi
    }).pipe(
      map((res) => this.normalizeSchemesResponse(res))
    );
  }

  getScheme(
    documentType: NumberingDocumentType,
    fiscalYear?: number
  ): Observable<ApiResponse<NumberingScheme>> {
    let params = new HttpParams();
    if (fiscalYear !== undefined && fiscalYear !== null) {
      params = params.set('fiscalYear', String(fiscalYear));
    }
    return this.http.get<ApiResponse<NumberingScheme>>(`${this.baseUrl}/${documentType}`, {
      params,
      context: this.skipGlobalErrorUi
    }).pipe(
      map((res) => this.normalizeSchemeResponse(res))
    );
  }

  saveScheme(
    documentType: NumberingDocumentType,
    request: SaveNumberingSchemeRequest,
    fiscalYear?: number
  ): Observable<ApiResponse<NumberingScheme>> {
    let params = new HttpParams();
    if (fiscalYear !== undefined && fiscalYear !== null) {
      params = params.set('fiscalYear', String(fiscalYear));
    }
    return this.http.put<ApiResponse<NumberingScheme>>(
      `${this.baseUrl}/${documentType}`,
      request,
      { params, context: this.skipGlobalErrorUi }
    ).pipe(
      map((res) => this.normalizeSchemeResponse(res))
    );
  }

  preview(
    documentType: NumberingDocumentType,
    request: PreviewNumberingRequest
  ): Observable<ApiResponse<PreviewNumberingResponse>> {
    return this.http.post<ApiResponse<PreviewNumberingResponse>>(
      `${this.baseUrl}/${documentType}/preview`,
      request,
      { context: this.skipGlobalErrorUi }
    );
  }

  reset(
    documentType: NumberingDocumentType,
    fiscalYear?: number
  ): Observable<ApiResponse<NumberingScheme>> {
    let params = new HttpParams();
    if (fiscalYear !== undefined && fiscalYear !== null) {
      params = params.set('fiscalYear', String(fiscalYear));
    }
    return this.http.post<ApiResponse<NumberingScheme>>(
      `${this.baseUrl}/${documentType}/reset`,
      null,
      { params, context: this.skipGlobalErrorUi }
    ).pipe(
      map((res) => this.normalizeSchemeResponse(res))
    );
  }

  private normalizeSchemesResponse(
    res: ApiResponse<NumberingScheme[]>
  ): ApiResponse<NumberingScheme[]> {
    if (!res.success || !res.data) {
      return res;
    }
    return {
      ...res,
      data: res.data.map((scheme) => normalizeNumberingScheme(scheme))
    };
  }

  private normalizeSchemeResponse(
    res: ApiResponse<NumberingScheme>
  ): ApiResponse<NumberingScheme> {
    if (!res.success || !res.data) {
      return res;
    }
    return {
      ...res,
      data: normalizeNumberingScheme(res.data)
    };
  }
}
