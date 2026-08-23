import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  LoginResultDto,
  PlatformAuthResponseDto,
  PlatformUserDto
} from '@core/models/platform.models';
import { isTwoFactorChallenge } from '@core/models/platform.models';

const STORAGE_ACCESS = 'ft_platform_access_token';
const STORAGE_REFRESH = 'ft_platform_refresh_token';
const STORAGE_EXPIRES = 'ft_platform_expires_at';
const STORAGE_USER = 'ft_platform_user';

@Injectable({ providedIn: 'root' })
export class PlatformAuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly userSignal = signal<PlatformUserDto | null>(this.readStoredUser());

  readonly user = computed(() => this.userSignal());

  isAuthenticated(): boolean {
    const token = this.getAccessToken();
    if (!token) return false;
    const exp = localStorage.getItem(STORAGE_EXPIRES);
    if (!exp) return false;
    return new Date(exp).getTime() > Date.now() + 5000;
  }

  getAccessToken(): string | null {
    return localStorage.getItem(STORAGE_ACCESS);
  }

  /**
   * Lot B2 — Login étape 1.
   * La réponse peut être :
   *  - `PlatformAuthResponseDto` (login complet, persisté immédiatement)
   *  - `TwoFactorChallengeDto` (le composant doit ensuite appeler `verifyTwoFactor()` avec le code)
   */
  login(email: string, password: string): Observable<ApiResponse<LoginResultDto>> {
    return this.http
      .post<ApiResponse<LoginResultDto>>(`${environment.apiUrl}/platform/auth/login`, {
        email,
        password
      })
      .pipe(
        tap(res => {
          if (res.success && res.data && !isTwoFactorChallenge(res.data)) {
            this.persistSession(res.data);
          }
        }),
        catchError(err => throwError(() => err))
      );
  }

  /**
   * Lot B2 — Login étape 2 : finalise l'authentification avec le ticket court + le code TOTP/recovery.
   */
  verifyTwoFactor(ticket: string, code: string): Observable<ApiResponse<PlatformAuthResponseDto>> {
    return this.http
      .post<ApiResponse<PlatformAuthResponseDto>>(`${environment.apiUrl}/platform/auth/2fa/verify`, {
        ticket,
        code
      })
      .pipe(
        tap(res => {
          if (res.success && res.data) {
            this.persistSession(res.data);
          }
        })
      );
  }

  logout(): void {
    const token = this.getAccessToken();
    if (token) {
      this.http.post<ApiResponse<unknown>>(`${environment.apiUrl}/platform/auth/logout`, {}).subscribe({
        next: () => this.clearSession(),
        error: () => this.clearSession()
      });
    } else {
      this.clearSession();
    }
  }

  logoutAndNavigate(): void {
    const token = this.getAccessToken();
    if (token) {
      this.http.post<ApiResponse<unknown>>(`${environment.apiUrl}/platform/auth/logout`, {}).subscribe({
        next: () => {
          this.clearSession();
          void this.router.navigate(['/login']);
        },
        error: () => {
          this.clearSession();
          void this.router.navigate(['/login']);
        }
      });
    } else {
      this.clearSessionAndNavigate();
    }
  }

  tryRefresh(): Observable<ApiResponse<PlatformAuthResponseDto>> {
    const refresh = localStorage.getItem(STORAGE_REFRESH);
    if (!refresh) {
      return throwError(() => new Error('no refresh'));
    }
    return this.http
      .post<ApiResponse<PlatformAuthResponseDto>>(`${environment.apiUrl}/platform/auth/refresh`, {
        refreshToken: refresh
      })
      .pipe(
        tap(res => {
          if (res.success && res.data) {
            this.persistSession(res.data);
          }
        })
      );
  }

  clearSessionAndNavigate(): void {
    this.clearSession();
    void this.router.navigate(['/login']);
  }

  /** Met à jour le profil stocké localement après édition (prénom/nom). */
  updateStoredUser(partial: Pick<PlatformUserDto, 'firstName' | 'lastName'>): void {
    const current = this.userSignal();
    if (!current) return;
    const updated: PlatformUserDto = {
      ...current,
      firstName: partial.firstName,
      lastName: partial.lastName,
      fullName: `${partial.firstName} ${partial.lastName}`
    };
    localStorage.setItem(STORAGE_USER, JSON.stringify(updated));
    this.userSignal.set(updated);
  }

  private persistSession(data: PlatformAuthResponseDto): void {
    localStorage.setItem(STORAGE_ACCESS, data.accessToken);
    localStorage.setItem(STORAGE_REFRESH, data.refreshToken);
    localStorage.setItem(STORAGE_EXPIRES, data.expiresAt);
    localStorage.setItem(STORAGE_USER, JSON.stringify(data.user));
    this.userSignal.set(data.user);
  }

  private clearSession(): void {
    localStorage.removeItem(STORAGE_ACCESS);
    localStorage.removeItem(STORAGE_REFRESH);
    localStorage.removeItem(STORAGE_EXPIRES);
    localStorage.removeItem(STORAGE_USER);
    this.userSignal.set(null);
  }

  private readStoredUser(): PlatformUserDto | null {
    const raw = localStorage.getItem(STORAGE_USER);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as PlatformUserDto;
    } catch {
      return null;
    }
  }
}
