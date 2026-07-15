import { Injectable, signal, computed } from '@angular/core';
import { ProductListItem } from '@core/services/product.service';

const MAX_SUGGESTIONS = 3;

/**
 * Service de suggestions de ventes additionnelles (upsell).
 * Propose des produits de la même catégorie ou complémentaires
 * lorsqu'un produit est ajouté à la commande.
 */
@Injectable({
  providedIn: 'root'
})
export class PosUpsellService {
  private readonly _suggestions = signal<ProductListItem[]>([]);

  readonly suggestions = this._suggestions.asReadonly();

  readonly hasSuggestions = computed(() => this._suggestions().length > 0);

  /**
   * Calcule les suggestions à partir du pool de produits disponible.
   * Priorité : même catégorie, puis autres catégories.
   * Exclut les produits déjà en commande et le produit venant d'être ajouté.
   */
  computeSuggestions(
    addedProduct: ProductListItem,
    orderProductIds: string[],
    productPool: ProductListItem[] | Map<string, ProductListItem>
  ): void {
    const pool = productPool instanceof Map
      ? Array.from(productPool.values())
      : productPool;

    const excludeIds = new Set([addedProduct.id, ...orderProductIds]);
    const sameCategory = pool.filter(
      p => !excludeIds.has(p.id) && p.categoryId && p.categoryId === addedProduct.categoryId
    );
    const otherCategories = pool.filter(
      p => !excludeIds.has(p.id) && (!p.categoryId || p.categoryId !== addedProduct.categoryId)
    );

    const result: ProductListItem[] = [];
    for (const p of sameCategory) {
      if (result.length >= MAX_SUGGESTIONS) break;
      result.push(p);
    }
    for (const p of otherCategories) {
      if (result.length >= MAX_SUGGESTIONS) break;
      result.push(p);
    }

    this._suggestions.set(result);
  }

  clearSuggestions(): void {
    this._suggestions.set([]);
  }
}
