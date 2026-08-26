import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map, switchMap, tap } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { ApiResponse, AuthService } from '@core/services/auth.service';

export interface CashRegisterDto {
  id: string;
  code: string;
  name: string;
  warehouseId: string;
  isActive: boolean;
  isDefault: boolean;
  requireOpenSession: boolean;
}

export interface CashRegisterSessionDto {
  id: string;
  cashRegisterId: string;
  cashRegisterCode: string;
  cashRegisterName: string;
  warehouseId: string;
  status: number;
  openedAt: string;
  openedByUserId: string;
  openingFloat: number;
  closedAt?: string | null;
  zReportId?: string | null;
  zReportNumber?: string | null;
}

export interface PosZTotalsByMethodDto {
  method: number;
  methodDisplay: string;
  amount: number;
}

export interface PosSessionReportDto {
  sessionId: string;
  cashRegisterId: string;
  cashRegisterName: string;
  openedAt: string;
  closedAt?: string | null;
  openingFloat: number;
  expectedCash: number;
  countedCash?: number | null;
  cashVariance?: number | null;
  invoiceCount: number;
  creditNoteCount: number;
  heldTicketCount: number;
  totalsByMethod: PosZTotalsByMethodDto[];
  invoiceIds: string[];
  zReportNumber?: string | null;
  notes?: string | null;
}

export interface ZReportListItemDto {
  id: string;
  number: string;
  cashRegisterSessionId: string;
  cashRegisterId: string;
  cashRegisterName: string;
  generatedAt: string;
  openingFloat: number;
  expectedCash: number;
  countedCash: number;
  cashVariance: number;
}

