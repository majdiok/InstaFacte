import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { environment } from '@environments/environment';
import { AuthService, ApiResponse } from '@core/services/auth.service';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import {
  PatchProductOnboardingRequest,
  ProductOnboardingChecklist,
  ProductOnboardingState,
  ProductOnboardingStatus,
  normalizeOnboardingStatus
} from './product-onboarding.models';

@Injectable({ providedIn: 'root' })
export class ProductOnboardingApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = `${environment.apiUrl}/me/onboarding`;

  readonly replayTick = signal(0);
  readonly isTourRunning = signal(false);

  get(): Observable<ProductOnboardingState | null> {
    return this.http
      .get<ApiResponse<ProductOnboardingState>>(this.base, {
        context: createHttpContextSkipGlobalErrorUi()
      })
      .pipe(
        map(res => (res.success && res.data ? normalizeState(res.data) : null)),
        catchError(() => of(null))
      );
  }

  patch(body: PatchProductOnboardingRequest): Observable<ProductOnboardingState | null> {
    return this.http
      .patch<ApiResponse<ProductOnboardingState>>(this.base, body, {
        context: createHttpContextSkipGlobalErrorUi()
      })
      .pipe(
        tap(res => {
          if (res.success && res.data) {
            this.auth.patchLocalOnboarding({
              productOnboardingStatus: normalizeOnboardingStatus(res.data.status),
              productOnboardingVersion: res.data.version,
              productOnboardingChecklist: res.data.checklist
            });
          }
        }),
        map(res => (res.success && res.data ? normalizeState(res.data) : null)),
        catchError(() => of(null))
      );
  }

  requestReplay(): void {
    this.replayTick.update(n => n + 1);
  }
}

export function normalizeState(raw: ProductOnboardingState): ProductOnboardingState {
  const checklist: ProductOnboardingChecklist = {
    dismissed: !!raw.checklist?.dismissed,
    doneIds: Array.isArray(raw.checklist?.doneIds) ? raw.checklist.doneIds : []
  };
  return {
    enabled: raw.enabled !== false,
    status: normalizeOnboardingStatus(raw.status),
    version: raw.version ?? 1,
    checklist,
    autoCompletedIds: Array.isArray(raw.autoCompletedIds) ? raw.autoCompletedIds : []
  };
}

export function isProductOnboardingUiEnabled(): boolean {
  return environment.productOnboardingEnabled !== false;
}

export type { ProductOnboardingStatus };
