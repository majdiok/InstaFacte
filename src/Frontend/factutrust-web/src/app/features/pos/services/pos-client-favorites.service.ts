import { Injectable, inject, signal, computed } from '@angular/core';
import { ProductListItem } from '@core/services/product.service';
import { PosStateService } from './pos-state.service';

const STORAGE_KEY = 'factutrust_pos_client_favorites';
const MAX_PRODUCTS_PER_CLIENT = 20;

interface ClientFavoritesRecord {
  [clientId: string]: string[];
}

@Injectable({
  providedIn: 'root'
})
export class PosClientFavoritesService {
  private readonly posState = inject(PosStateService);

  private getStorage(): ClientFavoritesRecord {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as ClientFavoritesRecord) : {};
    } catch {
      return {};
    }
  }

  private setStorage(data: ClientFavoritesRecord): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
    } catch {
      // ignore
    }
  }

  recordOrder(clientId: string, productIds: string[]): void {
    if (!clientId || productIds.length === 0) return;
    const store = this.getStorage();
    let list = store[clientId] ?? [];
    productIds.forEach(id => {
      list = list.filter(x => x !== id);
      list.unshift(id);
    });
    list = list.slice(0, MAX_PRODUCTS_PER_CLIENT);
    store[clientId] = list;
    this.setStorage(store);
  }

  getFrequentProductIds(clientId: string | null): string[] {
    if (!clientId) return [];
    return this.getStorage()[clientId] ?? [];
  }

  getFrequentProducts(
    clientId: string | null,
    productCache: Map<string, ProductListItem>
  ): ProductListItem[] {
    const ids = this.getFrequentProductIds(clientId);
    return ids
      .map(id => productCache.get(id))
      .filter((p): p is ProductListItem => p != null);
  }
}
