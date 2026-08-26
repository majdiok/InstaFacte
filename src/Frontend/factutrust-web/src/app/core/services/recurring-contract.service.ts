import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse, PagedResult } from './client.service';

// Les enums sont alignés verbatim sur les noms C# : l'API sérialise en strings
// PascalCase (JsonStringEnumConverter) et accepte les mêmes noms en entrée.
export type RecurringContractStatus = 'Draft' | 'Active' | 'Suspended' | 'Cancelled' | 'Expired';
export type BillingFrequency = 'Monthly' | 'Quarterly' | 'Annual' | 'OneOff';
export type RecurringContractLineType = 'FixedRecurring' | 'UsageMetered' | 'OneTimeSetup';
export type BillingRunStatus = 'Pending' | 'DraftCreated' | 'Invoiced' | 'Failed' | 'Skipped';
export type UsageAggregationMode = 'Sum' | 'Max' | 'Last';
export type UsageRecordSource = 'Manual' | 'Import' | 'Api';
export type AmendmentType =
  | 'Upgrade'
  | 'Downgrade'
  | 'AddLine'
  | 'RemoveLine'
  | 'PriceChange'
  | 'Suspend'
  | 'Resume'
  | 'Renewal';
export type ProrationPolicy = 'None' | 'DailyProration';
export type ScheduleEntryStatus = 'Upcoming' | 'Invoiced' | 'DraftGenerated' | 'Overdue' | 'Failed' | 'Skipped';

export interface RecurringContractListItem {
  id: string;
  number?: string | null;
  clientId: string;
  clientName: string;
  status: RecurringContractStatus;
  statusDisplay: string;
  billingFrequency: BillingFrequency;
  billingFrequencyDisplay: string;
  startDate: string;
  endDate?: string | null;
  nextBillingDate?: string | null;
  currency: string;
  estimatedMonthlyAmount: number;
}

export interface RecurringContractLine {
  id?: string;
  lineType: RecurringContractLineType;
  lineTypeDisplay?: string;
  productId?: string | null;
  productName?: string | null;
  description: string;
  quantity: number;
  unitPriceHT: number;
  vatRate: number;
  usageMetricId?: string | null;
  usageMetricName?: string | null;
  includedQuantity?: number | null;
  overageUnitPriceHT?: number | null;
  effectiveFrom?: string;
  effectiveTo?: string | null;
  isActive?: boolean;
  sortOrder?: number;
}

export interface RecurringContractDetail {
  id: string;
  number?: string | null;
  clientId: string;
  clientName: string;
  status: RecurringContractStatus;
  statusDisplay: string;
  billingFrequency: BillingFrequency;
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
  // Champs enrichis de GET /{id}/detail (phase 2) — absents sur le GET simple.
  estimatedMonthlyAmount?: number | null;
  upcomingOccurrencesCount?: number | null;
  cancellationDeadline?: string | null;
  currentPeriodTotalHT?: number | null;
  currentPeriodTotalTVA?: number | null;
  currentPeriodTotalTTC?: number | null;
}

