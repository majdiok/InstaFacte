import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
}

export interface AccountingAuditDashboardDto {
  fiscalYear: number;
  blockingCount: number;
  warningCount: number;
  anomalyCount: number;
  infoCount: number;
  complianceRate: number;
  complianceRateDeltaVsPriorYear?: number | null;
  lastRun?: {
    runId: string;
    completedAt: string;
    duration: string;
    triggeredByUserName?: string | null;
  } | null;
}

export interface AccountingAuditCorrectionLinkDto {
  route: string;
  queryParams: Record<string, string>;
  label?: string | null;
}

export interface AccountingAnomalyListItemDto {
  id: string;
  ruleCode: string;
  moduleCode: string;
  severity: number;
  category: number;
  title: string;
  description: string;
  detailSummary: string;
  accountRef?: string | null;
  periodFrom?: string | null;
  periodTo?: string | null;
  amount: number;
  status: number;
  assignedToUserId?: string | null;
  assignedToUserName?: string | null;
  deepLinkRoute?: string | null;
  correctionLink?: AccountingAuditCorrectionLinkDto | null;
  lineCount: number;
  detectedAt: string;
}

export interface PagedAnomaliesDto {
  items: AccountingAnomalyListItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  openCount: number;
  ignoredCount: number;
}

export interface AccountingAnomalyDetailDto {
  id: string;
  ruleCode: string;
  moduleCode: string;
  severity: number;
  category: number;
  title: string;
  description: string;
  impact: string;
  accountRef?: string | null;
  amount: number;
  periodFrom?: string | null;
  periodTo?: string | null;
  status: number;
  assignedToUserId?: string | null;
  assignedToUserName?: string | null;
  detectedAt: string;
  deepLinkRoute?: string | null;
  correctionLink?: AccountingAuditCorrectionLinkDto | null;
  recommendations: string[];
  lines: {
    id: string;
    journalEntryId?: string | null;
    entryDate?: string | null;
    accountNumber?: string | null;
    label?: string | null;
    debit: number;
    credit: number;
    pieceRef?: string | null;
    justificationStatus?: string | null;
  }[];
  activities: {
    id: string;
    activityType: number;
    userName?: string | null;
    message: string;
    createdAt: string;
  }[];
}

export interface AccountingAuditAnalyticsDto {
  byCategory: { category: number; label: string; count: number }[];
  trend: { label: string; blocking: number; warning: number; info: number }[];
  topAccounts: { accountNumber: string; accountLabel?: string | null; anomalyCount: number; totalAmount: number }[];
  riskRadar: { axis: string; currentScore: number; sectorAverage: number }[];
  recentActivity: { id: string; activityType: number; userName?: string | null; message: string; createdAt: string }[];
}

export interface AccountingControlModuleDto {
  code: string;
  label: string;
  icon: string;
  count: number;
}

export interface AccountingAuditRunResultDto {
  runId: string;
  fiscalYear: number;
  complianceRate: number;
  totalAnomalies: number;
  blockingCount: number;
  warningCount: number;
  infoCount: number;
  dashboard?: AccountingAuditDashboardDto | null;
}

export interface AnomalyFilter {
  fiscalYear: number;
  severity?: number;
  category?: number;
  status?: number;
  account?: string;
  search?: string;
  moduleCode?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class AccountingAuditService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/accounting/audit`;

  runAudit(fiscalYear: number, periodFrom?: string, periodTo?: string): Observable<ApiResponse<AccountingAuditRunResultDto>> {
    return this.http.post<ApiResponse<AccountingAuditRunResultDto>>(`${this.base}/runs`, {
      fiscalYear,
      periodFrom: periodFrom ?? null,
      periodTo: periodTo ?? null
    });
  }

  getDashboard(fiscalYear: number): Observable<ApiResponse<AccountingAuditDashboardDto>> {
    return this.http.get<ApiResponse<AccountingAuditDashboardDto>>(`${this.base}/dashboard`, {
      params: { fiscalYear: fiscalYear.toString() }
    });
  }

  getAnomalies(filter: AnomalyFilter): Observable<ApiResponse<PagedAnomaliesDto>> {
    let params = new HttpParams().set('fiscalYear', filter.fiscalYear.toString());
    if (filter.severity != null) params = params.set('severity', filter.severity);
    if (filter.status != null) params = params.set('status', filter.status);
    if (filter.category != null) params = params.set('category', filter.category);
    if (filter.account) params = params.set('account', filter.account);
    if (filter.search) params = params.set('search', filter.search);
    if (filter.moduleCode) params = params.set('moduleCode', filter.moduleCode);
    params = params.set('page', (filter.page ?? 1).toString());
    params = params.set('pageSize', (filter.pageSize ?? 12).toString());
    return this.http.get<ApiResponse<PagedAnomaliesDto>>(`${this.base}/anomalies`, { params });
  }

  getAnomalyDetail(id: string): Observable<ApiResponse<AccountingAnomalyDetailDto>> {
    return this.http.get<ApiResponse<AccountingAnomalyDetailDto>>(`${this.base}/anomalies/${id}`);
  }

  getAnalytics(fiscalYear: number): Observable<ApiResponse<AccountingAuditAnalyticsDto>> {
    return this.http.get<ApiResponse<AccountingAuditAnalyticsDto>>(`${this.base}/analytics`, {
      params: { fiscalYear: fiscalYear.toString() }
    });
  }

  getModules(fiscalYear: number): Observable<ApiResponse<AccountingControlModuleDto[]>> {
    return this.http.get<ApiResponse<AccountingControlModuleDto[]>>(`${this.base}/modules`, {
      params: { fiscalYear: fiscalYear.toString() }
    });
  }

  assignAnomaly(id: string, assigneeUserId?: string, assigneeUserName?: string): Observable<ApiResponse<AccountingAnomalyDetailDto>> {
    return this.http.patch<ApiResponse<AccountingAnomalyDetailDto>>(`${this.base}/anomalies/${id}/assign`, {
      assigneeUserId: assigneeUserId ?? null,
      assigneeUserName: assigneeUserName ?? null
    });
  }

  resolveAnomaly(id: string): Observable<ApiResponse<AccountingAnomalyDetailDto>> {
    return this.http.post<ApiResponse<AccountingAnomalyDetailDto>>(`${this.base}/anomalies/${id}/resolve`, {});
  }

  ignoreAnomaly(id: string, reason: string): Observable<ApiResponse<AccountingAnomalyDetailDto>> {
    return this.http.post<ApiResponse<AccountingAnomalyDetailDto>>(`${this.base}/anomalies/${id}/ignore`, { reason });
  }

  exportCsv(fiscalYear: number): Observable<Blob> {
    return this.http.get(`${this.base}/export`, {
      params: { fiscalYear: fiscalYear.toString(), format: 'csv' },
      responseType: 'blob'
    });
  }

  exportPdf(fiscalYear: number): Observable<Blob> {
    return this.http.get(`${this.base}/export`, {
      params: { fiscalYear: fiscalYear.toString(), format: 'pdf' },
      responseType: 'blob'
    });
  }
}
