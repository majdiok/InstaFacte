import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { ApiResponse, AuthService } from '@core/services/auth.service';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import {
  DashboardBlockId,
  DEFAULT_DASHBOARD_BLOCK_ORDER,
  reconcileOrder
} from '../dashboard-layout.config';
import {
  readDashboardLayoutForUser,
  writeDashboardLayoutForUser
} from './dashboard-layout-storage';

export interface DashboardLayoutDto {
  blockOrder: string[];
}

/**
 * Gère l'ordre des blocs du tableau de bord.
 *
 * Source de vérité : backend (multi-appareils). Doublé d'un cache localStorage
 * (user+tenant scoped) pour un affichage instantané et un repli gracieux :
 * si l'API est indisponible, le tableau de bord reste pleinement fonctionnel
 * sur l'ordre en cache ou par défaut, sans jamais afficher d'erreur.
 */
@Injectable({ providedIn: 'root' })
export class DashboardLayoutService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly baseUrl = `${environment.apiUrl}/settings/dashboard-layout`;
  private readonly skipErrorUi = createHttpContextSkipGlobalErrorUi();

  /** Ordre actuellement appliqué (réconcilié). */
  private readonly orderSignal = signal<DashboardBlockId[]>([...DEFAULT_DASHBOARD_BLOCK_ORDER]);
  readonly order = this.orderSignal.asReadonly();

  /**
   * Charge l'ordre : applique d'abord le cache local (immédiat), puis tente le
   * backend et réconcilie. Tout échec est silencieux (repli sur cache/défaut).
   */
  loadLayout(): void {
    const user = this.auth.user();
    if (user) {
      const cached = readDashboardLayoutForUser(user.id, user.tenantId);
      if (cached) {
        this.orderSignal.set(cached);
      }
    }

    this.http
      .get<ApiResponse<DashboardLayoutDto>>(this.baseUrl, { context: this.skipErrorUi })
      .pipe(catchError(() => of(null)))
      .subscribe((res) => {
        if (res?.success && res.data?.blockOrder) {
          const reconciled = reconcileOrder(res.data.blockOrder);
          this.orderSignal.set(reconciled);
          this.cache(reconciled);
        }
      });
  }

  /** Met à jour l'ordre en mémoire uniquement (glissement en cours / annulation). */
  setOrderInMemory(order: readonly DashboardBlockId[]): void {
    this.orderSignal.set([...order]);
  }

  /** Persiste l'ordre : maj optimiste (signal + cache) puis PUT silencieux. */
  saveLayout(order: readonly DashboardBlockId[]): Observable<unknown> {
    const reconciled = reconcileOrder(order);
    this.orderSignal.set(reconciled);
    this.cache(reconciled);
    return this.http
      .put<ApiResponse<DashboardLayoutDto>>(
        this.baseUrl,
        { blockOrder: reconciled } as DashboardLayoutDto,
        { context: this.skipErrorUi }
      )
      .pipe(
        tap((res) => {
          if (res?.success && res.data?.blockOrder) {
            const fromServer = reconcileOrder(res.data.blockOrder);
            this.orderSignal.set(fromServer);
            this.cache(fromServer);
          }
        }),
        catchError(() => of(null))
      );
  }

  /** Réinitialise à l'ordre par défaut et persiste. */
  resetLayout(): Observable<unknown> {
    return this.saveLayout([...DEFAULT_DASHBOARD_BLOCK_ORDER]);
  }

  private cache(order: readonly DashboardBlockId[]): void {
    const user = this.auth.user();
    if (user) {
      writeDashboardLayoutForUser(user.id, user.tenantId, order);
    }
  }
}
