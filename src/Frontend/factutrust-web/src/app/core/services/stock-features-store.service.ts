import { Injectable, inject, signal } from '@angular/core';
import { PERMISSIONS } from '@core/config/permission-keys';
import { AuthService } from '@core/services/auth.service';
import { StockFeatures, StockService } from './stock.service';

/**
 * Cached stock feature flags from GET /stock/features (tenant-scoped).
 * Used by navigation and other shell-level visibility rules.
 */
@Injectable({ providedIn: 'root' })
export class StockFeaturesStore {
  private readonly stockService = inject(StockService);
  private readonly auth = inject(AuthService);

  private readonly _features = signal<StockFeatures | null>(null);
  private loadStarted = false;

  readonly features = this._features.asReadonly();

  ensureLoaded(): void {
    if (!this.auth.hasAnyPermission([PERMISSIONS.stock.read, PERMISSIONS.products.read])) {
      return;
    }
    if (this.loadStarted) return;
    this.loadStarted = true;

    this.stockService.getFeatures().subscribe({
      next: res => {
        if (res.success && res.data) {
          this._features.set(res.data);
        }
      },
      error: () => {
        // Fail closed: keep features null (variant axes hidden) and do not retry.
      }
    });
  }

  productVariantsEnabled(): boolean {
    return this._features()?.productVariantsEnabled ?? false;
  }
}
