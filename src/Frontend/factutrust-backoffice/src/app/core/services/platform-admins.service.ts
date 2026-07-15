import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  ChangePlatformAdminRoleRequest,
  CreatePlatformAdminRequest,
  PlatformAdminListItemDto,
  PlatformAdminListPageDto,
  ResetPlatformAdminPasswordRequest
} from '@core/models/platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformAdminsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/admins`;

  list(): Observable<ApiResponse<PlatformAdminListPageDto>> {
    return this.http.get<ApiResponse<PlatformAdminListPageDto>>(this.base);
  }

  create(request: CreatePlatformAdminRequest): Observable<ApiResponse<PlatformAdminListItemDto>> {
    return this.http.post<ApiResponse<PlatformAdminListItemDto>>(this.base, request);
  }

  changeRole(id: string, request: ChangePlatformAdminRoleRequest): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/role`, request);
  }

  disable(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/disable`, {});
  }

  enable(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/enable`, {});
  }

  resetPassword(id: string, request: ResetPlatformAdminPasswordRequest): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/reset-password`, request);
  }
}
