import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { AppModule } from '@core/models/app-module';

/**
 * Wire shape of `GET /api/company/modules` (plan v1 §2.2 — tenant-scoped module
 * management). `allowedByPlan=false` ⇒ the module is locked behind a higher plan and
 * the UI must render it disabled with a "Plan supérieur requis" badge regardless of
 * `isEnabled`. `requires`/`requiredBy` carry the direct (non-transitive) dependency
 * edges — the UI closes the transitive dependency chain itself when toggling on.
 */
export interface CompanyModuleDto {
  id: AppModule;
  code: string;
  labelFr: string;
  isCore: boolean;
  isEnabled: boolean;
  allowedByPlan: boolean;
  requires: AppModule[];
  requiredBy: AppModule[];
  recommendedForSector: boolean;
}

export interface CompanyModulesResponse {
  planCode: string;
  modules: CompanyModuleDto[];
}

export interface UpdateCompanyModulesResponse {
  enabledModuleIds: AppModule[];
  warnings: string[];
}

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string | null;
  errors: string[];
}

@Injectable({ providedIn: 'root' })
export class CompanyModulesService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/company/modules`;

  getModules(): Observable<ApiResponse<CompanyModulesResponse>> {
    return this.http.get<ApiResponse<CompanyModulesResponse>>(this.API_URL);
  }

  updateModules(enabledModuleIds: AppModule[]): Observable<ApiResponse<UpdateCompanyModulesResponse>> {
    return this.http.put<ApiResponse<UpdateCompanyModulesResponse>>(this.API_URL, { enabledModuleIds });
  }
}
