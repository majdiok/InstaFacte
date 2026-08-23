import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse, PagedResult } from './client.service';

export type RecurringContractStatus = 'Draft' | 'Active' | 'Suspended' | 'Cancelled' | 'Expired';
export type BillingFrequency = 'Monthly' | 'Quarterly' | 'Annual';
export type RecurringContractLineType = 'FixedRecurring' | 'UsageMetered' | 'OneTimeSetup';

export interface RecurringContractListItem {
  id: string;
  number?: string | null;
  clientId: string;
  clientName: string;
  status: number;
  statusDisplay: string;
  billingFrequency: number;
  billingFrequencyDisplay: string;
  startDate: string;
  endDate?: string | null;
  nextBillingDate?: string | null;
  currency: string;
  estimatedMonthlyAmount: number;
}

export interface RecurringContractLine {
  id?: string;
  lineType: number;
  lineTypeDisplay?: string;
  productId?: string | null;
  description: string;
  quantity: number;
  unitPriceHT: number;
  vatRate: number;
  usageMetricId?: string | null;
  usageMetricName?: string | null;
  includedQuantity?: number | null;
  overageUnitPriceHT?: number | null;
  sortOrder?: number;
}

export interface RecurringContractDetail {
  id: string;
  number?: string | null;
  clientId: string;
  clientName: string;
  status: number;
  statusDisplay: string;
  billingFrequency: number;
  billingFrequencyDisplay: string;
  billingDayOfMonth: number;
  startDate: string;
  endDate?: string | null;
  nextBillingDate?: string | null;
  lastBilledPeriodEnd?: string | null;
  paymentTermTemplateId?: string | null;
  priceListId?: string | null;
  autoRenew: boolean;
  noticePeriodDays: number;
  currency: string;
  sourceQuoteId?: string | null;
  reference?: string | null;
  notes?: string | null;
  setupFeeBilled: boolean;
  lines: RecurringContractLine[];
}

export interface UpsertRecurringContractPayload {
  clientId: string;
  billingFrequency: number;
  billingDayOfMonth: number;
  startDate: string;
  endDate?: string | null;
  autoRenew: boolean;
  noticePeriodDays: number;
  paymentTermTemplateId?: string | null;
  priceListId?: string | null;
  sourceQuoteId?: string | null;
  reference?: string | null;
  notes?: string | null;
  lines: RecurringContractLinePayload[];
}

export interface RecurringContractLinePayload {
  id?: string | null;
  lineType: number;
  productId?: string | null;
  description: string;
  quantity: number;
  unitPriceHT: number;
  vatRate: number;
  usageMetricId?: string | null;
  includedQuantity?: number | null;
  overageUnitPriceHT?: number | null;
  sortOrder: number;
}

export interface UsageMetric {
  id: string;
  code: string;
  name: string;
  unit: string;
  aggregationMode: number;
  isActive: boolean;
}

export interface UsageRecord {
  id: string;
  recurringContractId: string;
  usageMetricId: string;
  usageMetricName: string;
  periodFrom: string;
  periodTo: string;
  quantity: number;
  source: number;
  sourceDisplay: string;
  notes?: string | null;
}

export interface PendingRecurringDraft {
  billingRunId: string;
  recurringContractId: string;
  contractNumber?: string | null;
  clientName: string;
  invoiceDraftId: string;
  periodFrom: string;
  periodTo: string;
  totalAmount: number;
}

@Injectable({ providedIn: 'root' })
export class RecurringContractService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/recurring-contracts`;

  list(params: {
    search?: string;
    status?: number;
    clientId?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<RecurringContractListItem>> {
    let httpParams = new HttpParams();
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.status != null) httpParams = httpParams.set('status', params.status);
    if (params.clientId) httpParams = httpParams.set('clientId', params.clientId);
    httpParams = httpParams.set('page', String(params.page ?? 1));
    httpParams = httpParams.set('pageSize', String(params.pageSize ?? 20));
    return this.http.get<ApiResponse<PagedResult<RecurringContractListItem>>>(this.base, { params: httpParams })
      .pipe(map(r => r.data!));
  }

  get(id: string): Observable<RecurringContractDetail> {
    return this.http.get<ApiResponse<RecurringContractDetail>>(`${this.base}/${id}`)
      .pipe(map(r => r.data!));
  }

  create(payload: UpsertRecurringContractPayload): Observable<string> {
    return this.http.post<ApiResponse<string>>(this.base, payload).pipe(map(r => r.data!));
  }

  update(id: string, payload: UpsertRecurringContractPayload): Observable<void> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/${id}`, payload).pipe(map(() => undefined));
  }

  activate(id: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/activate`, {}).pipe(map(() => undefined));
  }

  suspend(id: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/suspend`, {}).pipe(map(() => undefined));
  }

  resume(id: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/resume`, {}).pipe(map(() => undefined));
  }

  cancel(id: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/cancel`, {}).pipe(map(() => undefined));
  }

  listPendingDrafts(): Observable<PendingRecurringDraft[]> {
    return this.http.get<ApiResponse<PendingRecurringDraft[]>>(`${this.base}/pending-drafts`)
      .pipe(map(r => r.data ?? []));
  }

  triggerBilling(contractId?: string): Observable<number> {
    let params = new HttpParams();
    if (contractId) params = params.set('contractId', contractId);
    return this.http.post<ApiResponse<number>>(`${this.base}/billing-runs/trigger`, {}, { params })
      .pipe(map(r => r.data ?? 0));
  }

  listUsageMetrics(): Observable<UsageMetric[]> {
    return this.http.get<ApiResponse<UsageMetric[]>>(`${this.base}/usage-metrics`)
      .pipe(map(r => r.data ?? []));
  }

  recordUsage(contractId: string, payload: {
    usageMetricId: string;
    periodFrom: string;
    periodTo: string;
    quantity: number;
    notes?: string;
  }): Observable<string> {
    return this.http.post<ApiResponse<string>>(`${this.base}/${contractId}/usage-records`, payload)
      .pipe(map(r => r.data!));
  }

  listUsageRecords(contractId: string): Observable<UsageRecord[]> {
    return this.http.get<ApiResponse<UsageRecord[]>>(`${this.base}/${contractId}/usage-records`)
      .pipe(map(r => r.data ?? []));
  }

  convertFromQuote(quoteId: string, payload: UpsertRecurringContractPayload): Observable<string> {
    return this.http.post<ApiResponse<string>>(`${this.base}/convert-from-quote/${quoteId}`, payload)
      .pipe(map(r => r.data!));
  }
}
