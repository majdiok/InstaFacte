import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse, ProjectTimeEntry } from '../projects/project-api.service';

export interface TimesheetGridDayCell {
  date: string;
  hours: number;
  expectedHours: number;
  colorCode: string;
}

export interface TimesheetGridProjectRow {
  projectId: string;
  projectName: string;
  timesheetsEnabled: boolean;
  days: TimesheetGridDayCell[];
  periodTotalHours: number;
  expectedPeriodHours: number;
  periodColorCode: string;
}

export interface TimesheetBillingRateKpi {
  billableLoggedHours: number;
  targetHours: number;
  completionPercent: number;
  totalLoggedHours: number;
  targetReached: boolean;
}

export interface TimesheetLeaderboardEntry {
  userId: string;
  userName: string;
  billableLoggedHours: number;
  targetHours: number;
  completionPercent: number;
  totalLoggedHours: number;
  rank: number;
}

export interface TimesheetLeaderboard {
  topThree: TimesheetLeaderboardEntry[];
  fullRanking: TimesheetLeaderboardEntry[];
  currentUser?: TimesheetBillingRateKpi | null;
  dailyTip?: string | null;
  mode: string;
}

export interface TimesheetGrid {
  from: string;
  to: string;
  rows: TimesheetGridProjectRow[];
  dailyTotals: TimesheetGridDayCell[];
  billingRateKpi?: TimesheetBillingRateKpi | null;
  leaderboard?: TimesheetLeaderboard | null;
}

export interface TimesheetTimerState {
  entryId: string;
  projectId: string;
  projectName: string;
  taskId?: string | null;
  startedAtUtc: string;
}

export interface TenantTimesheetSettings {
  billingRateIndicatorsEnabled: boolean;
  billingRateLeaderboardEnabled: boolean;
  timeOffEntriesEnabled: boolean;
  encodingMethod: number;
  timeOffProjectId?: string | null;
  timeOffTaskId?: string | null;
  defaultDailyWorkingHours: number;
}

export interface ProjectProfitabilityLine {
  category: string;
  expected: number;
  toInvoiceOrBill: number;
  invoicedOrBilled: number;
}

export interface ProjectProfitability {
  projectId: string;
  revenues: ProjectProfitabilityLine[];
  costs: ProjectProfitabilityLine[];
  totalRevenueExpected: number;
  totalRevenueToInvoice: number;
  totalRevenueInvoiced: number;
  totalCostExpected: number;
  totalCostToBill: number;
  totalCostBilled: number;
  marginInvoiced: number;
}

@Injectable({ providedIn: 'root' })
export class TimesheetApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/timesheets`;
  private readonly projects = `${environment.apiUrl}/api/projects`;

  getGrid(from: Date, to: Date, userId?: string): Observable<ApiResponse<TimesheetGrid>> {
    let params = new HttpParams()
      .set('from', from.toISOString())
      .set('to', to.toISOString());
    if (userId) params = params.set('userId', userId);
    return this.http.get<ApiResponse<TimesheetGrid>>(`${this.base}/grid`, { params });
  }

  getLeaderboard(year: number, month: number, mode = 'billingRate'): Observable<ApiResponse<TimesheetLeaderboard>> {
    const params = new HttpParams().set('year', year).set('month', month).set('mode', mode);
    return this.http.get<ApiResponse<TimesheetLeaderboard>>(`${this.base}/leaderboard`, { params });
  }

  getSettings(): Observable<ApiResponse<TenantTimesheetSettings>> {
    return this.http.get<ApiResponse<TenantTimesheetSettings>>(`${this.base}/settings`);
  }

  startTimer(body: { projectId: string; taskId?: string; salesOrderLineId?: string; notes?: string }): Observable<ApiResponse<TimesheetTimerState>> {
    return this.http.post<ApiResponse<TimesheetTimerState>>(`${this.base}/timer/start`, body);
  }

  stopTimer(entryId: string): Observable<ApiResponse<number>> {
    return this.http.post<ApiResponse<number>>(`${this.base}/timer/${entryId}/stop`, {});
  }

  getActiveTimer(): Observable<ApiResponse<TimesheetTimerState | null>> {
    return this.http.get<ApiResponse<TimesheetTimerState | null>>(`${this.base}/timer/active`);
  }

  getValidationQueue(): Observable<ApiResponse<ProjectTimeEntry[]>> {
    return this.http.get<ApiResponse<ProjectTimeEntry[]>>(`${this.base}/validation-queue`);
  }

  getProfitability(projectId: string): Observable<ApiResponse<ProjectProfitability>> {
    return this.http.get<ApiResponse<ProjectProfitability>>(`${this.projects}/${projectId}/profitability`);
  }
}
