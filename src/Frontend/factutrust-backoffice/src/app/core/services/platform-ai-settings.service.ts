import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  PlatformAiSettingsDto,
  UpdatePlatformAiSettingsRequest
} from '@core/models/platform.models';

/** Configuration IA plateforme : modèle LLM global partagé par toutes les entreprises. */
@Injectable({ providedIn: 'root' })
export class PlatformAiSettingsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/ai-settings`;

  get(): Observable<ApiResponse<PlatformAiSettingsDto>> {
    return this.http.get<ApiResponse<PlatformAiSettingsDto>>(this.base);
  }

  update(request: UpdatePlatformAiSettingsRequest): Observable<ApiResponse<PlatformAiSettingsDto>> {
    return this.http.put<ApiResponse<PlatformAiSettingsDto>>(this.base, request);
  }
}
