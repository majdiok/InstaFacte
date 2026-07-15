import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  TenantModuleOverrideDto
} from '@core/models/platform.models';

/**
 * Lot C1 — Service HTTP pour les overrides de modules par tenant.
 *
 * Routes :
 * - `GET    /api/platform/tenants/{tenantId}/module-overrides`
 * - `POST   /api/platform/tenants/{tenantId}/module-overrides`
 * - `DELETE /api/platform/tenants/{tenantId}/module-overrides/{overrideId}`
 */
export interface SetTenantModuleOverrideRequest {
  module: number;
  isEnabled: boolean;
  expiresAt?: string;
  reason?: string;
}

@Injectable({ providedIn: 'root' })
export class PlatformTenantModuleOverridesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/tenants`;

  list(tenantId: string): Observable<ApiResponse<TenantModuleOverrideDto[]>> {
    return this.http.get<ApiResponse<TenantModuleOverrideDto[]>>(
      `${this.base}/${tenantId}/module-overrides`
    );
  }

  set(tenantId: string, request: SetTenantModuleOverrideRequest): Observable<ApiResponse<TenantModuleOverrideDto>> {
    return this.http.post<ApiResponse<TenantModuleOverrideDto>>(
      `${this.base}/${tenantId}/module-overrides`,
      request
    );
  }

  remove(tenantId: string, overrideId: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(
      `${this.base}/${tenantId}/module-overrides/${overrideId}`
    );
  }
}
