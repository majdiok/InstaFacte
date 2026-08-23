import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type { PlatformMeProfileDto, UpdatePlatformMeProfileRequest } from '@core/models/platform.models';
import type { UserSessionsPageDto, FailedLoginAttemptsPageDto } from '@core/models/platform.models';
import { PlatformAuthService } from './platform-auth.service';

@Injectable({ providedIn: 'root' })
export class PlatformMeService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(PlatformAuthService);
  private readonly base = `${environment.apiUrl}/platform/auth`;

  getProfile(): Observable<ApiResponse<PlatformMeProfileDto>> {
    return this.http.get<ApiResponse<PlatformMeProfileDto>>(`${this.base}/me`);
  }

  updateProfile(request: UpdatePlatformMeProfileRequest): Observable<ApiResponse<PlatformMeProfileDto>> {
    return this.http.put<ApiResponse<PlatformMeProfileDto>>(`${this.base}/me`, request).pipe(
      tap(res => {
        if (res.success && res.data) {
          this.auth.updateStoredUser({
            firstName: res.data.firstName,
            lastName: res.data.lastName
          });
        }
      })
    );
  }

  getMySessions(page = 1, pageSize = 25): Observable<ApiResponse<UserSessionsPageDto>> {
    return this.http.get<ApiResponse<UserSessionsPageDto>>(`${this.base}/me/sessions`, {
      params: { page: String(page), pageSize: String(pageSize) }
    });
  }

  getMyFailedLogins(page = 1, pageSize = 25): Observable<ApiResponse<FailedLoginAttemptsPageDto>> {
    return this.http.get<ApiResponse<FailedLoginAttemptsPageDto>>(`${this.base}/me/failed-logins`, {
      params: { page: String(page), pageSize: String(pageSize) }
    });
  }
}
