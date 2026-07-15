import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  CouponDto,
  CouponRedemptionDto,
  CouponsPageDto,
  CreateCouponRequest,
  UpdateCouponRequest
} from '@core/models/platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformCouponsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/coupons`;

  list(activeOnly?: boolean | null, search?: string, page = 1, pageSize = 25): Observable<ApiResponse<CouponsPageDto>> {
    let hp = new HttpParams();
    if (activeOnly !== undefined && activeOnly !== null) hp = hp.set('activeOnly', String(activeOnly));
    if (search?.trim()) hp = hp.set('search', search.trim());
    hp = hp.set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<ApiResponse<CouponsPageDto>>(this.base, { params: hp });
  }

  get(id: string): Observable<ApiResponse<CouponDto>> {
    return this.http.get<ApiResponse<CouponDto>>(`${this.base}/${id}`);
  }

  create(request: CreateCouponRequest): Observable<ApiResponse<CouponDto>> {
    return this.http.post<ApiResponse<CouponDto>>(this.base, request);
  }

  update(id: string, request: UpdateCouponRequest): Observable<ApiResponse<CouponDto>> {
    return this.http.put<ApiResponse<CouponDto>>(`${this.base}/${id}`, request);
  }

  deactivate(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/deactivate`, {});
  }

  reactivate(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/reactivate`, {});
  }

  redemptions(id: string): Observable<ApiResponse<CouponRedemptionDto[]>> {
    return this.http.get<ApiResponse<CouponRedemptionDto[]>>(`${this.base}/${id}/redemptions`);
  }
}
