import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  CancelPlatformInvoiceRequest,
  CancelPlatformReceiptRequest,
  CreatePlatformInvoiceRequest,
  CreatePlatformReceiptRequest,
  PlatformInvoiceDetailDto,
  PlatformInvoicesPageDto,
  PlatformReceiptDto
} from '@core/models/platform.models';

/** Lot C4 (complément) — Avoir partiel sur facture émise. */
export interface IssueCreditNoteRequest {
  reason: string;
  /** Si vide ou ≤0 → avoir du TTC complet de la facture d'origine. */
  amountTtcTND?: number | null;
}

/** Lot C4 (complément) — Agrégation TVA d'un mois (DGI). */
export interface PlatformVatPeriodDto {
  year: number;
  month: number;
  monthLabel: string;
  invoicesCount: number;
  totalHT: number;
  totalVat: number;
  totalStamp: number;
  totalTTC: number;
  creditNotesCount: number;
  creditNotesAmountTTC: number;
}

/**
 * Lot C4 — Service HTTP CRUD factures plateforme.
 *
 * Routes :
 * - `GET    /api/platform/invoices?tenantId&status&from&to&page&pageSize`
 * - `GET    /api/platform/invoices/{id}`
 * - `POST   /api/platform/invoices`           (CreateDraft)
 * - `POST   /api/platform/invoices/{id}/issue`
 * - `POST   /api/platform/invoices/{id}/cancel`
 * - `GET    /api/platform/invoices/{id}/pdf`  (Blob)
 * - `POST   /api/platform/invoices/{id}/receipts`
 * - `POST   /api/platform/invoices/receipts/{id}/cancel`
 * - `POST   /api/platform/invoices/receipts/{id}/confirm`
 */
@Injectable({ providedIn: 'root' })
export class PlatformInvoicesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/invoices`;

  list(
    tenantId?: string | null,
    status?: string | null,
    from?: string | null,
    to?: string | null,
    page = 1,
    pageSize = 25
  ): Observable<ApiResponse<PlatformInvoicesPageDto>> {
    let hp = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    if (tenantId) hp = hp.set('tenantId', tenantId);
    if (status) hp = hp.set('status', status);
    if (from) hp = hp.set('from', from);
    if (to) hp = hp.set('to', to);
    return this.http.get<ApiResponse<PlatformInvoicesPageDto>>(this.base, { params: hp });
  }

  get(id: string): Observable<ApiResponse<PlatformInvoiceDetailDto>> {
    return this.http.get<ApiResponse<PlatformInvoiceDetailDto>>(`${this.base}/${id}`);
  }

  create(request: CreatePlatformInvoiceRequest): Observable<ApiResponse<PlatformInvoiceDetailDto>> {
    return this.http.post<ApiResponse<PlatformInvoiceDetailDto>>(this.base, request);
  }

  issue(id: string): Observable<ApiResponse<PlatformInvoiceDetailDto>> {
    return this.http.post<ApiResponse<PlatformInvoiceDetailDto>>(`${this.base}/${id}/issue`, {});
  }

  cancel(id: string, request: CancelPlatformInvoiceRequest): Observable<ApiResponse<PlatformInvoiceDetailDto>> {
    return this.http.post<ApiResponse<PlatformInvoiceDetailDto>>(`${this.base}/${id}/cancel`, request);
  }

  /** Récupère le PDF en blob — l'appelant peut faire `URL.createObjectURL`. */
  pdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/pdf`, { responseType: 'blob' });
  }

  addReceipt(invoiceId: string, request: CreatePlatformReceiptRequest): Observable<ApiResponse<PlatformReceiptDto>> {
    return this.http.post<ApiResponse<PlatformReceiptDto>>(`${this.base}/${invoiceId}/receipts`, request);
  }

  cancelReceipt(receiptId: string, request: CancelPlatformReceiptRequest): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/receipts/${receiptId}/cancel`, request);
  }

  confirmReceipt(receiptId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/receipts/${receiptId}/confirm`, {});
  }

  /** Lot C4 (complément) — Avoir partiel sur facture émise. */
  creditNote(id: string, request: IssueCreditNoteRequest): Observable<ApiResponse<PlatformInvoiceDetailDto>> {
    return this.http.post<ApiResponse<PlatformInvoiceDetailDto>>(`${this.base}/${id}/credit-note`, request);
  }

  /** Lot C4 (complément) — PDF d'un reçu (encaissement). */
  receiptPdf(receiptId: string): Observable<Blob> {
    return this.http.get(`${this.base}/receipts/${receiptId}/pdf`, { responseType: 'blob' });
  }

  /** Lot C4 (complément) — Agrégation TVA mensuelle plateforme (DGI). */
  vatPeriods(year: number): Observable<ApiResponse<PlatformVatPeriodDto[]>> {
    const hp = new HttpParams().set('year', String(year));
    return this.http.get<ApiResponse<PlatformVatPeriodDto[]>>(
      `${environment.apiUrl}/platform/fiscal/vat-periods`,
      { params: hp }
    );
  }
}
