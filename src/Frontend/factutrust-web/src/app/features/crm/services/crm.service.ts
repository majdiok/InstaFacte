import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

export interface OpportunityDto {
  id: string;
  title: string;
  clientId: string;
  clientName: string;
  assignedUserId: string;
  assignedUserName: string;
  stage: number;
  stageName: string;
  stageColor: string;
  expectedAmount: number;
  probability: number;
  weightedAmount: number;
  expectedCloseDate: string;
  actualCloseDate?: string | null;
  lostReason?: string | null;
  linkedQuoteId?: string | null;
  linkedInvoiceId?: string | null;
  notes?: string | null;
  source?: string | null;
  currency: string;
}

export interface PipelineSummaryDto {
  byStage: { stage: number; stageName: string; count: number; totalAmount: number; weightedAmount: number }[];
  totalWeightedAmount: number;
  conversionRate: number;
  averageCycleDays: number;
  totalOpen: number;
}

export interface SalesActivityDto {
  id: string;
  type: number;
  typeName: string;
  typeIcon: string;
  subject: string;
  description?: string | null;
  clientId: string;
  clientName?: string | null;
  opportunityId?: string | null;
  assignedUserId: string;
  assignedUserName: string;
  dueDate?: string | null;
  completedAt?: string | null;
  isCompleted: boolean;
  priority: number;
  priorityName: string;
  priorityColor: string;
  reminderDate?: string | null;
  linkedEntityType?: string | null;
  linkedEntityId?: string | null;
}

export interface SalesTargetDto {
  id: string;
  userId: string;
  userName: string;
  year: number;
  month: number;
  targetAmount: number;
  achievedAmount: number;
  progressPercent: number;
  currency: string;
}

export interface QuoteTemplateLineDto {
  id: string;
  productId: string;
  productName?: string | null;
  quantity: number;
  customUnitPrice?: number | null;
  discountPercent?: number | null;
  sortOrder: number;
}

export interface QuoteTemplateDto {
  id: string;
  name: string;
  description?: string | null;
  defaultNotes?: string | null;
  defaultTermsAndConditions?: string | null;
  defaultValidityDays: number;
  isActive: boolean;
  usageCount: number;
  lines: QuoteTemplateLineDto[];
}

export interface CreateQuoteTemplateLinePayload {
  productId: string;
  quantity: number;
  customUnitPrice?: number | null;
  discountPercent?: number | null;
  sortOrder: number;
}

export interface CreateQuoteTemplatePayload {
  name: string;
  description?: string | null;
  defaultNotes?: string | null;
  defaultTermsAndConditions?: string | null;
  defaultValidityDays: number;
  lines: CreateQuoteTemplateLinePayload[];
}

export interface UpdateQuoteTemplatePayload extends CreateQuoteTemplatePayload {
  isActive?: boolean | null;
}

export interface SalesDashboardDto {
  myRevenueThisMonth: number;
  myRevenueLastMonth: number;
  revenueDeltaPercent: number;
  monthlyTarget?: number | null;
  targetProgressPercent?: number | null;
  pendingQuotesCount: number;
  pendingQuotesAmount: number;
  quoteConversionRate: number;
  remindersToday: number;
  pipelineWeightedTotal: number;
  topOpportunities: OpportunityDto[];
  newClientsThisMonth: number;
  currency: string;
}

interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string | null;
  errors?: string[];
}

export interface CrmAssignableUserDto {
  id: string;
  displayName: string;
}

