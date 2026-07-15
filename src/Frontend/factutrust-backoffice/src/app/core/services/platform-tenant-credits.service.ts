import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  GrantTenantCreditRequest,
  RevokeTenantCreditRequest,
  TenantCreditDto,
  TenantCreditsPageDto
} from '@core/models/platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformTenantCreditsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform`;

  listAll(activeOnly?: boolean | null, page = 1, pageSize = 25): Observable<ApiResponse<TenantCreditsPageDto>> {
    let hp = new HttpParams();
    if (activeOnly !== undefined && activeOnly !== null) hp = hp.set('activeOnly', String(activeOnly));
    hp = hp.set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<ApiResponse<TenantCreditsPageDto>>(`${this.base}/credits`, { params: hp });
  }

  listByTenant(tenantId: string, page = 1, pageSize = 25): Observable<ApiResponse<TenantCreditsPageDto>> {
    let hp = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<ApiResponse<TenantCreditsPageDto>>(`${this.base}/tenants/${tenantId}/credits`, { params: hp });
  }

  grant(tenantId: string, request: GrantTenantCreditRequest): Observable<ApiResponse<TenantCreditDto>> {
    return this.http.post<ApiResponse<TenantCreditDto>>(`${this.base}/tenants/${tenantId}/credits`, request);
  }

  revoke(creditId: string, request: RevokeTenantCreditRequest): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/credits/${creditId}/revoke`, request);
  }
}
