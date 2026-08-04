import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '@environments/environment';
import {
  HonorairesDocumentType,
  HonorairesInvoiceStatus,
  normalizeHonorairesDocumentType,
  normalizeHonorairesInvoiceStatus,
  normalizeHonorairesQuoteStatus
} from '../models/honoraires-invoice-status';

export interface HonorairesLineWrite {
  id?: string;
  activityCode?: string | null;
  designation: string;
  description?: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  discountPercent?: number | null;
}

export interface HonorairesLine extends HonorairesLineWrite {
  id: string;
  lineNumber: number;
  discountAmount: number;
  subTotal: number;
  vatAmount: number;
  total: number;
}

export interface UpsertHonorairesInvoice {
  firmClientAssignmentId: string;
  clientName: string;
  clientNif?: string;
  clientAddress?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  issueDate: string;
  dueDate?: string | null;
  currency: string;
  reference?: string;
  notes?: string;
  paymentTerms?: string;
  paymentMethod?: string;
  bankAccountLabel?: string;
  withholdingAmount: number;
  isRecurring: boolean;
  recurrenceFrequency?: number | null;
  sourceQuoteId?: string | null;
  lines: HonorairesLineWrite[];
}

export interface HonorairesInvoice {
  id: string;
  number?: string | null;
  issueDate: string;
  dueDate?: string | null;
  status: number;
  statusDisplay: string;
  type: number;
  isCreditNote: boolean;
  firmClientAssignmentId: string;
  clientName: string;
  clientNif?: string;
  clientAddress?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  reference?: string;
  notes?: string;
  paymentTerms?: string;
  paymentMethod?: string;
  bankAccountLabel?: string;
  currency: string;
  sourceQuoteId?: string;
  linkedInvoiceId?: string;
  linkedInvoiceNumber?: string | null;
  subTotal: number;
  totalVat: number;
  withholdingAmount: number;
  totalAmount: number;
  amountPaid: number;
  amountDue: number;
  isRecurring: boolean;
  recurrenceFrequency?: number | null;
  lines: HonorairesLine[];
  payments: HonorairesPayment[];
}

export interface HonorairesInvoiceListItem {
  id: string;
  number?: string | null;
  issueDate: string;
  dueDate?: string | null;
  status: number;
  statusDisplay: string;
  type: number;
  linkedInvoiceId?: string | null;
  clientName: string;
  firmClientAssignmentId: string;
  totalAmount: number;
  amountDue: number;
  currency: string;
}

export interface UpsertHonorairesQuote {
  firmClientAssignmentId: string;
  clientName: string;
  clientNif?: string;
  clientAddress?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  issueDate: string;
  validUntil?: string | null;
  currency: string;
  reference?: string;
  notes?: string;
  paymentTerms?: string;
  lines: HonorairesLineWrite[];
}

export interface HonorairesQuote {
  id: string;
  number?: string | null;
  issueDate: string;
  validUntil?: string | null;
  status: number;
  statusDisplay: string;
  firmClientAssignmentId: string;
  clientName: string;
  clientNif?: string;
  clientAddress?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  reference?: string;
  notes?: string;
  paymentTerms?: string;
  currency: string;
  convertedInvoiceId?: string;
  subTotal: number;
  totalVat: number;
  totalAmount: number;
  lines: HonorairesLine[];
}

export interface HonorairesQuoteListItem {
  id: string;
  number?: string | null;
  issueDate: string;
  status: number;
  statusDisplay: string;
  clientName: string;
  firmClientAssignmentId: string;
  totalAmount: number;
  currency: string;
}

export interface HonorairesPayment {
  id: string;
  honorairesInvoiceId: string;
  invoiceNumber?: string;
  clientName?: string;
  paymentDate: string;
  amount: number;
  clientWithholdingAmount: number;
  appliedAmount: number;
  method: number;
  methodDisplay: string;
  reference?: string;
  notes?: string;
  bankAccountLabel?: string;
}

export interface RecordHonorairesPayment {
  paymentDate: string;
  amount: number;
  clientWithholdingAmount: number;
  method: number;
  reference?: string;
  notes?: string;
  bankAccountLabel?: string;
}

export interface BillableDossier {
  assignmentId: string;
  companyTenantId: string;
  companyName: string;
  nif?: string;
  address?: string;
  contactEmail?: string;
  contactPhone?: string;
  annualFeeAmount?: number;
  billingFrequency?: number;
  suggestedLineDesignation?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
  error?: string;
}

type HonorairesInvoiceListItemApi = Omit<HonorairesInvoiceListItem, 'status' | 'type'> & {
  status: string | number;
  type: string | number;
};

type HonorairesInvoiceApi = Omit<HonorairesInvoice, 'status' | 'type'> & {
  status: string | number;
  type: string | number;
};

type HonorairesQuoteListItemApi = Omit<HonorairesQuoteListItem, 'status'> & {
  status: string | number;
};

type HonorairesQuoteApi = Omit<HonorairesQuote, 'status'> & {
  status: string | number;
};

function mapInvoiceListItem(dto: HonorairesInvoiceListItemApi): HonorairesInvoiceListItem {
  const status = normalizeHonorairesInvoiceStatus(dto.status) ?? HonorairesInvoiceStatus.Draft;
  const type = normalizeHonorairesDocumentType(dto.type) ?? HonorairesDocumentType.Invoice;
  return {
    ...dto,
    status,
    type
  };
}

function mapInvoice(dto: HonorairesInvoiceApi): HonorairesInvoice {
  const status = normalizeHonorairesInvoiceStatus(dto.status) ?? HonorairesInvoiceStatus.Draft;
  const type = normalizeHonorairesDocumentType(dto.type) ?? HonorairesDocumentType.Invoice;
  return {
    ...dto,
    status,
    type
  };
}