export interface PagedActivitiesResult {
  items: SalesActivityDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ActivitiesQueryParams {
  clientId?: string;
  assignedUserId?: string;
  opportunityId?: string;
  completed?: boolean;
  dueFrom?: string;
  dueTo?: string;
  activityType?: number;
  search?: string;
  page: number;
  pageSize: number;
}

/** Totaux agrégés (backend) de la liste des activités, sur l'ensemble filtré complet. */
export interface ActivityListSummary {
  count: number;
  openCount: number;
  completedCount: number;
  overdueCount: number;
}

@Injectable({ providedIn: 'root' })
export class CrmService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/crm`;

  getOpportunities(stage?: number, assignedUserId?: string, clientId?: string, context?: HttpContext): Observable<ApiResponse<OpportunityDto[]>> {
    let p = new HttpParams();
    if (stage !== undefined) p = p.set('stage', stage);
    if (assignedUserId) p = p.set('assignedUserId', assignedUserId);
    if (clientId) p = p.set('clientId', clientId);
    return this.http.get<ApiResponse<OpportunityDto[]>>(`${this.base}/opportunities`, { params: p, context });
  }

  getOpportunity(id: string): Observable<ApiResponse<OpportunityDto>> {
    return this.http.get<ApiResponse<OpportunityDto>>(`${this.base}/opportunities/${id}`);
  }

  createOpportunity(data: any): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.base}/opportunities`, data);
  }

  updateOpportunity(id: string, data: any): Observable<ApiResponse<boolean>> {
    return this.http.put<ApiResponse<boolean>>(`${this.base}/opportunities/${id}`, data);
  }

  advanceOpportunity(id: string): Observable<ApiResponse<boolean>> {
    return this.http.put<ApiResponse<boolean>>(`${this.base}/opportunities/${id}/advance`, {});
  }

  winOpportunity(id: string): Observable<ApiResponse<boolean>> {
    return this.http.put<ApiResponse<boolean>>(`${this.base}/opportunities/${id}/win`, {});
  }

  loseOpportunity(id: string, reason: string): Observable<ApiResponse<boolean>> {
    return this.http.put<ApiResponse<boolean>>(`${this.base}/opportunities/${id}/lose`, { reason });
  }

  deleteOpportunity(id: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/opportunities/${id}`);
  }

  getActivitiesPaged(params: ActivitiesQueryParams): Observable<ApiResponse<PagedActivitiesResult>> {
    let p = new HttpParams().set('page', String(params.page)).set('pageSize', String(params.pageSize));
    if (params.clientId) p = p.set('clientId', params.clientId);
    if (params.assignedUserId) p = p.set('assignedUserId', params.assignedUserId);
    if (params.opportunityId) p = p.set('opportunityId', params.opportunityId);
    if (params.completed !== undefined) p = p.set('completed', String(params.completed));
    if (params.dueFrom) p = p.set('dueFrom', params.dueFrom);
    if (params.dueTo) p = p.set('dueTo', params.dueTo);
    if (params.activityType !== undefined && params.activityType !== null) {
      p = p.set('activityType', String(params.activityType));
    }
    if (params.search?.trim()) p = p.set('search', params.search.trim());
    return this.http.get<ApiResponse<PagedActivitiesResult>>(`${this.base}/activities`, { params: p });
  }

  /** Totaux agrégés respectant les mêmes filtres que {@link getActivitiesPaged} (calcul backend). */
  getActivitiesSummary(params: Omit<ActivitiesQueryParams, 'page' | 'pageSize'>): Observable<ApiResponse<ActivityListSummary>> {
    let p = new HttpParams();
    if (params.clientId) p = p.set('clientId', params.clientId);
    if (params.assignedUserId) p = p.set('assignedUserId', params.assignedUserId);
    if (params.opportunityId) p = p.set('opportunityId', params.opportunityId);
    if (params.completed !== undefined) p = p.set('completed', String(params.completed));
    if (params.dueFrom) p = p.set('dueFrom', params.dueFrom);
    if (params.dueTo) p = p.set('dueTo', params.dueTo);
    if (params.activityType !== undefined && params.activityType !== null) {
      p = p.set('activityType', String(params.activityType));
    }
    if (params.search?.trim()) p = p.set('search', params.search.trim());
    return this.http.get<ApiResponse<ActivityListSummary>>(`${this.base}/activities/summary`, { params: p });
  }

  getAssignableUsers(): Observable<ApiResponse<CrmAssignableUserDto[]>> {
    return this.http.get<ApiResponse<CrmAssignableUserDto[]>>(`${this.base}/assignable-users`);
  }

  getMyReminders(context?: HttpContext): Observable<ApiResponse<SalesActivityDto[]>> {
    return this.http.get<ApiResponse<SalesActivityDto[]>>(`${this.base}/activities/my-reminders`, { context });
  }

  createActivity(data: any): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.base}/activities`, data);
  }

  updateActivity(id: string, data: any): Observable<ApiResponse<boolean>> {
    return this.http.put<ApiResponse<boolean>>(`${this.base}/activities/${id}`, data);
  }

  completeActivity(id: string): Observable<ApiResponse<boolean>> {
    return this.http.put<ApiResponse<boolean>>(`${this.base}/activities/${id}/complete`, {});
  }

  deleteActivity(id: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/activities/${id}`);
  }

  getTargets(year?: number, userId?: string, month?: number | null): Observable<ApiResponse<SalesTargetDto[]>> {
    let p = new HttpParams();
    if (year !== undefined && year !== null) p = p.set('year', String(year));
    if (userId) p = p.set('userId', userId);
    if (month !== undefined && month !== null && month >= 1 && month <= 12) {
      p = p.set('month', String(month));
    }
    return this.http.get<ApiResponse<SalesTargetDto[]>>(`${this.base}/targets`, { params: p });
  }

  getTargetsAssignableUsers(): Observable<ApiResponse<CrmAssignableUserDto[]>> {
    return this.http.get<ApiResponse<CrmAssignableUserDto[]>>(`${this.base}/targets/assignable-users`);
  }

  createTarget(data: {
    userId: string;
    userName: string;
    year: number;
    month: number;
    targetAmount: number;
  }): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.base}/targets`, data);
  }

  updateTarget(id: string, body: { targetAmount: number }): Observable<ApiResponse<boolean>> {
    return this.http.put<ApiResponse<boolean>>(`${this.base}/targets/${id}`, body);
  }

  deleteTarget(id: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/targets/${id}`);
  }

  getQuoteTemplates(options?: { activeOnly?: boolean; search?: string }): Observable<ApiResponse<QuoteTemplateDto[]>> {
    let p = new HttpParams();
    if (options?.activeOnly !== undefined) p = p.set('activeOnly', String(options.activeOnly));
    if (options?.search?.trim()) p = p.set('search', options.search.trim());
    return this.http.get<ApiResponse<QuoteTemplateDto[]>>(`${this.base}/quote-templates`, { params: p });
  }

  getQuoteTemplate(id: string): Observable<ApiResponse<QuoteTemplateDto>> {
    return this.http.get<ApiResponse<QuoteTemplateDto>>(`${this.base}/quote-templates/${id}`);
  }

  createQuoteTemplate(data: CreateQuoteTemplatePayload): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.base}/quote-templates`, data);
  }

  updateQuoteTemplate(id: string, data: UpdateQuoteTemplatePayload): Observable<ApiResponse<boolean>> {
    return this.http.put<ApiResponse<boolean>>(`${this.base}/quote-templates/${id}`, data);
  }

  setQuoteTemplateActive(id: string, isActive: boolean): Observable<ApiResponse<boolean>> {
    return this.http.patch<ApiResponse<boolean>>(`${this.base}/quote-templates/${id}/active`, { isActive });
  }

  deleteQuoteTemplate(id: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/quote-templates/${id}`);
  }
}
