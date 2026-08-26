import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { ApiResponse } from '@core/services/auth.service';
import {
  FirmLeaveBalance,
  FirmLeaveCalendarEntry,
  FirmLeaveOverview,
  FirmLeaveReconciliation,
  FirmLeaveReplayResult,
  FirmLeaveRequest,
  FirmLeaveSettings,
  FirmLeaveType
} from './firm-leaves.models';

@Injectable({ providedIn: 'root' })
export class FirmLeavesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/firm/governance`;

  getOverview(year?: number): Observable<ApiResponse<FirmLeaveOverview>> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    return this.http.get<ApiResponse<FirmLeaveOverview>>(`${this.base}/leaves/overview`, { params });
  }

  getCalendar(from: string, to: string): Observable<ApiResponse<FirmLeaveCalendarEntry[]>> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<ApiResponse<FirmLeaveCalendarEntry[]>>(`${this.base}/leaves/calendar`, { params });
  }

  listRequests(filter: {
    userId?: string;
    status?: number;
    typeId?: string;
    from?: string;
    to?: string;
    year?: number;
  } = {}): Observable<ApiResponse<FirmLeaveRequest[]>> {
    let params = new HttpParams();
    if (filter.userId) params = params.set('userId', filter.userId);
    if (filter.status != null) params = params.set('status', filter.status);
    if (filter.typeId) params = params.set('typeId', filter.typeId);
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    if (filter.year) params = params.set('year', filter.year);
    return this.http.get<ApiResponse<FirmLeaveRequest[]>>(`${this.base}/leaves`, { params });
  }

  getRequest(id: string): Observable<ApiResponse<FirmLeaveRequest>> {
    return this.http.get<ApiResponse<FirmLeaveRequest>>(`${this.base}/leaves/${id}`);
  }

  computeDays(body: {
    startDate: string;
    endDate: string;
    startUnit: number;
    endUnit: number;
  }): Observable<ApiResponse<{ days: number }>> {
    return this.http.post<ApiResponse<{ days: number }>>(`${this.base}/leaves/compute-days`, body);
  }

  create(body: {
    userId?: string;
    leaveTypeId: string;
    startDate: string;
    endDate: string;
    startUnit: number;
    endUnit: number;
    reason?: string;
    submitImmediately?: boolean;
  }): Observable<ApiResponse<FirmLeaveRequest>> {
    return this.http.post<ApiResponse<FirmLeaveRequest>>(
      `${this.base}/leaves`, body, { context: createHttpContextSkipGlobalErrorUi() });
  }

  update(id: string, body: {
    leaveTypeId: string;
    startDate: string;
    endDate: string;
    startUnit: number;
    endUnit: number;
    reason?: string;
  }): Observable<ApiResponse<FirmLeaveRequest>> {
    return this.http.put<ApiResponse<FirmLeaveRequest>>(
      `${this.base}/leaves/${id}`, body, { context: createHttpContextSkipGlobalErrorUi() });
  }

  submit(id: string): Observable<ApiResponse<FirmLeaveRequest>> {
    return this.http.post<ApiResponse<FirmLeaveRequest>>(`${this.base}/leaves/${id}/submit`, {});
  }

  cancel(id: string): Observable<ApiResponse<FirmLeaveRequest>> {
    return this.http.post<ApiResponse<FirmLeaveRequest>>(`${this.base}/leaves/${id}/cancel`, {});
  }

  process(id: string, approve: boolean, rejectionReason?: string): Observable<ApiResponse<FirmLeaveRequest>> {
    return this.http.post<ApiResponse<FirmLeaveRequest>>(`${this.base}/leaves/${id}/process`, {
      approve,
      rejectionReason
    });
  }

  listBalances(year?: number): Observable<ApiResponse<FirmLeaveBalance[]>> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    return this.http.get<ApiResponse<FirmLeaveBalance[]>>(`${this.base}/leaves/balances`, { params });
  }

  getMyBalance(year?: number): Observable<ApiResponse<FirmLeaveBalance>> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    return this.http.get<ApiResponse<FirmLeaveBalance>>(`${this.base}/leaves/balances/me`, { params });
  }

  setBalance(userId: string, year: number, body: {
    openingBalanceDays: number;
    adjustmentDays: number;
    notes?: string;
  }): Observable<ApiResponse<FirmLeaveBalance>> {
    const params = new HttpParams().set('year', year);
    return this.http.put<ApiResponse<FirmLeaveBalance>>(`${this.base}/leaves/balances/${userId}`, body, { params });
  }

  listTypes(activeOnly = true): Observable<ApiResponse<FirmLeaveType[]>> {
    const params = new HttpParams().set('activeOnly', activeOnly);
    return this.http.get<ApiResponse<FirmLeaveType[]>>(`${this.base}/leaves/types`, { params });
  }

  upsertType(body: Partial<FirmLeaveType> & { code: string; label: string }): Observable<ApiResponse<FirmLeaveType>> {
    return this.http.post<ApiResponse<FirmLeaveType>>(`${this.base}/leaves/types`, body);
  }

  /** Écarts entre congés approuvés et report en paie interne. */
  getReconciliation(year: number): Observable<ApiResponse<FirmLeaveReconciliation>> {
    const params = new HttpParams().set('year', year);
    return this.http.get<ApiResponse<FirmLeaveReconciliation>>(
      `${this.base}/leaves/reconciliation`, { params });
  }

  /** Rejoue le report d'une demande, ou de toutes celles en écart si l'id est omis. */
  replayPayrollMirror(year: number, leaveRequestId?: string): Observable<ApiResponse<FirmLeaveReplayResult>> {
    let params = new HttpParams().set('year', year);
    if (leaveRequestId) params = params.set('leaveRequestId', leaveRequestId);
    return this.http.post<ApiResponse<FirmLeaveReplayResult>>(
      `${this.base}/leaves/reconciliation/replay`, {}, { params });
  }

  getSettings(year: number): Observable<ApiResponse<FirmLeaveSettings>> {
    return this.http.get<ApiResponse<FirmLeaveSettings>>(`${this.base}/leaves/settings/${year}`);
  }

  updateSettings(year: number, body: Omit<FirmLeaveSettings, 'id' | 'year'>): Observable<ApiResponse<FirmLeaveSettings>> {
    return this.http.put<ApiResponse<FirmLeaveSettings>>(`${this.base}/leaves/settings/${year}`, body);
  }

  exportList(year?: number, status?: number, typeId?: string): Observable<Blob> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    if (status != null) params = params.set('status', status);
    if (typeId) params = params.set('typeId', typeId);
    return this.http.get(`${this.base}/leaves/export`, { params, responseType: 'blob' });
  }

  exportSynthesis(year?: number): Observable<Blob> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    return this.http.get(`${this.base}/leaves/export/synthesis`, { params, responseType: 'blob' });
  }
}
