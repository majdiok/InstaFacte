import { Injectable, inject, signal } from '@angular/core';
import { StockFeatures, StockService } from './stock.service';

/**
 * Cached stock feature flags from GET /stock/features (tenant-scoped).
 * Used by navigation and other shell-level visibility rules.
 */
@Injectable({ providedIn: 'root' })
export class StockFeaturesStore {
  private readonly stockService = inject(StockService);

  private readonly _features = signal<StockFeatures | null>(null);
  private loadStarted = false;

  readonly features = this._features.asReadonly();

  ensureLoaded(): void {
    if (this.loadStarted) return;
    this.loadStarted = true;
    this.stockService.getFeatures().subscribe({
      next: res => {
        if (res.success && res.data) {
          this._features.set(res.data);
        }
      },
      error: () => {
        this.loadStarted = false;
      }
    });
  }

  productVariantsEnabled(): boolean {
    return this._features()?.productVariantsEnabled ?? false;
  }
}
