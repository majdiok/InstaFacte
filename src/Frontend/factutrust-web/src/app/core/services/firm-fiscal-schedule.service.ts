import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import {
  CaptureFiscalSchedulePaymentRequest,
  CreateFiscalScheduleEntryRequest,
  FiscalScheduleAttachmentDto,
  FiscalScheduleEntryDto,
  FiscalScheduleFilters,
  FiscalScheduleHistoryDto,
  FiscalScheduleListDto,
  MarkFiscalScheduleDepositedRequest,
  MarkFiscalScheduleValidatedRequest,
  ScheduleFiscalReminderRequest,
  toFiscalScheduleParams,
  UpdateFiscalScheduleEntryRequest
} from '../../features/accounting/services/fiscal-schedule.service';

@Injectable({ providedIn: 'root' })
export class FirmFiscalScheduleService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/firm/fiscal-schedule`;

  getSchedule(filters: FiscalScheduleFilters): Observable<ApiResponse<FiscalScheduleListDto>> {
    return this.http.get<ApiResponse<FiscalScheduleListDto>>(this.base, { params: toFiscalScheduleParams(filters) });
  }

  getCompanies(): Observable<ApiResponse<FiscalScheduleListDto['companies']>> {
    return this.http.get<ApiResponse<FiscalScheduleListDto['companies']>>(`${this.base}/companies`);
  }

  ensureFiscalYear(companyTenantId: string, fiscalYear: number): Observable<ApiResponse<number>> {
    return this.http.post<ApiResponse<number>>(
      `${this.base}/${companyTenantId}/ensure/${fiscalYear}`,
      {},
      this.mutationOptions()
    );
  }

  create(companyTenantId: string, request: CreateFiscalScheduleEntryRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(
      `${this.base}/${companyTenantId}/entries`,
      request,
      this.mutationOptions()
    );
  }

  update(companyTenantId: string, id: string, request: UpdateFiscalScheduleEntryRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.put<ApiResponse<FiscalScheduleEntryDto>>(
      `${this.base}/${companyTenantId}/entries/${id}`,
      request,
      this.mutationOptions()
    );
  }

  delete(companyTenantId: string, id: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(
      `${this.base}/${companyTenantId}/entries/${id}`,
      this.mutationOptions()
    );
  }

  markDeposited(companyTenantId: string, id: string, request: MarkFiscalScheduleDepositedRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(
      `${this.base}/${companyTenantId}/entries/${id}/deposit`,
      request,
      this.mutationOptions()
    );
  }

  capturePayment(companyTenantId: string, id: string, request: CaptureFiscalSchedulePaymentRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(
      `${this.base}/${companyTenantId}/entries/${id}/payment`,
      request,
      this.mutationOptions()
    );
  }

  markValidated(companyTenantId: string, id: string, request: MarkFiscalScheduleValidatedRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(
      `${this.base}/${companyTenantId}/entries/${id}/validate`,
      request,
      this.mutationOptions()
    );
  }

  scheduleReminder(companyTenantId: string, id: string, request: ScheduleFiscalReminderRequest): Observable<ApiResponse<FiscalScheduleEntryDto>> {
    return this.http.post<ApiResponse<FiscalScheduleEntryDto>>(
      `${this.base}/${companyTenantId}/entries/${id}/reminder`,
      request,
      this.mutationOptions()
    );
  }

  getHistory(companyTenantId: string, id: string): Observable<ApiResponse<FiscalScheduleHistoryDto[]>> {
    return this.http.get<ApiResponse<FiscalScheduleHistoryDto[]>>(`${this.base}/${companyTenantId}/entries/${id}/history`);
  }

  getAttachments(companyTenantId: string, id: string): Observable<ApiResponse<FiscalScheduleAttachmentDto[]>> {
    return this.http.get<ApiResponse<FiscalScheduleAttachmentDto[]>>(`${this.base}/${companyTenantId}/entries/${id}/attachments`);
  }

  uploadAttachment(companyTenantId: string, id: string, file: File): Observable<ApiResponse<FiscalScheduleAttachmentDto>> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ApiResponse<FiscalScheduleAttachmentDto>>(`${this.base}/${companyTenantId}/entries/${id}/attachments`, form);
  }

  downloadAttachment(companyTenantId: string, id: string, attachmentId: string): Observable<Blob> {
    return this.http.get(
      `${this.base}/${companyTenantId}/entries/${id}/attachments/${attachmentId}/download`,
      { responseType: 'blob' }
    );
  }

  deleteAttachment(companyTenantId: string, id: string, attachmentId: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(
      `${this.base}/${companyTenantId}/entries/${id}/attachments/${attachmentId}`,
      this.mutationOptions()
    );
  }

  private mutationOptions() {
    return { context: createHttpContextSkipGlobalErrorUi() };
  }
}
