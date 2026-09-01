import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { AppModule } from '@core/models/app-module';

/**
 * Wire shape of `GET /api/company/module-recommendations` (plan §3.3) — explainable,
 * rule-based usage recommendations. Never auto-applied; always dismissible by the tenant.
 */
export interface ModuleRecommendationDto {
  moduleId: AppModule;
  reasonCode: string;
  reasonFr: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string | null;
  errors: string[];
}

/**
 * Plan §3.3 — fetches and dismisses rule-based module usage recommendations for the
 * current tenant. Read-only nudges: activating a recommendation still goes through
 * `CompanyModulesService.updateModules()` (PUT /api/company/modules); this service only
 * lists and dismisses. Dismissing is permanent per tenant/module pair.
 */
@Injectable({ providedIn: 'root' })
export class ModuleRecommendationsService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/company/module-recommendations`;

  getRecommendations(): Observable<ApiResponse<ModuleRecommendationDto[]>> {
    return this.http.get<ApiResponse<ModuleRecommendationDto[]>>(this.API_URL);
  }

  dismiss(moduleId: AppModule): Observable<ApiResponse<boolean>> {
    return this.http.post<ApiResponse<boolean>>(`${this.API_URL}/${moduleId}/dismiss`, {});
  }
}
