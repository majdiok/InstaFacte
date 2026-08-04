import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, of } from 'rxjs';
import { finalize, map } from 'rxjs/operators';
import { PricingService, ResolvedPrice } from '@core/services/pricing.service';
import { formatLocalDate } from '@core/utils/date.util';

export interface ResolveLinePriceParams {
  productId: string;
  clientId: string | null;
  quantity: number;
  documentDate?: Date | null;
  priceOverridden: boolean;
  /** Vérification dynamique (race) : si true, la réponse est ignorée. */
  isOverrideCheck?: () => boolean;
}

/** État promotionnel prévisualisé pour une ligne document. */
export interface LinePromotionPreview {
  promotionDiscountPercent: number | null;
  promotionName: string | null;
  promotionId: string | null;
  promotionEligible: boolean;
  promotionMinQuantityRequired: number | null;
}

export const EMPTY_LINE_PROMOTION: LinePromotionPreview = {
  promotionDiscountPercent: null,
  promotionName: null,
  promotionId: null,
  promotionEligible: false,
  promotionMinQuantityRequired: null
};

export function mapResolvedPricePromotion(resolved: ResolvedPrice): LinePromotionPreview {
  return {
    promotionDiscountPercent: resolved.promotionDiscountPercent ?? null,
    promotionName: resolved.promotionName ?? null,
    promotionId: resolved.promotionId ?? null,
    promotionEligible: resolved.promotionEligible ?? false,
    promotionMinQuantityRequired: resolved.promotionMinQuantityRequired ?? null
  };
}

/** Remise effective affichée : manuelle prioritaire, sinon promotion auto. */
export function effectiveLineDiscountPercent(
  manualDiscountPercent: number | null | undefined,
  promo: LinePromotionPreview
): number {
  if (manualDiscountPercent != null && manualDiscountPercent > 0) {
    return manualDiscountPercent;
  }
  if (promo.promotionEligible && promo.promotionDiscountPercent != null) {
    return promo.promotionDiscountPercent;
  }
  return 0;
}

export function lineTotalWithPromotion(
  quantity: number,
  unitPrice: number,
  manualDiscountPercent: number | null | undefined,
  promo: LinePromotionPreview
): number {
  const gross = quantity * unitPrice;
  const discount = effectiveLineDiscountPercent(manualDiscountPercent, promo);
  return gross - (gross * discount) / 100;
}

/**
 * Service partagé pour résoudre le prix HT et la promotion d'une ligne document.
 */
@Injectable({ providedIn: 'root' })
export class DocumentLinePricingService {
  private readonly pricingService = inject(PricingService);
  private readonly resolvingCount = signal(0);

  readonly resolving = computed(() => this.resolvingCount() > 0);

  formatDocumentDate(date: Date | null | undefined): string | undefined {
    return date ? formatLocalDate(date) : undefined;
  }

  resolveLinePrice(params: ResolveLinePriceParams): Observable<ResolvedPrice | null> {
    if (params.priceOverridden) {
      return of(null);
    }

    const dateStr = this.formatDocumentDate(params.documentDate ?? undefined);
    this.resolvingCount.update(c => c + 1);

    return this.pricingService
      .resolve(params.productId, params.clientId, params.quantity, dateStr)
      .pipe(
        map(res => {
          if (params.priceOverridden || params.isOverrideCheck?.()) {
            return null;
          }
          return res.data ?? null;
        }),
        finalize(() => this.resolvingCount.update(c => Math.max(0, c - 1)))
      );
  }
}
