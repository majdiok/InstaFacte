import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  FailedLoginAttemptsPageDto,
  FailedLoginListParams
} from '@core/models/platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformSecurityService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/security`;

  failedLogins(params: FailedLoginListParams = {}): Observable<ApiResponse<FailedLoginAttemptsPageDto>> {
    let hp = new HttpParams();
    if (params.email?.trim()) hp = hp.set('email', params.email.trim());
    if (params.ipAddress?.trim()) hp = hp.set('ipAddress', params.ipAddress.trim());
    if (params.from) hp = hp.set('from', params.from);
    if (params.to) hp = hp.set('to', params.to);
    hp = hp.set('page', String(params.page ?? 1)).set('pageSize', String(params.pageSize ?? 25));
    return this.http.get<ApiResponse<FailedLoginAttemptsPageDto>>(`${this.base}/failed-logins`, {
      params: hp
    });
  }
}