function mapQuoteListItem(dto: HonorairesQuoteListItemApi): HonorairesQuoteListItem {
  const status = normalizeHonorairesQuoteStatus(dto.status) ?? 0;
  return { ...dto, status };
}

function mapQuote(dto: HonorairesQuoteApi): HonorairesQuote {
  const status = normalizeHonorairesQuoteStatus(dto.status) ?? 0;
  return { ...dto, status };
}

@Injectable({ providedIn: 'root' })
export class HonorairesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/honoraires`;

  listInvoices(params: {
    type?: number;
    status?: number;
    assignmentId?: string;
    search?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<HonorairesInvoiceListItem>> {
    let httpParams = new HttpParams();
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') httpParams = httpParams.set(k, String(v));
    });
    return this.http
      .get<ApiResponse<PagedResult<HonorairesInvoiceListItemApi>>>(`${this.base}/invoices`, { params: httpParams })
      .pipe(
        map(r => ({
          ...r.data,
          items: r.data.items.map(mapInvoiceListItem)
        }))
      );
  }

  getInvoice(id: string): Observable<HonorairesInvoice> {
    return this.http
      .get<ApiResponse<HonorairesInvoiceApi>>(`${this.base}/invoices/${id}`)
      .pipe(map(r => mapInvoice(r.data)));
  }

  createInvoice(dto: UpsertHonorairesInvoice): Observable<string> {
    return this.http.post<ApiResponse<string>>(`${this.base}/invoices`, dto).pipe(map(r => r.data));
  }

  updateInvoice(id: string, dto: UpsertHonorairesInvoice): Observable<void> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/invoices/${id}`, dto).pipe(map(() => void 0));
  }

  validateInvoice(id: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/invoices/${id}/validate`, {}).pipe(map(() => void 0));
  }

  createCreditNote(dto: {
    linkedInvoiceId: string;
    issueDate: string;
    dueDate?: string | null;
    notes?: string;
    lines?: HonorairesLineWrite[];
  }): Observable<string> {
    return this.http.post<ApiResponse<string>>(`${this.base}/invoices/credit-notes`, dto).pipe(map(r => r.data));
  }

  previewInvoiceNumber(type = 0): Observable<string> {
    return this.http
      .get<ApiResponse<string>>(`${this.base}/invoices/preview-number`, { params: { type } })
      .pipe(map(r => r.data));
  }

  downloadInvoicePdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/invoices/${id}/pdf`, { responseType: 'blob' });
  }

  listQuotes(params: {
    status?: number;
    assignmentId?: string;
    search?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<HonorairesQuoteListItem>> {
    let httpParams = new HttpParams();
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') httpParams = httpParams.set(k, String(v));
    });
    return this.http
      .get<ApiResponse<PagedResult<HonorairesQuoteListItemApi>>>(`${this.base}/quotes`, { params: httpParams })
      .pipe(
        map(r => ({
          ...r.data,
          items: r.data.items.map(mapQuoteListItem)
        }))
      );
  }

  getQuote(id: string): Observable<HonorairesQuote> {
    return this.http
      .get<ApiResponse<HonorairesQuoteApi>>(`${this.base}/quotes/${id}`)
      .pipe(map(r => mapQuote(r.data)));
  }

  createQuote(dto: UpsertHonorairesQuote): Observable<string> {
    return this.http.post<ApiResponse<string>>(`${this.base}/quotes`, dto).pipe(map(r => r.data));
  }

  updateQuote(id: string, dto: UpsertHonorairesQuote): Observable<void> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/quotes/${id}`, dto).pipe(map(() => void 0));
  }

  sendQuote(id: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/quotes/${id}/send`, {}).pipe(map(() => void 0));
  }

  acceptQuote(id: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/quotes/${id}/accept`, {}).pipe(map(() => void 0));
  }

  convertQuote(id: string): Observable<string> {
    return this.http.post<ApiResponse<string>>(`${this.base}/quotes/${id}/convert`, {}).pipe(map(r => r.data));
  }

  downloadQuotePdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/quotes/${id}/pdf`, { responseType: 'blob' });
  }

  listPayments(invoiceId?: string): Observable<HonorairesPayment[]> {
    let params = new HttpParams();
    if (invoiceId) params = params.set('invoiceId', invoiceId);
    return this.http
      .get<ApiResponse<HonorairesPayment[]>>(`${this.base}/payments`, { params })
      .pipe(map(r => r.data));
  }

  recordPayment(invoiceId: string, dto: RecordHonorairesPayment): Observable<string> {
    return this.http
      .post<ApiResponse<string>>(`${this.base}/invoices/${invoiceId}/payments`, dto)
      .pipe(map(r => r.data));
  }

  listDossiers(): Observable<BillableDossier[]> {
    return this.http.get<ApiResponse<BillableDossier[]>>(`${this.base}/dossiers`).pipe(map(r => r.data));
  }

  listAttachments(kind: number, documentId: string): Observable<HonorairesAttachmentItem[]> {
    return this.http
      .get<ApiResponse<HonorairesAttachmentItem[]>>(`${this.base}/attachments`, {
        params: { kind, documentId }
      })
      .pipe(map(r => r.data));
  }

  uploadAttachment(kind: number, documentId: string, file: File): Observable<string> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http
      .post<ApiResponse<string>>(`${this.base}/attachments`, form, {
        params: { kind, documentId }
      })
      .pipe(map(r => r.data));
  }

  deleteAttachment(attachmentId: string): Observable<void> {
    return this.http
      .delete<ApiResponse<unknown>>(`${this.base}/attachments/${attachmentId}`)
      .pipe(map(() => void 0));
  }
}

export interface HonorairesAttachmentItem {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
}
