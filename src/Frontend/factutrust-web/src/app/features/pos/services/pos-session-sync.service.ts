import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { PosStateService, PosState } from './pos-state.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';

const STORAGE_KEY = 'factutrust_pos_cloud_draft';
const LOCAL_DRAFT_KEY = 'factutrust_pos_draft';

@Injectable({
  providedIn: 'root'
})
export class PosSessionSyncService {
  private readonly http = inject(HttpClient);
  private readonly posState = inject(PosStateService);
  private readonly warehouseContext = inject(WarehouseContextService);
  private readonly apiUrl = `${environment.apiUrl}/pos`;
  private debounceHandle: ReturnType<typeof setTimeout> | null = null;
  private lastHadLines = false;

  scheduleSave(): void {
    if (this.debounceHandle) {
      clearTimeout(this.debounceHandle);
    }
    this.debounceHandle = setTimeout(() => this.flush(), 1000);
  }

  private warehouseId(): string | null {
    return this.warehouseContext.selectedWarehouseId();
  }

  private cartUrl(warehouseId: string | null): { url: string; params: HttpParams } {
    let params = new HttpParams();
    if (warehouseId) {
      params = params.set('warehouseId', warehouseId);
    }
    return { url: `${this.apiUrl}/cart`, params };
  }

  private flush(): void {
    const snapshot = this.posState.getSnapshot();
    const warehouseId = this.warehouseId();
    const hasLines = snapshot.lines.length > 0;

    if (!hasLines) {
      if (this.lastHadLines) {
        this.clearCloudSession();
      }
      this.lastHadLines = false;
      return;
    }

    this.lastHadLines = true;
    const { url, params } = this.cartUrl(warehouseId);
    this.http
      .put<{ success: boolean }>(url, { warehouseId, state: snapshot }, { params })
      .pipe(
        catchError(() => {
          this.writeLocal(snapshot);
          return of(null);
        })
      )
      .subscribe();
  }

  saveToCloud(): Observable<boolean> {
    this.flush();
    return of(true);
  }

  getFromCloud(): Observable<PosState | null> {
    const warehouseId = this.warehouseId();
    const { url, params } = this.cartUrl(warehouseId);
    return this.http.get<{ data?: { state?: PosState } }>(url, { params }).pipe(
      map(res => {
        const server = res?.data?.state ?? null;
        if (server && (server.lines?.length ?? 0) > 0) {
          return server;
        }
        const local = this.readLocal();
        if (local && (local.lines?.length ?? 0) > 0) {
          return local;
        }
        return server;
      }),
      catchError(() => of(this.readLocal()))
    );
  }

  hasCloudSession(): Observable<boolean> {
    return this.getFromCloud().pipe(
      map(s => {
        if (s && (s.lines?.length ?? 0) > 0) {
          return true;
        }
        const local = this.readLocal();
        return !!local && (local.lines?.length ?? 0) > 0;
      })
    );
  }

  clearCloudSession(): void {
    const warehouseId = this.warehouseId();
    const { url, params } = this.cartUrl(warehouseId);
    this.http.delete(url, { params }).pipe(catchError(() => of(null))).subscribe();
    this.clearLocal();
    this.lastHadLines = false;
  }

  private writeLocal(snapshot: PosState): void {
    try {
      const raw = JSON.stringify(snapshot);
      localStorage.setItem(STORAGE_KEY, raw);
      localStorage.setItem(LOCAL_DRAFT_KEY, raw);
    } catch {
      // ignore quota / private mode
    }
  }

  private readLocal(): PosState | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY) ?? localStorage.getItem(LOCAL_DRAFT_KEY);
      if (!raw) {
        return null;
      }
      return JSON.parse(raw) as PosState;
    } catch {
      return null;
    }
  }

  private clearLocal(): void {
    try {
      localStorage.removeItem(STORAGE_KEY);
      localStorage.removeItem(LOCAL_DRAFT_KEY);
    } catch {
      // ignore
    }
  }
}
