import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from '@core/services/auth.service';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';

export enum FiscalObligationType {
  MonthlyDeclaration = 0,
  ProvisionalCorporateTaxInstallment = 1,
  WithholdingTax = 2,
  Fodec = 3,
  QuarterlyVat = 4,
  FinancialStatements = 5,
  SemiAnnualFinancialStatements = 6,
  PersonalIncomeTaxInstallment = 7,
  CnssDtsQuarterly = 8,
  PayrollIrppWithholding = 9,
  Other = 99
}

export enum FiscalScheduleStatus {
  UpcomingWithin7Days = 0,
  UpcomingAfter7Days = 1,
  Overdue = 2,
  Deposited = 3,
  Paid = 4,
  Validated = 5,
  Cancelled = 9
}

export enum FiscalReminderChannel {
  Email = 0,
  InApp = 1,
  Sms = 2
}

export interface FiscalScheduleFilters {
  companyTenantId?: string | null;
  fiscalYear?: number | null;
  periodMonth?: number | null;
  periodQuarter?: number | null;
  obligationType?: number | null;
  status?: number | null;
  responsibleUserId?: string | null;
  dueFrom?: string | null;
  dueTo?: string | null;
  search?: string | null;
  includeCancelled?: boolean;
  page?: number;
  pageSize?: number;
}

export interface FiscalScheduleSummaryDto {
  upcomingWithin7DaysCount: number;
  upcomingWithin7DaysAmount: number;
  upcomingAfter7DaysCount: number;
  upcomingAfter7DaysAmount: number;
  overdueCount: number;
  overdueAmount: number;
  depositedThisMonthCount: number;
  depositedThisMonthAmount: number;
  totalCount: number;
  totalAmount: number;
}

export interface FiscalScheduleEntryDto {
  id: string;
  companyTenantId?: string | null;
  companyName?: string | null;
  obligationType: number;
  obligationTypeDisplay: string;
  obligationLabel: string;
  fiscalYear: number;
  periodMonth?: number | null;
  periodQuarter?: number | null;
  periodStart?: string | null;
  periodEnd?: string | null;
  periodDisplay: string;
  dueDate: string;
  estimatedAmount: number;
  currency: string;
  status: number;
  statusDisplay: string;
  sourceType: number;
  sourceId?: string | null;
  depositDate?: string | null;
  paymentDate?: string | null;
  responsibleUserId?: string | null;
  responsibleName?: string | null;
  observations?: string | null;
  lastReminderAt?: string | null;
  lastReminderChannel?: number | null;
  lastReminderChannelDisplay?: string | null;
  validatedAt?: string | null;
  validatedBy?: string | null;
  attachmentCount: number;
  historyCount: number;
  createdAt: string;
  updatedAt?: string | null;
  createdBy?: string | null;
  updatedBy?: string | null;
  version: number;
}

