import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  EmailMessageDetailDto,
  EmailMessageListParams,
  EmailMessagesPageDto
} from '@core/models/platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformEmailsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/email-messages`;

  list(params: EmailMessageListParams = {}): Observable<ApiResponse<EmailMessagesPageDto>> {
    let hp = new HttpParams();
    if (params.tenantId) hp = hp.set('tenantId', params.tenantId);
    if (params.status !== undefined) hp = hp.set('status', String(params.status));
    if (params.search?.trim()) hp = hp.set('search', params.search.trim());
    hp = hp.set('page', String(params.page ?? 1)).set('pageSize', String(params.pageSize ?? 25));
    return this.http.get<ApiResponse<EmailMessagesPageDto>>(this.base, { params: hp });
  }

  getById(id: string): Observable<ApiResponse<EmailMessageDetailDto>> {
    return this.http.get<ApiResponse<EmailMessageDetailDto>>(`${this.base}/${id}`);
  }

  retry(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/retry`, {});
  }

  sendTest(toEmail: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/send-test`, { toEmail });
  }
}
