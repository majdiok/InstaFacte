import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '@environments/environment';
import { AuthResponse } from '@core/services/auth.service';

export interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  message?: string | null;
  errors?: string[];
}

export interface PortalMe {
  sellerName: string;
  sellerTradeName: string | null;
  sellerLogoUrl: string | null;
  sellerEmail: string | null;
  bankName: string | null;
  iban: string | null;
  rib: string | null;
  clientId: string;
  clientName: string;
  clientEmail: string | null;
  clientNif: string | null;
  contactEmail: string;
  contactName: string;
}

export interface PortalInvoiceListItem {
  id: string;
  number: string;
  type: string;
  isCreditNote: boolean;
  issueDate: string;
  dueDate: string | null;
  status: string;
  statusDisplay: string;
  totalAmount: number;
  totalPaid: number;
  remainingAmount: number;
  currency: string;
}

export interface PortalInvoiceLine {
  description: string;
  quantity: number;
  unitPrice: number;
  vatRatePercent: number;
  lineTotal: number;
}

export interface PortalPayment {
  id: string;
  invoiceId: string;
  invoiceNumber: string | null;
  paymentDate: string;
  amount: number;
  methodDisplay: string;
  reference: string | null;
}

export interface PortalInvoiceDetail extends PortalInvoiceListItem {
  reference: string | null;
  notes: string | null;
  paymentTerms: string | null;
  subTotal: number;
  totalVat: number;
  lines: PortalInvoiceLine[];
  payments: PortalPayment[];
}

export interface PortalSummary {
  unpaidAmount: number;
  unpaidCount: number;
  overdueAmount: number;
  totalOutstanding: number;
  upcomingInvoices: PortalInvoiceListItem[];
}

export interface PortalStatementLine {
  date: string;
  kind: string;
  label: string;
  debit: number;
  credit: number;
  balance: number;
}

export interface PortalStatement {
  clientName: string;
  fromDate: string | null;
  toDate: string | null;
  openingBalance: number;
  closingBalance: number;
  lines: PortalStatementLine[];
}

export interface PagedPortalInvoices {
  items: PortalInvoiceListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ClientPortalContact {
  id: string;
  userId: string;
  email: string;
  displayName: string;
  status: string;
  statusDisplay: string;
  invitedAt: string;
  acceptedAt: string | null;
  lastAccessAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class PortalService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/portal`;

  getMe(): Observable<PortalMe> {
    return this.unwrap(this.http.get<ApiEnvelope<PortalMe>>(`${this.base}/me`));
  }

  getSummary(): Observable<PortalSummary> {
    return this.unwrap(this.http.get<ApiEnvelope<PortalSummary>>(`${this.base}/summary`));
  }

  getInvoices(params: {
    status?: string;
    fromDate?: string;
    toDate?: string;
    unpaidOnly?: boolean;
    page?: number;
    pageSize?: number;
  }): Observable<PagedPortalInvoices> {
    return this.unwrap(
      this.http.get<ApiEnvelope<PagedPortalInvoices>>(`${this.base}/invoices`, { params: params as never })
    );
  }

  getInvoice(id: string): Observable<PortalInvoiceDetail> {
    return this.unwrap(this.http.get<ApiEnvelope<PortalInvoiceDetail>>(`${this.base}/invoices/${id}`));
  }

  downloadInvoicePdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/invoices/${id}/pdf`, { responseType: 'blob' });
  }

  getPayments(fromDate?: string, toDate?: string): Observable<PortalPayment[]> {
    return this.unwrap(
      this.http.get<ApiEnvelope<PortalPayment[]>>(`${this.base}/payments`, {
        params: { fromDate, toDate } as never
      })
    );
  }

  getStatement(fromDate?: string, toDate?: string): Observable<PortalStatement> {
    return this.unwrap(
      this.http.get<ApiEnvelope<PortalStatement>>(`${this.base}/statement`, {
        params: { fromDate, toDate } as never
      })
    );
  }

  listContacts(clientId: string): Observable<ClientPortalContact[]> {
    return this.unwrap(
      this.http.get<ApiEnvelope<ClientPortalContact[]>>(
        `${environment.apiUrl}/clients/${clientId}/portal-contacts`
      )
    );
  }

  inviteContact(
    clientId: string,
    body: { email: string; firstName: string; lastName: string }
  ): Observable<ClientPortalContact> {
    return this.unwrap(
      this.http.post<ApiEnvelope<ClientPortalContact>>(
        `${environment.apiUrl}/clients/${clientId}/portal-contacts`,
        body
      )
    );
  }

  resendInvite(clientId: string, contactId: string): Observable<unknown> {
    return this.unwrap(
      this.http.post<ApiEnvelope<unknown>>(
        `${environment.apiUrl}/clients/${clientId}/portal-contacts/${contactId}/resend`,
        {}
      )
    );
  }

  revokeContact(clientId: string, contactId: string): Observable<unknown> {
    return this.unwrap(
      this.http.post<ApiEnvelope<unknown>>(
        `${environment.apiUrl}/clients/${clientId}/portal-contacts/${contactId}/revoke`,
        {}
      )
    );
  }

  acceptInvite(body: { token: string; password: string; confirmPassword: string }): Observable<AuthResponse> {
    return this.unwrap(
      this.http.post<ApiEnvelope<AuthResponse>>(`${environment.apiUrl}/auth/portal/accept-invite`, body)
    );
  }

  private unwrap<T>(source: Observable<ApiEnvelope<T>>): Observable<T> {
    return source.pipe(
      map(res => {
        if (!res.success || res.data === undefined || res.data === null) {
          throw new Error(res.errors?.[0] || res.message || 'Erreur portail');
        }
        return res.data;
      })
    );
  }
}