export interface UpsertRecurringContractPayload {
  clientId: string;
  billingFrequency: BillingFrequency;
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
  lineType: RecurringContractLineType;
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
  aggregationMode: UsageAggregationMode;
  aggregationModeDisplay?: string;
  productId?: string | null;
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
  source: UsageRecordSource;
  sourceDisplay: string;
  notes?: string | null;
  createdAt?: string;
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

/** GET /{id}/billing-runs — historique des passages de facturation. */
export interface RecurringContractBillingRun {
  id: string;
  recurringContractId: string;
  contractNumber?: string | null;
  periodFrom: string;
  periodTo: string;
  status: BillingRunStatus;
  statusDisplay: string;
  invoiceDraftId?: string | null;
  invoiceId?: string | null;
  fixedAmount: number;
  usageAmount: number;
  prorationAmount: number;
  totalAmount: number;
  errorMessage?: string | null;
  createdAt: string;
}

/** POST /{id}/amend — avenant (lignes sur contrat actif, effet immédiat). */
export interface AmendRecurringContractPayload {
  amendmentType: AmendmentType;
  effectiveDate: string;
  prorationPolicy: ProrationPolicy;
  notes?: string | null;
  updatedContract?: UpsertRecurringContractPayload | null;
}

/** POST/PUT /usage-metrics. */
export interface UpsertUsageMetricPayload {
  code: string;
  name: string;
  unit: string;
  aggregationMode: UsageAggregationMode;
  productId?: string | null;
  isActive: boolean;
}

/** POST /{id}/usage-records/import — une ligne du CSV d'import. */
export interface ImportUsageRecordRow {
  metricCode: string;
  periodFrom: string;
  periodTo: string;
  quantity: number;
  notes?: string | null;
}

// ---- Interfaces phase 2 (plan maître §4) — endpoints tolérants 404 ----

export interface ContractScheduleEntry {
  date: string;
  periodFrom: string;
  periodTo: string;
  description: string;
  estimatedAmountHT: number;
  estimatedAmountTTC: number;
  status: ScheduleEntryStatus;
  statusDisplay: string;
  billingRunId?: string | null;
  invoiceDraftId?: string | null;
  invoiceId?: string | null;
}

export interface ContractFinancialSummary {
  contractId: string;
  windowFrom: string;
  windowTo: string;
  isOpenEnded: boolean;
  totalContractAmount: number;
  totalInvoicedAmount: number;
  remainingAmount: number;
  percentInvoiced: number;
  invoicedRunsCount: number;
  totalRunsCount: number;
  currency: string;
}

export interface LinkedInvoice {
  invoiceId: string;
  number: string;
  date: string;
  dueDate: string | null;
  amountHT: number;
  amountTTC: number;
  status: string;
  statusDisplay: string;
  isCreditNote: boolean;
  billingRunId?: string | null;
  periodFrom?: string | null;
  periodTo?: string | null;
}

export interface ContractAmendment {
  id: string;
  type: AmendmentType;
  typeDisplay: string;
  effectiveDate: string;
  prorationPolicy: ProrationPolicy;
  prorationPolicyDisplay: string;
  notes?: string | null;
  createdAt: string;
  createdByUserId?: string | null;
  createdByUserName?: string | null;
}

export interface ContractEvolutionPoint {
  month: string;
  amount: number;
}

export interface ContractStats {
  activeCount: number;
  estimatedMonthlyRecurringTotal: number;
  dueSoonCount: number;
  pendingDraftsCount: number;
  currency: string;
}

@Injectable({ providedIn: 'root' })
export class RecurringContractService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/recurring-contracts`;

  list(params: {
    search?: string;
    status?: RecurringContractStatus;
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

  listUsageRecords(contractId: string, from?: string, to?: string): Observable<UsageRecord[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<ApiResponse<UsageRecord[]>>(`${this.base}/${contractId}/usage-records`, { params })
      .pipe(map(r => r.data ?? []));
  }

  convertFromQuote(quoteId: string, payload: UpsertRecurringContractPayload): Observable<string> {
    return this.http.post<ApiResponse<string>>(`${this.base}/convert-from-quote/${quoteId}`, payload)
      .pipe(map(r => r.data!));
  }

  // ---- Endpoints existants nouvellement exploités ----

  listBillingRuns(contractId: string): Observable<RecurringContractBillingRun[]> {
    return this.http.get<ApiResponse<RecurringContractBillingRun[]>>(`${this.base}/${contractId}/billing-runs`)
      .pipe(map(r => r.data ?? []));
  }

  amend(contractId: string, payload: AmendRecurringContractPayload): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${contractId}/amend`, payload)
      .pipe(map(() => undefined));
  }

  createUsageMetric(payload: UpsertUsageMetricPayload): Observable<string> {
    return this.http.post<ApiResponse<string>>(`${this.base}/usage-metrics`, payload)
      .pipe(map(r => r.data!));
  }

  updateUsageMetric(id: string, payload: UpsertUsageMetricPayload): Observable<void> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/usage-metrics/${id}`, payload)
      .pipe(map(() => undefined));
  }

  importUsageRecords(contractId: string, rows: ImportUsageRecordRow[]): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.base}/${contractId}/usage-records/import`, rows)
      .pipe(map(r => r.data ?? 0));
  }

  // ---- Endpoints phase 2 (plan maître §4) — 404 = « pas encore déployé » → null ----

  /** GET /{id}/detail — vue enrichie (KPI). Repli attendu sur get() côté page si null. */
  getDetail(contractId: string): Observable<RecurringContractDetail | null> {
    return this.http.get<ApiResponse<RecurringContractDetail>>(`${this.base}/${contractId}/detail`)
      .pipe(map(r => r.data ?? null), this.nullIfNotFound());
  }

  getSchedule(contractId: string, count = 12): Observable<ContractScheduleEntry[] | null> {
    const params = new HttpParams().set('count', String(count));
    return this.http.get<ApiResponse<ContractScheduleEntry[]>>(`${this.base}/${contractId}/schedule`, { params })
      .pipe(map(r => r.data ?? []), this.nullIfNotFound());
  }

  getFinancialSummary(contractId: string): Observable<ContractFinancialSummary | null> {
    return this.http.get<ApiResponse<ContractFinancialSummary>>(`${this.base}/${contractId}/financial-summary`)
      .pipe(map(r => r.data ?? null), this.nullIfNotFound());
  }

  getLinkedInvoices(contractId: string): Observable<LinkedInvoice[] | null> {
    return this.http.get<ApiResponse<LinkedInvoice[]>>(`${this.base}/${contractId}/linked-invoices`)
      .pipe(map(r => r.data ?? []), this.nullIfNotFound());
  }

  getAmendments(contractId: string): Observable<ContractAmendment[] | null> {
    return this.http.get<ApiResponse<ContractAmendment[]>>(`${this.base}/${contractId}/amendments`)
      .pipe(map(r => r.data ?? []), this.nullIfNotFound());
  }

  /**
   * POST /{id}/clone — action utilisateur : PAS de repli silencieux sur 404.
   * La page appelante affiche un toast info « après mise à jour du serveur ».
   */
  clone(contractId: string, payload?: { startDate?: string; clientId?: string; reference?: string }): Observable<string> {
    return this.http.post<ApiResponse<string>>(`${this.base}/${contractId}/clone`, payload ?? {})
      .pipe(map(r => r.data!));
  }

  /** POST /{id}/renew — même traitement 404 que clone (toast info côté page). */
  renewNow(contractId: string, payload?: { notes?: string }): Observable<{ newEndDate: string }> {
    return this.http.post<ApiResponse<{ newEndDate: string }>>(`${this.base}/${contractId}/renew`, payload ?? {})
      .pipe(map(r => r.data!));
  }

  getEvolution(contractId: string, months = 6): Observable<ContractEvolutionPoint[] | null> {
    const params = new HttpParams().set('months', String(months));
    return this.http.get<ApiResponse<ContractEvolutionPoint[]>>(`${this.base}/${contractId}/evolution`, { params })
      .pipe(map(r => r.data ?? []), this.nullIfNotFound());
  }

  getStats(): Observable<ContractStats | null> {
    return this.http.get<ApiResponse<ContractStats>>(`${this.base}/stats`)
      .pipe(map(r => r.data ?? null), this.nullIfNotFound());
  }

  /**
   * PATCH /{id}/notes — édition des notes à tout statut (plan maître §4.10).
   * 404 (endpoint non déployé) → 'unavailable' : l'onglet Notes bascule en lecture seule.
   */
  updateNotes(contractId: string, notes: string | null): Observable<'ok' | 'unavailable'> {
    return this.http.patch<ApiResponse<unknown>>(`${this.base}/${contractId}/notes`, { notes }).pipe(
      map(() => 'ok' as const),
      catchError(err => (err?.status === 404 ? of('unavailable' as const) : throwError(() => err)))
    );
  }

  /** 404 → null (endpoint phase 2 non déployé) ; toute autre erreur remonte. */
  private nullIfNotFound<T>(): (src: Observable<T>) => Observable<T | null> {
    return (src) => src.pipe(catchError(err => (err?.status === 404 ? of(null) : throwError(() => err))));
  }
}
