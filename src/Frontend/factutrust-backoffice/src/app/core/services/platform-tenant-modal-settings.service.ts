import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  TenantModalSettingsDto,
  UpdateTenantModalSettingsRequest
} from '@core/models/platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformTenantModalSettingsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/tenants`;

  get(tenantId: string): Observable<ApiResponse<TenantModalSettingsDto>> {
    return this.http.get<ApiResponse<TenantModalSettingsDto>>(
      `${this.base}/${tenantId}/modal-settings`
    );
  }

  update(
    tenantId: string,
    request: UpdateTenantModalSettingsRequest
  ): Observable<ApiResponse<TenantModalSettingsDto>> {
    return this.http.put<ApiResponse<TenantModalSettingsDto>>(
      `${this.base}/${tenantId}/modal-settings`,
      request
    );
  }

  remove(tenantId: string): Observable<ApiResponse<TenantModalSettingsDto>> {
    return this.http.delete<ApiResponse<TenantModalSettingsDto>>(
      `${this.base}/${tenantId}/modal-settings`
    );
  }
}
