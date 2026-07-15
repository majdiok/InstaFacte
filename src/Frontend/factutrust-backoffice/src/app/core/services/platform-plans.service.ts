import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  BillingPeriodValue,
  ClonePlanRequest,
  PlanDto,
  PlanFeatureDto,
  PlanLimitDto,
  PlanModuleDto
} from '@core/models/platform.models';

/** Lot C1 — Création / mise à jour d'un plan (CRUD complet, body identique côté serveur). */
export interface CreatePlanRequest {
  code: string;
  name: string;
  description?: string | null;
  billingPeriod: BillingPeriodValue;
  basePriceTND: number;
  isPublic: boolean;
  trialDays: number;
  sortOrder: number;
  currency: string;
  limits: PlanLimitDto[];
  features: PlanFeatureDto[];
  modules: PlanModuleDto[];
}

export interface UpdatePlanRequest {
  name: string;
  description?: string | null;
  billingPeriod: BillingPeriodValue;
  basePriceTND: number;
  isPublic: boolean;
  trialDays: number;
  sortOrder: number;
  currency: string;
  limits: PlanLimitDto[];
  features: PlanFeatureDto[];
  modules: PlanModuleDto[];
}

@Injectable({ providedIn: 'root' })
export class PlatformPlansService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/plans`;

  list(includeArchived = false): Observable<ApiResponse<PlanDto[]>> {
    let hp = new HttpParams();
    if (includeArchived) hp = hp.set('includeArchived', 'true');
    return this.http.get<ApiResponse<PlanDto[]>>(this.base, { params: hp });
  }

  get(id: string): Observable<ApiResponse<PlanDto>> {
    return this.http.get<ApiResponse<PlanDto>>(`${this.base}/${id}`);
  }

  create(request: CreatePlanRequest): Observable<ApiResponse<PlanDto>> {
    return this.http.post<ApiResponse<PlanDto>>(this.base, request);
  }

  update(id: string, request: UpdatePlanRequest): Observable<ApiResponse<PlanDto>> {
    return this.http.put<ApiResponse<PlanDto>>(`${this.base}/${id}`, request);
  }

  archive(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/archive`, {});
  }

  reactivate(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/reactivate`, {});
  }

  clone(id: string, request: ClonePlanRequest): Observable<ApiResponse<PlanDto>> {
    return this.http.post<ApiResponse<PlanDto>>(`${this.base}/${id}/clone`, request);
  }
}
