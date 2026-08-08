import { DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';
import { toObservable, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, UrlTree } from '@angular/router';
import { Observable, of } from 'rxjs';
import { map, catchError, switchMap } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { StockService, Warehouse } from './stock.service';
import {
  clearWarehouseStorage,
  readWarehouseIdForUser,
  writeWarehouseIdForUser
} from './warehouse-storage';

@Injectable({
  providedIn: 'root'
})
export class WarehouseContextService {
  private authService = inject(AuthService);
  private stockService = inject(StockService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  private selectedId = signal<string | null>(this.readStoredIdIfUserMatches());

  /** Current warehouse id for the logged-in tenant; null if none / not yet chosen (multi). */
  readonly selectedWarehouseId = this.selectedId.asReadonly();

  readonly hasWarehouseSelected = computed(() => !!this.selectedId());

  private resolvedWarehouseSignal = signal<Warehouse | null>(null);

  /**
   * Full `Warehouse` for the current `selectedWarehouseId`, loaded from the API.
   * Single source of truth for display (main header, POS header, etc.).
   */
  readonly resolvedWarehouse = this.resolvedWarehouseSignal.asReadonly();

  constructor() {
    effect(() => {
      if (!this.authService.user()) {
        this.selectedId.set(null);
      }
    });

    effect(() => {
      if (this.authService.isAccountingFirm()) {
        this.selectedId.set(null);
        this.resolvedWarehouseSignal.set(null);
        clearWarehouseStorage();
      }
    });

    toObservable(this.selectedWarehouseId)
      .pipe(
        switchMap((id) => {
          if (this.authService.isAccountingFirm()) {
            return of<Warehouse | null>(null);
          }
          if (!id) {
            return of<Warehouse | null>(null);
          }
          return this.stockService.getWarehouses(true).pipe(
            map((res) => {
              const list: Warehouse[] = res.success && res.data ? res.data : [];
              return list.find((w) => w.id === id) ?? null;
            }),
            catchError(() => of<Warehouse | null>(null))
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((w) => this.resolvedWarehouseSignal.set(w));
  }

  /** Clears in-memory state and localStorage (logout). */
  clear(): void {
    this.selectedId.set(null);
    clearWarehouseStorage();
  }

  /** Persists selection for the current user and tenant. */
  setSelectedWarehouse(warehouseId: string): void {
    const user = this.authService.user();
    const tenantId = user?.tenantId;
    const userId = user?.id;
    if (!tenantId || !userId) return;
    writeWarehouseIdForUser(userId, tenantId, warehouseId);
    this.selectedId.set(warehouseId);
  }

  /**
   * After login/register: loads warehouses and navigates to returnUrl or select-warehouse.
   */
  navigateAfterSuccessfulAuth(returnUrl: string): void {
    const user = this.authService.user();
    if (this.authService.isAccountingFirm()) {
      const safeUrl =
        returnUrl?.startsWith('/firm') ||
        (returnUrl?.startsWith('/accounting') && user?.accessMode === 'delegated')
          ? returnUrl
          : '/firm/dashboard';
      this.router.navigateByUrl(safeUrl);
      return;
    }
    const safeUrl = returnUrl?.startsWith('/') ? returnUrl : '/dashboard';
    this.stockService.getWarehouses(true).subscribe({
      next: (res) => {
        const list: Warehouse[] = res.success && res.data ? res.data : [];
        this.applyResolution(list, safeUrl);
      },
      error: () => {
        this.router.navigateByUrl(safeUrl);
      }
    });
  }

  /**
   * Resolves selection for route guard: returns true, or UrlTree to select-warehouse.
   */
  resolveForAuthenticatedRoute(returnUrl: string): Observable<boolean | UrlTree> {
    if (this.authService.isAccountingFirm()) {
      return of(true);
    }
    return this.stockService.getWarehouses(true).pipe(
      map((res) => {
        const list: Warehouse[] = res.success && res.data ? res.data : [];
        const tree = this.resolveListForGuard(list, returnUrl);
        return tree === true ? true : tree;
      }),
      catchError(() => of(true))
    );
  }

  private applyResolution(warehouses: Warehouse[], returnUrl: string): void {
    const user = this.authService.user();
    const tenantId = user?.tenantId;
    if (!tenantId) {
      this.router.navigateByUrl(returnUrl);
      return;
    }

    if (warehouses.length === 0) {
      this.clearIfTenantMismatch();
      this.router.navigateByUrl(returnUrl);
      return;
    }

    if (warehouses.length === 1) {
      this.setSelectedWarehouse(warehouses[0].id);
      this.router.navigateByUrl(returnUrl);
      return;
    }

    const current = this.selectedId();
    const storedValid =
      current && warehouses.some((w) => w.id === current) ? current : null;
    const persisted = this.readStoredIdIfUserMatches();
    const persistedValid =
      persisted && warehouses.some((w) => w.id === persisted) ? persisted : null;

    const chosen = storedValid || persistedValid;
    if (chosen) {
      this.setSelectedWarehouse(chosen);
      this.router.navigateByUrl(returnUrl);
      return;
    }

    this.router.navigate(['/auth/select-warehouse'], {
      queryParams: { returnUrl }
    });
  }

  private resolveListForGuard(warehouses: Warehouse[], returnUrl: string): true | UrlTree {
    const user = this.authService.user();
    const tenantId = user?.tenantId;
    if (!tenantId) return true;

    if (warehouses.length === 0) {
      this.clearIfTenantMismatch();
      return true;
    }

    if (warehouses.length === 1) {
      this.setSelectedWarehouse(warehouses[0].id);
      return true;
    }

    const current = this.selectedId();
    const storedValid =
      current && warehouses.some((w) => w.id === current) ? current : null;
    const persisted = this.readStoredIdIfUserMatches();
    const persistedValid =
      persisted && warehouses.some((w) => w.id === persisted) ? persisted : null;

    const chosen = storedValid || persistedValid;
    if (chosen) {
      this.setSelectedWarehouse(chosen);
      return true;
    }

    return this.router.createUrlTree(['/auth/select-warehouse'], {
      queryParams: { returnUrl }
    });
  }

  private readStoredIdIfUserMatches(): string | null {
    if (this.authService.isAccountingFirm()) {
      return null;
    }
    const user = this.authService.user();
    const tenantId = user?.tenantId;
    const userId = user?.id;
    if (!tenantId || !userId) return null;
    return readWarehouseIdForUser(userId, tenantId);
  }

  private clearIfTenantMismatch(): void {
    this.selectedId.set(null);
    clearWarehouseStorage();
  }

  /** Re-sync signal from storage when user session is restored (e.g. F5). */
  hydrateFromStorage(): void {
    if (this.authService.isAccountingFirm()) {
      this.selectedId.set(null);
      return;
    }
    const id = this.readStoredIdIfUserMatches();
    this.selectedId.set(id);
  }
}
