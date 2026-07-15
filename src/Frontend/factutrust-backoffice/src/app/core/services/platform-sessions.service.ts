import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type { SessionListParams, UserSessionsPageDto } from '@core/models/platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformSessionsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/sessions`;

  list(params: SessionListParams = {}): Observable<ApiResponse<UserSessionsPageDto>> {
    let hp = new HttpParams();
    if (params.activeOnly !== undefined && params.activeOnly !== null) {
      hp = hp.set('activeOnly', String(params.activeOnly));
    }
    hp = hp.set('page', String(params.page ?? 1)).set('pageSize', String(params.pageSize ?? 25));
    return this.http.get<ApiResponse<UserSessionsPageDto>>(this.base, { params: hp });
  }

  listByUser(userId: string, params: SessionListParams = {}): Observable<ApiResponse<UserSessionsPageDto>> {
    let hp = new HttpParams()
      .set('page', String(params.page ?? 1))
      .set('pageSize', String(params.pageSize ?? 25));
    return this.http.get<ApiResponse<UserSessionsPageDto>>(`${this.base}/users/${userId}`, { params: hp });
  }

  revoke(id: string, reason = 'ManualRevoke'): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/revoke`, { reason });
  }

  revokeAllByUser(userId: string, reason = 'AdminRevokeAll'): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/users/${userId}/revoke-all`, { reason });
  }
}
