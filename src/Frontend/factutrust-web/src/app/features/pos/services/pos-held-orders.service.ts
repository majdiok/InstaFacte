import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { of } from 'rxjs';
import { environment } from '@environments/environment';
import { createClientUuid } from '@core/utils/safe-random-uuid.util';
import { PosStateService, PosState } from './pos-state.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { ApiResponse } from '@core/services/auth.service';

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

interface HeldTicketDto {
  id: string;
  label: string;
  totalTtc: number;
  lineCount: number;
  heldAt: string;
  state?: PosState | null;
}

@Injectable({
  providedIn: 'root'
})
export class PosHeldOrdersService {
  private readonly http = inject(HttpClient);
  private readonly warehouseContext = inject(WarehouseContextService);
  private readonly apiUrl = `${environment.apiUrl}/pos`;
  private readonly heldOrders = signal<HeldOrder[]>([]);
  private db: IDBDatabase | null = null;
  private imported = false;

  readonly heldOrderCount = computed(() => this.heldOrders().length);
  readonly heldOrdersList = computed(() => this.heldOrders());

  constructor(private readonly posState: PosStateService) {
    this.initDb().catch(() => undefined);
  }

  async initialize(): Promise<void> {
    await this.initDb();
    const warehouseId = this.warehouseContext.selectedWarehouseId();
    if (!warehouseId) {
      return;
    }

    const remote = await this.fetchRemote(warehouseId);
    if (remote.length > 0) {
      this.heldOrders.set(remote);
      await this.clearIndexedDb();
      this.imported = true;
      return;
    }

    const local = await this.readIndexedDb();
    if (local.length > 0 && !this.imported) {
      await this.importLocal(warehouseId, local);
      this.imported = true;
      await this.clearIndexedDb();
      this.heldOrders.set(await this.fetchRemote(warehouseId));
      return;
    }

    this.heldOrders.set([]);
  }

  private warehouseParams(): HttpParams {
    const warehouseId = this.warehouseContext.selectedWarehouseId();
    return warehouseId ? new HttpParams().set('warehouseId', warehouseId) : new HttpParams();
  }

  private async fetchRemote(warehouseId: string): Promise<HeldOrder[]> {
    try {
      const params = new HttpParams().set('warehouseId', warehouseId);
      const res = await firstValueFrom(
        this.http.get<ApiResponse<HeldTicketDto[]>>(`${this.apiUrl}/held-tickets`, { params })
      );
      if (!res.success || !res.data) {
        return [];
      }
      return res.data.map(this.toHeldOrder);
    } catch {
      return [];
    }
  }

  private toHeldOrder(dto: HeldTicketDto): HeldOrder {
    return {
      id: dto.id,
      label: dto.label,
      state: dto.state as PosState,
      lineCount: dto.lineCount,
      totalTTC: dto.totalTtc,
      heldAt: dto.heldAt
    };
  }

  async holdCurrentOrder(): Promise<string | null> {
    const state = this.posState.getSnapshot();
    if (state.lines.length === 0) {
      return null;
    }

    const clientName = state.client?.name ?? 'Client passager';
    const lineCount = state.lines.length;
    const totalTTC = this.posState.totals().totalTTC;
    const label = `${clientName} - ${lineCount} article${lineCount > 1 ? 's' : ''}`;
    const warehouseId = this.warehouseContext.selectedWarehouseId();
    const id = createClientUuid();

    if (!warehouseId) {
      const held: HeldOrder = {
        id,
        label,
        state,
        lineCount,
        totalTTC,
        heldAt: new Date().toISOString()
      };
      this.heldOrders.update(prev => [...prev, held]);
      this.posState.resetOrder();
      return id;
    }

    try {
      const res = await firstValueFrom(
        this.http.post<ApiResponse<HeldTicketDto>>(`${this.apiUrl}/held-tickets`, {
          warehouseId,
          id,
          label,
          totalTtc: totalTTC,
          lineCount,
          state
        })
      );
      if (res.success && res.data) {
        this.heldOrders.update(prev => [...prev, this.toHeldOrder(res.data!)]);
        this.posState.resetOrder();
        return res.data.id;
      }
    } catch {
      this.heldOrders.update(prev => [
        ...prev,
        { id, label, state, lineCount, totalTTC, heldAt: new Date().toISOString() }
      ]);
      this.posState.resetOrder();
      return id;
    }

    return null;
  }

  getHeldOrders(): HeldOrder[] {
    return this.heldOrders();
  }

  async recallOrder(id: string): Promise<boolean> {
    const warehouseId = this.warehouseContext.selectedWarehouseId();
    if (warehouseId) {
      try {
        const res = await firstValueFrom(
          this.http.post<ApiResponse<HeldTicketDto>>(`${this.apiUrl}/held-tickets/${id}/recall`, {})
        );
        if (res.success && res.data?.state) {
          this.posState.restoreSnapshot(res.data.state as PosState);
          this.heldOrders.update(prev => prev.filter(o => o.id !== id));
          return true;
        }
      } catch {
        // fall through to local list
      }
    }

    const held = this.heldOrders().find(o => o.id === id);
    if (!held?.state) {
      return false;
    }
    this.posState.restoreSnapshot(held.state);
    await this.deleteHeldOrder(id);
    return true;
  }

  async deleteHeldOrder(id: string): Promise<void> {
    this.heldOrders.update(prev => prev.filter(o => o.id !== id));
    const warehouseId = this.warehouseContext.selectedWarehouseId();
    if (warehouseId) {
      await firstValueFrom(
        this.http.delete(`${this.apiUrl}/held-tickets/${id}`).pipe(catchError(() => of(null)))
      );
    }
  }

  private async importLocal(warehouseId: string, tickets: HeldOrder[]): Promise<void> {
    await firstValueFrom(
      this.http.post(`${this.apiUrl}/held-tickets/import`, {
        warehouseId,
        tickets: tickets.map(t => ({
          warehouseId,
          id: t.id,
          label: t.label,
          totalTtc: t.totalTTC,
          lineCount: t.lineCount,
          state: t.state
        }))
      }).pipe(catchError(() => of(null)))
    );
  }

  private initDb(): Promise<void> {
    return new Promise(resolve => {
      if (typeof indexedDB === 'undefined') {
        resolve();
        return;
      }
      const req = indexedDB.open(DB_NAME, DB_VERSION);
      req.onerror = () => resolve();
      req.onsuccess = () => {
        this.db = req.result;
        resolve();
      };
      req.onupgradeneeded = e => {
        const db = (e.target as IDBOpenDBRequest).result;
        if (!db.objectStoreNames.contains(STORE_NAME)) {
          db.createObjectStore(STORE_NAME, { keyPath: 'id' });
        }
      };
    });
  }

  private readIndexedDb(): Promise<HeldOrder[]> {
    return new Promise(resolve => {
      if (!this.db) {
        resolve([]);
        return;
      }
      const tx = this.db.transaction(STORE_NAME, 'readonly');
      const store = tx.objectStore(STORE_NAME);
      const req = store.getAll();
      req.onsuccess = () => resolve((req.result as HeldOrder[]) || []);
      req.onerror = () => resolve([]);
    });
  }

  private clearIndexedDb(): Promise<void> {
    return new Promise(resolve => {
      if (!this.db) {
        resolve();
        return;
      }
      const tx = this.db.transaction(STORE_NAME, 'readwrite');
      const store = tx.objectStore(STORE_NAME);
      const req = store.clear();
      req.onsuccess = () => resolve();
      req.onerror = () => resolve();
    });
  }
}
