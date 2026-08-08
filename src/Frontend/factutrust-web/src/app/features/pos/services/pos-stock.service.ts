import { Injectable, inject, signal, effect } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import {
  StockService,
  ProductAvailabilityDetail,
  StockAvailabilityCheck,
  StockAlertsResult
} from '@core/services/stock.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { PosStateService } from './pos-state.service';

const CACHE_TTL_MS = 30000;

export type ProductStockAlert = { alertLevel: 'warning' | 'insufficient'; message: string };

@Injectable({
  providedIn: 'root'
})
export class PosStockService {
  private readonly stockService = inject(StockService);
  private readonly posState = inject(PosStateService);
  private readonly warehouseContext = inject(WarehouseContextService);

  private readonly alertsMap = signal<Map<string, ProductAvailabilityDetail>>(new Map());
  private readonly catalogAlertsMap = signal<Map<string, ProductStockAlert>>(new Map());
  private lastCheckTime = 0;

  constructor() {
    effect(() => {
      const id = this.warehouseContext.selectedWarehouseId();
      if (!id) {
        this.catalogAlertsMap.set(new Map());
        return;
      }
      this.loadCatalogAlertsForWarehouseId(id);
    });
  }

  getProductAlert(productId: string): ProductAvailabilityDetail | null {
    return this.alertsMap().get(productId) ?? null;
  }

  getCatalogAlert(productId: string): ProductStockAlert | null {
    return this.catalogAlertsMap().get(productId) ?? null;
  }

  /** @deprecated Prefer effect-driven reload; kept for explicit refresh if needed. */
  loadCatalogAlerts(): void {
    const id = this.warehouseContext.selectedWarehouseId();
    if (id) this.loadCatalogAlertsForWarehouseId(id);
  }

  private loadCatalogAlertsForWarehouseId(warehouseId: string): void {
    this.stockService
      .getStockAlerts(warehouseId)
      .pipe(
        map(res => (res as { data?: StockAlertsResult })?.data ?? null),
        catchError(() => of(null))
      )
      .subscribe(data => {
        const next = new Map<string, ProductStockAlert>();
        if (!data) {
          this.catalogAlertsMap.set(next);
          return;
        }
        (data.outOfStockItems ?? []).forEach(item => {
          next.set(item.productId, { alertLevel: 'insufficient', message: 'Rupture' });
        });
        (data.lowStockItems ?? []).forEach(item => {
          if (!next.has(item.productId)) {
            next.set(item.productId, { alertLevel: 'warning', message: 'Stock faible' });
          }
        });
        this.catalogAlertsMap.set(next);
      });
  }

  checkOrderStock(): Observable<StockAvailabilityCheck | null> {
    const lines = this.posState.lines();
    if (lines.length === 0) return of(null);

    const now = Date.now();
    if (now - this.lastCheckTime < CACHE_TTL_MS) {
      const details = Array.from(this.alertsMap().values());
      const allAvailable = details.every(d => d.isAvailable);
      return of({
        allAvailable,
        insufficientCount: details.filter(d => !d.isAvailable).length,
        details,
        summaryMessage: allAvailable ? 'Stock disponible' : 'Stock insuffisant'
      });
    }

    const items = lines.map(l => ({
      productId: l.productId,
      requestedQuantity: l.quantity
    }));

    const warehouseId = this.warehouseContext.selectedWarehouseId() ?? undefined;

    return this.stockService.checkAvailability(items, warehouseId).pipe(
      map(res => {
        const data = (res as { data?: StockAvailabilityCheck })?.data;
        if (!data || typeof data !== 'object') return null;
        const check = data as StockAvailabilityCheck;
        this.lastCheckTime = Date.now();
        const map = new Map<string, ProductAvailabilityDetail>();
        (check.details ?? []).forEach(d => map.set(d.productId, d));
        this.alertsMap.set(map);
        return check;
      }),
      catchError(() => of(null))
    );
  }

  refreshAlertsForProduct(productId: string): void {
    this.alertsMap.update(m => {
      const next = new Map(m);
      next.delete(productId);
      return next;
    });
  }

  clearAlerts(): void {
    this.alertsMap.set(new Map());
  }

  /** Après une vente réussie : évite de réutiliser le cache 30s de `checkOrderStock` avec d'anciennes quantités. */
  invalidateOrderStockCache(): void {
    this.lastCheckTime = 0;
    this.alertsMap.set(new Map());
  }
}
