import { Injectable, signal, computed } from '@angular/core';
import { ProductListItem } from '@core/services/product.service';

const STORAGE_KEY = 'pos-favorites';
const MAX_RECENTS = 10;

@Injectable({
  providedIn: 'root'
})
export class PosFavoritesService {
  private readonly favorites = signal<string[]>(this.loadFromStorage());
  private readonly recents = signal<ProductListItem[]>([]);

  readonly favoriteIds = computed(() => this.favorites());
  readonly recentProducts = computed(() => this.recents());

  isFavorite(productId: string): boolean {
    return this.favorites().includes(productId);
  }

  toggleFavorite(productId: string): void {
    const current = this.favorites();
    const next = current.includes(productId)
      ? current.filter(id => id !== productId)
      : [...current, productId];
    this.favorites.set(next);
    this.saveToStorage(next);
  }

  addRecent(product: ProductListItem): void {
    this.recents.update(prev => {
      const filtered = prev.filter(p => p.id !== product.id);
      return [product, ...filtered].slice(0, MAX_RECENTS);
    });
  }

  /** Met à jour les quantités affichées pour les produits récents après synchro stock (ex. vente POS). */
  patchQuantitiesInRecents(updates: Map<string, number>): void {
    if (updates.size === 0) return;
    this.recents.update(prev =>
      prev.map(p => {
        const q = updates.get(p.id);
        if (q === undefined || !p.isStockManaged) return p;
        return { ...p, quantityAvailable: q };
      })
    );
  }

  getFavoriteIds(): string[] {
    return this.favorites();
  }

  private loadFromStorage(): string[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as unknown;
        return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string') : [];
      }
    } catch {
      // ignore
    }
    return [];
  }

  private saveToStorage(ids: string[]): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(ids));
    } catch {
      // ignore
    }
  }
}
