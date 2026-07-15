import { Injectable, signal, computed } from '@angular/core';
import { PosStateService, PosState } from './pos-state.service';

const DB_NAME = 'pos-held-orders';
const STORE_NAME = 'orders';
const DB_VERSION = 1;

export interface HeldOrder {
  id: string;
  label: string;
  state: PosState;
  lineCount: number;
  totalTTC: number;
  heldAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class PosHeldOrdersService {
  private readonly heldOrders = signal<HeldOrder[]>([]);
  private db: IDBDatabase | null = null;

  readonly heldOrderCount = computed(() => this.heldOrders().length);
  readonly heldOrdersList = computed(() => this.heldOrders());

  constructor(private readonly posState: PosStateService) {
    this.initDb().then(() => this.loadHeldOrders());
  }

  private initDb(): Promise<void> {
    return new Promise((resolve, reject) => {
      if (typeof indexedDB === 'undefined') {
        resolve();
        return;
      }
      const req = indexedDB.open(DB_NAME, DB_VERSION);
      req.onerror = () => reject(req.error);
      req.onsuccess = () => {
        this.db = req.result;
        resolve();
      };
      req.onupgradeneeded = (e) => {
        const db = (e.target as IDBOpenDBRequest).result;
        if (!db.objectStoreNames.contains(STORE_NAME)) {
          db.createObjectStore(STORE_NAME, { keyPath: 'id' });
        }
      };
    });
  }

  private loadHeldOrders(): void {
    if (!this.db) return;
    const tx = this.db.transaction(STORE_NAME, 'readonly');
    const store = tx.objectStore(STORE_NAME);
    const req = store.getAll();
    req.onsuccess = () => {
      this.heldOrders.set((req.result as HeldOrder[]) || []);
    };
  }

  holdCurrentOrder(): Promise<string | null> {
    const state = this.posState.getSnapshot();
    if (state.lines.length === 0) return Promise.resolve(null);

    const clientName = state.client?.name ?? 'Client passager';
    const lineCount = state.lines.length;
    const totalTTC = this.posState.totals().totalTTC;
    const label = `${clientName} - ${lineCount} article${lineCount > 1 ? 's' : ''}`;

    const held: HeldOrder = {
      id: crypto.randomUUID(),
      label,
      state,
      lineCount,
      totalTTC,
      heldAt: new Date().toISOString()
    };

    return new Promise((resolve, reject) => {
      if (!this.db) {
        this.heldOrders.update(prev => [...prev, held]);
        resolve(held.id);
        return;
      }
      const tx = this.db.transaction(STORE_NAME, 'readwrite');
      const store = tx.objectStore(STORE_NAME);
      const req = store.add(held);
      req.onsuccess = () => {
        this.heldOrders.update(prev => [...prev, held]);
        this.posState.resetOrder();
        resolve(held.id);
      };
      req.onerror = () => reject(req.error);
    });
  }

  getHeldOrders(): HeldOrder[] {
    return this.heldOrders();
  }

  recallOrder(id: string): boolean {
    const held = this.heldOrders().find(o => o.id === id);
    if (!held) return false;
    this.posState.restoreSnapshot(held.state);
    this.deleteHeldOrder(id);
    return true;
  }

  deleteHeldOrder(id: string): Promise<void> {
    return new Promise((resolve, reject) => {
      this.heldOrders.update(prev => prev.filter(o => o.id !== id));
      if (!this.db) {
        resolve();
        return;
      }
      const tx = this.db.transaction(STORE_NAME, 'readwrite');
      const store = tx.objectStore(STORE_NAME);
      const req = store.delete(id);
      req.onsuccess = () => resolve();
      req.onerror = () => reject(req.error);
    });
  }
}
