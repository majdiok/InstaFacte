import { Injectable } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { SKIP_ERROR_TOAST, createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { ApiResponse } from './auth.service';

export interface QuoteListItem {
  id: string;
  number: string;
  issueDate: string;
  expiryDate: string;
  status: string;
  statusCssClass: string;
  clientName: string;
  totalAmount: number;
  currency: string;
  isExpired: boolean;
  isConverted: boolean;
  convertedInvoiceId: string | null;
}

export interface QuoteLine {
  id: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  productDescription: string | null;
  quantity: number;
  unit: string | null;
  unitPrice: number;
  vatRatePercent: number;
  discountPercent: number | null;
  discountAmount: number;
  subTotal: number;
  isFodecApplicable: boolean;
  fodecRatePercent: number;
  /** FODEC de ligne (assiette : HT après remise) — repris tel quel sur la facture. */
  fodecAmount: number;
  vatAmount: number;
  total: number;
}

export interface VatBreakdown {
  rate: number;
  rateDisplay: string;
  baseAmount: number;
  vatAmount: number;
}

export interface ClientSummary {
  id: string;
  name: string;
  nif: string | null;
  email: string;
  address: string;
}

export interface QuoteDetail {
  id: string;
  number: string;
  issueDate: string;
  expiryDate: string;
  status: string;
  statusDisplay: string;
  clientId: string;
  client: ClientSummary;
  reference: string | null;
  notes: string | null;
  termsAndConditions: string | null;
  lines: QuoteLine[];
  subTotal: number;
  /** FODEC agrégé annoncé au devis. */
  fodecAmount: number;
  totalVat: number;
  /** Timbre fiscal annoncé au devis. */
  fiscalStampAmount: number;
  totalAmount: number;
  currency: string;
  vatBreakdown: VatBreakdown[];
  sentAt: string | null;
  acceptedAt: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  convertedInvoiceId: string | null;
  convertedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface CreateQuoteLine {
  productId?: string;
  designation?: string;
  description?: string;
  quantity: number;
  unit?: string;
  unitPrice: number;
  vatRatePercent: number;
  discountPercent?: number;
}

export interface CreateQuoteRequest {
  clientId: string;
  issueDate: string;
  expiryDate: string;
  reference?: string;
  notes?: string;
  termsAndConditions?: string;
  lines: CreateQuoteLine[];
  /** When set, increments usage on the quote template after successful creation. */
  quoteTemplateId?: string;
}

export interface QuoteSearchParams {
  search?: string;
  status?: number;
  fromDate?: string;
  toDate?: string;
  clientId?: string;
  page?: number;
  pageSize?: number;
  /** When true, global 403 modal / toast is suppressed (e.g. dashboard aggregate). */
  skipGlobalErrorUi?: boolean;
}

/** Totaux agrégés (backend) de la liste des devis, sur l'ensemble filtré complet. */
export interface QuoteListSummary {
  count: number;
  totalTtc: number;
  totalHt: number;
  totalVat: number;
  acceptedCount: number;
  expiredCount: number;
  currency: string;
}

@Injectable({
  providedIn: 'root'
})
export class QuoteService {
  private readonly API_URL = `${environment.apiUrl}/quotes`;

  constructor(private http: HttpClient) {}

  /** Totaux agrégés respectant les mêmes filtres que {@link getQuotes} (calcul backend). */
  getQuotesSummary(params: QuoteSearchParams = {}): Observable<ApiResponse<QuoteListSummary>> {
    let httpParams = new HttpParams();
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.status !== undefined) httpParams = httpParams.set('status', params.status.toString());
    if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params.clientId) httpParams = httpParams.set('clientId', params.clientId);
    const opts: { params: HttpParams; context?: ReturnType<typeof createHttpContextSkipGlobalErrorUi> } = {
      params: httpParams
    };
    if (params.skipGlobalErrorUi === true) {
      opts.context = createHttpContextSkipGlobalErrorUi();
    }
    return this.http.get<ApiResponse<QuoteListSummary>>(`${this.API_URL}/summary`, opts);
  }

  getQuotes(params: QuoteSearchParams = {}): Observable<ApiResponse<PagedResult<QuoteListItem>>> {
    const skipGlobalErrorUi = params.skipGlobalErrorUi === true;
    let httpParams = new HttpParams();
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.status !== undefined) httpParams = httpParams.set('status', params.status.toString());
    if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params.clientId) httpParams = httpParams.set('clientId', params.clientId);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    const opts: { params: HttpParams; context?: ReturnType<typeof createHttpContextSkipGlobalErrorUi> } = {
      params: httpParams
    };
    if (skipGlobalErrorUi) {
      opts.context = createHttpContextSkipGlobalErrorUi();
    }
    return this.http.get<ApiResponse<PagedResult<QuoteListItem>>>(this.API_URL, opts);
  }

  getQuote(id: string): Observable<ApiResponse<QuoteDetail>> {
    return this.http.get<ApiResponse<QuoteDetail>>(`${this.API_URL}/${id}`);
  }

  createQuote(quote: CreateQuoteRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(this.API_URL, quote, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    });
  }

  sendQuote(id: string): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.API_URL}/${id}/send`, {});
  }

  acceptQuote(id: string): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.API_URL}/${id}/accept`, {});
  }

  rejectQuote(id: string, reason?: string): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.API_URL}/${id}/reject`, { reason: reason ?? null });
  }

  cancelQuote(id: string, reason: string): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.API_URL}/${id}/cancel`, { reason });
  }

  convertToInvoice(id: string, options?: { issueDate?: string; dueDate?: string; reference?: string; notes?: string; paymentTerms?: string }): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.API_URL}/${id}/convert-to-invoice`, options ?? {});
  }

  duplicateQuote(id: string): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.API_URL}/${id}/duplicate`, {});
  }

  downloadPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.API_URL}/${id}/pdf`, { responseType: 'blob' });
  }
}
