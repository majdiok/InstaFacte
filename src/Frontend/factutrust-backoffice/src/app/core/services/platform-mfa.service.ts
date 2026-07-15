import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  MfaConfirmDto,
  MfaSetupDto,
  MfaStatusDto,
  PlatformAuthResponseDto
} from '@core/models/platform.models';

/**
 * Lot B2 — Service 2FA pour la page `/me/2fa` et la 2e étape du login.
 *
 * Endpoints consommés :
 *  - GET    /api/platform/auth/2fa/status
 *  - POST   /api/platform/auth/2fa/setup
 *  - POST   /api/platform/auth/2fa/confirm
 *  - POST   /api/platform/auth/2fa/disable
 *  - POST   /api/platform/auth/2fa/verify (étape 2 du login — appelé par PlatformAuthService)
 */
@Injectable({ providedIn: 'root' })
export class PlatformMfaService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/auth/2fa`;

  status(): Observable<ApiResponse<MfaStatusDto>> {
    return this.http.get<ApiResponse<MfaStatusDto>>(`${this.base}/status`);
  }

  startSetup(): Observable<ApiResponse<MfaSetupDto>> {
    return this.http.post<ApiResponse<MfaSetupDto>>(`${this.base}/setup`, {});
  }

  confirmSetup(code: string): Observable<ApiResponse<MfaConfirmDto>> {
    return this.http.post<ApiResponse<MfaConfirmDto>>(`${this.base}/confirm`, { code });
  }

  disable(password: string, code: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/disable`, { password, code });
  }

  /**
   * Étape 2 du login — finalise l'authentification avec le ticket court reçu à l'étape 1.
   * Appelé par PlatformAuthService après détection du `TwoFactorChallengeDto`.
   */
  verifyLogin(ticket: string, code: string): Observable<ApiResponse<PlatformAuthResponseDto>> {
    return this.http.post<ApiResponse<PlatformAuthResponseDto>>(`${this.base}/verify`, {
      ticket,
      code
    });
  }
}