export interface FiscalScheduleListDto {
  summary: FiscalScheduleSummaryDto;
  items: FiscalScheduleEntryDto[];
  companies?: FirmClientDossierDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface FirmClientDossierDto {
  assignmentId: string;
  companyTenantId: string;
  companyName: string;
  activeSince: string;
}

export interface CreateFiscalScheduleEntryRequest {
  obligationType: number;
  obligationLabel: string;
  fiscalYear: number;
  periodMonth?: number | null;
  periodQuarter?: number | null;
  periodStart?: string | null;
  periodEnd?: string | null;
  dueDate: string;
  estimatedAmount: number;
  currency: string;
  responsibleUserId?: string | null;
  responsibleName?: string | null;
  observations?: string | null;
}

export type UpdateFiscalScheduleEntryRequest = CreateFiscalScheduleEntryRequest;

export interface MarkFiscalScheduleDepositedRequest {
  depositDate: string;
  observations?: string | null;
}

export interface CaptureFiscalSchedulePaymentRequest {
  paymentDate: string;
  observations?: string | null;
}

export interface ScheduleFiscalReminderRequest {
  channel: number;
  reminderAt?: string | null;
}

export interface MarkFiscalScheduleValidatedRequest {
  validatedDate: string;
  observations?: string | null;
}

export interface FiscalScheduleAttachmentDto {
  id: string;
  fiscalScheduleEntryId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
  uploadedBy?: string | null;
}

export interface FiscalScheduleHistoryDto {
  id: string;
  fiscalScheduleEntryId: string;
  action: string;
  summary: string;
  oldValuesJson?: string | null;
  newValuesJson?: string | null;
  createdAt: string;
  createdBy?: string | null;
}

/**
 * Responsable proposé pour une échéance : utilisateur actif du tenant d'APPARTENANCE de
 * l'appelant (en mode délégué : collaborateur du cabinet, pas utilisateur du dossier client).
 */
export interface FiscalAssignableUserDto {
  id: string;
  displayName: string;
}

@Injectable({ providedIn: 'root' })
export class FiscalScheduleService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/accounting/fiscal-schedule`;

  getSchedule(filters: FiscalScheduleFilters): Observable<ApiResponse<FiscalScheduleListDto>> {
    return this.http.get<ApiResponse<FiscalScheduleListDto>>(this.base, { params: toFiscalScheduleParams(filters) });
  }

  /**
   * Responsables proposés (collaborateurs du cabinet en mode délégué, utilisateurs de la
   * société en mode natif). Appel silencieux : un échec ne déclenche JAMAIS la modale
   * globale — l'écran reste utilisable sans le filtre Responsable.
   */
  getAssignableUsers(): Observable<ApiResponse<FiscalAssignableUserDto[]>> {
    return this.http.get<ApiResponse<FiscalAssignableUserDto[]>>(
      `${this.base}/assignable-users`, this.mutationOptions());
  }

  exportSchedule(filters: FiscalScheduleFilters): Observable<Blob> {
    return this.http.get(`${this.base}/export`, { params: toFiscalScheduleParams(filters), responseType: 'blob' });
  }

  ensureFiscalYear(fiscalYear: number): Observable<ApiResponse<number>> {
    return this.http.post<ApiResponse<number>>(`${this.base}/ensure/${fiscalYear}`, {}, this.mutationOptions());
  }

  create(request: CreateFiscalScheduleEntryRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(this.base, request, this.mutationOptions());
  }

  update(id: string, request: UpdateFiscalScheduleEntryRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.put<ApiResponse<FiscalScheduleEntryDto>>(`${this.base}/${id}`, request, this.mutationOptions());
  }

  delete(id: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/${id}`, this.mutationOptions());
  }

  markDeposited(id: string, request: MarkFiscalScheduleDepositedRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(`${this.base}/${id}/deposit`, request, this.mutationOptions());
  }

  capturePayment(id: string, request: CaptureFiscalSchedulePaymentRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(`${this.base}/${id}/payment`, request, this.mutationOptions());
  }

  scheduleReminder(id: string, request: ScheduleFiscalReminderRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(`${this.base}/${id}/reminder`, request, this.mutationOptions());
  }

  markValidated(id: string, request: MarkFiscalScheduleValidatedRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(`${this.base}/${id}/validate`, request, this.mutationOptions());
  }

  private mutationOptions(): { context: HttpContext } {
    return { context: createHttpContextSkipGlobalErrorUi() };
  }

  getHistory(id: string): Observable<ApiResponse<FiscalScheduleHistoryDto[]>> {
    return this.http.get<ApiResponse<FiscalScheduleHistoryDto[]>>(`${this.base}/${id}/history`);
  }

  getAttachments(id: string): Observable<ApiResponse<FiscalScheduleAttachmentDto[]>> {
    return this.http.get<ApiResponse<FiscalScheduleAttachmentDto[]>>(`${this.base}/${id}/attachments`);
  }

  uploadAttachment(id: string, file: File): Observable<ApiResponse<FiscalScheduleAttachmentDto>> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ApiResponse<FiscalScheduleAttachmentDto>>(`${this.base}/${id}/attachments`, form);
  }

  downloadAttachment(id: string, attachmentId: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/attachments/${attachmentId}/download`, { responseType: 'blob' });
  }

  deleteAttachment(id: string, attachmentId: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/${id}/attachments/${attachmentId}`);
  }
}

export function toFiscalScheduleParams(filters: FiscalScheduleFilters): HttpParams {
  let params = new HttpParams();
  const add = (key: keyof FiscalScheduleFilters, value: unknown) => {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(String(key), String(value));
    }
  };

  add('companyTenantId', filters.companyTenantId);
  add('fiscalYear', filters.fiscalYear);
  add('periodMonth', filters.periodMonth);
  add('periodQuarter', filters.periodQuarter);
  add('obligationType', filters.obligationType);
  add('status', filters.status);
  add('responsibleUserId', filters.responsibleUserId);
  add('dueFrom', filters.dueFrom);
  add('dueTo', filters.dueTo);
  add('search', filters.search);
  add('includeCancelled', filters.includeCancelled);
  add('page', filters.page ?? 1);
  add('pageSize', filters.pageSize ?? 25);
  return params;
}
