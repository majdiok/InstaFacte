import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { AppModule } from '@core/models/app-module';

/** Wire shape of `GET /api/company/sector` (plan v1 §2.3). */
export interface SectorOptionDto {
  code: string;
  labelFr: string;
}

export interface CompanySectorResponse {
  companySegment: string | null;
  businessDomain: string | null;
  availableSegments: SectorOptionDto[];
  availableDomains: SectorOptionDto[];
}

export interface CompanySectorRequest {
  companySegment: string;
  businessDomain: string;
}

/** `POST /api/company/sector/preview` — dry-run, no side effect. */
export interface SectorPreviewModuleDto {
  id: AppModule;
  labelFr: string;
}

export interface CompanySectorPreviewResponse {
  modulesToEnable: SectorPreviewModuleDto[];
  templates: string[];
  warnings: string[];
}

/** `PUT /api/company/sector` — applies the change (additive-only, rate-limited to 1/day). */
export interface CompanySectorApplyResponse {
  companySegment: string;
  businessDomain: string;
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
export class CompanySectorService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/company/sector`;

  getSector(): Observable<ApiResponse<CompanySectorResponse>> {
    return this.http.get<ApiResponse<CompanySectorResponse>>(this.API_URL);
  }

  previewSector(request: CompanySectorRequest): Observable<ApiResponse<CompanySectorPreviewResponse>> {
    return this.http.post<ApiResponse<CompanySectorPreviewResponse>>(`${this.API_URL}/preview`, request);
  }

  applySector(request: CompanySectorRequest): Observable<ApiResponse<CompanySectorApplyResponse>> {
    return this.http.put<ApiResponse<CompanySectorApplyResponse>>(this.API_URL, request);
  }
}
