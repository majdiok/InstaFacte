import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  PlatformFiscalSettingsDto,
  UpdatePlatformFiscalSettingsRequest
} from '@core/models/platform.models';

/** Lot C4 — Lecture / mise à jour des paramètres fiscaux singleton plateforme. */
@Injectable({ providedIn: 'root' })
export class PlatformFiscalSettingsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/fiscal-settings`;

  get(): Observable<ApiResponse<PlatformFiscalSettingsDto>> {
    return this.http.get<ApiResponse<PlatformFiscalSettingsDto>>(this.base);
  }

  update(request: UpdatePlatformFiscalSettingsRequest): Observable<ApiResponse<PlatformFiscalSettingsDto>> {
    return this.http.put<ApiResponse<PlatformFiscalSettingsDto>>(this.base, request);
  }
}