@Injectable({
  providedIn: 'root'
})
export class PosRegisterSessionService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.apiUrl}/pos`;

  private readonly session = signal<CashRegisterSessionDto | null>(null);
  private readonly register = signal<CashRegisterDto | null>(null);
  private readonly registers = signal<CashRegisterDto[]>([]);
  private readonly selectedId = signal<string | null>(null);
  private readonly loading = signal(false);

  readonly currentSession = this.session.asReadonly();
  readonly currentRegister = this.register.asReadonly();
  readonly availableRegisters = this.registers.asReadonly();
  readonly selectedRegisterId = this.selectedId.asReadonly();
  readonly isLoading = this.loading.asReadonly();
  readonly requireOpenSession = computed(() => this.register()?.requireOpenSession !== false);
  readonly canSell = computed(() => !this.requireOpenSession() || this.session() != null);

  hydrate(warehouseId: string | null | undefined, cashRegisterId?: string | null): Observable<CashRegisterSessionDto | null> {
    if (!warehouseId) {
      this.session.set(null);
      this.register.set(null);
      this.registers.set([]);
      this.selectedId.set(null);
      return of(null);
    }

    this.loading.set(true);
    const persisted = cashRegisterId || this.readPersistedRegisterId(warehouseId);

    return this.listRegisters(warehouseId).pipe(
      catchError(() => of([] as CashRegisterDto[])),
      switchMap(list => {
        this.registers.set(list);
        const chosen =
          list.find(r => r.id === persisted)
          ?? list.find(r => r.isDefault)
          ?? list[0]
          ?? null;
        this.register.set(chosen);
        this.selectedId.set(chosen?.id ?? null);
        if (chosen) {
          this.persistRegisterId(warehouseId, chosen.id);
        }

        let params = new HttpParams().set('warehouseId', warehouseId);
        if (chosen?.id) {
          params = params.set('cashRegisterId', chosen.id);
        }

        return this.http
          .get<ApiResponse<CashRegisterSessionDto | null>>(`${this.apiUrl}/open-session`, { params })
          .pipe(
            catchError(() => of({
              success: false,
              data: null,
              message: null,
              errors: []
            } as ApiResponse<CashRegisterSessionDto | null>))
          );
      }),
      tap(sessionRes => {
        this.session.set(sessionRes.success ? sessionRes.data ?? null : null);
        this.loading.set(false);
      }),
      map(sessionRes => (sessionRes.success ? sessionRes.data ?? null : null))
    );
  }

  listRegisters(warehouseId: string): Observable<CashRegisterDto[]> {
    const params = new HttpParams().set('warehouseId', warehouseId);
    return this.http.get<ApiResponse<CashRegisterDto[]>>(`${this.apiUrl}/registers`, { params }).pipe(
      map(res => (res.success && res.data ? res.data : [])),
      catchError(() => of([]))
    );
  }

  createRegister(payload: {
    warehouseId: string;
    code: string;
    name: string;
    isDefault: boolean;
  }): Observable<CashRegisterDto> {
    return this.http.post<ApiResponse<CashRegisterDto>>(`${this.apiUrl}/registers`, payload).pipe(
      map(res => {
        if (!res.success || !res.data) {
          throw new Error(res.message || 'Impossible de creer la caisse');
        }
        return res.data;
      })
    );
  }

  listZReports(from: Date, to: Date, registerId?: string | null): Observable<ZReportListItemDto[]> {
    let params = new HttpParams()
      .set('from', from.toISOString())
      .set('to', to.toISOString());
    if (registerId) {
      params = params.set('registerId', registerId);
    }
    return this.http.get<ApiResponse<ZReportListItemDto[]>>(`${this.apiUrl}/z-reports`, { params }).pipe(
      map(res => (res.success && res.data ? res.data : [])),
      catchError(() => of([]))
    );
  }

  selectRegister(warehouseId: string, registerId: string): void {
    const found = this.registers().find(r => r.id === registerId) ?? null;
    this.register.set(found);
    this.selectedId.set(registerId);
    this.persistRegisterId(warehouseId, registerId);
  }

  open(warehouseId: string, openingFloat: number, cashRegisterId?: string | null): Observable<CashRegisterSessionDto> {
    const body: { warehouseId: string; openingFloat: number; cashRegisterId?: string } = {
      warehouseId,
      openingFloat
    };
    if (cashRegisterId) {
      body.cashRegisterId = cashRegisterId;
      this.selectRegister(warehouseId, cashRegisterId);
    }
    return this.http
      .post<ApiResponse<CashRegisterSessionDto>>(`${this.apiUrl}/session/open`, body)
      .pipe(
        map(res => {
          if (!res.success || !res.data) {
            throw new Error(res.message || "Impossible d'ouvrir la caisse");
          }
          this.session.set(res.data);
          return res.data;
        })
      );
  }

  getXReport(warehouseId: string): Observable<PosSessionReportDto> {
    let params = new HttpParams().set('warehouseId', warehouseId);
    const registerId = this.selectedId();
    if (registerId) {
      params = params.set('cashRegisterId', registerId);
    }
    return this.http
      .get<ApiResponse<PosSessionReportDto>>(`${this.apiUrl}/session/x-report`, { params })
      .pipe(
        map(res => {
          if (!res.success || !res.data) {
            throw new Error(res.message || 'Rapport X indisponible');
          }
          return res.data;
        })
      );
  }

  close(warehouseId: string, countedCash: number, notes?: string): Observable<PosSessionReportDto> {
    let params = new HttpParams().set('warehouseId', warehouseId);
    const registerId = this.selectedId();
    if (registerId) {
      params = params.set('cashRegisterId', registerId);
    }
    return this.http
      .post<ApiResponse<PosSessionReportDto>>(
        `${this.apiUrl}/session/close`,
        { countedCash, notes: notes || null },
        { params }
      )
      .pipe(
        map(res => {
          if (!res.success || !res.data) {
            throw new Error(res.message || 'Clôture Z impossible');
          }
          this.session.set(null);
          return res.data;
        })
      );
  }

  private persistRegisterId(warehouseId: string, registerId: string): void {
    try {
      localStorage.setItem(this.storageKey(warehouseId), registerId);
    } catch {
      /* ignore quota */
    }
  }

  private readPersistedRegisterId(warehouseId: string): string | null {
    try {
      return localStorage.getItem(this.storageKey(warehouseId));
    } catch {
      return null;
    }
  }

  private storageKey(warehouseId: string): string {
    const user = this.auth.user();
    return `factutrust_pos_register:${user?.tenantId ?? 't'}:${user?.id ?? 'u'}:${warehouseId}`;
  }
}
