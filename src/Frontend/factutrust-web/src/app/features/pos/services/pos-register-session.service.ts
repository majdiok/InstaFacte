import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { ApiResponse } from '@core/services/auth.service';

export interface CashRegisterDto {
  id: string;
  code: string;
  name: string;
  warehouseId: string;
  isActive: boolean;
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

@Injectable({
  providedIn: 'root'
})
export class PosRegisterSessionService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/pos`;

  private readonly session = signal<CashRegisterSessionDto | null>(null);
  private readonly register = signal<CashRegisterDto | null>(null);
  private readonly loading = signal(false);

  readonly currentSession = this.session.asReadonly();
  readonly currentRegister = this.register.asReadonly();
  readonly isLoading = this.loading.asReadonly();
  readonly requireOpenSession = computed(() => this.register()?.requireOpenSession !== false);
  readonly canSell = computed(() => !this.requireOpenSession() || this.session() != null);

  hydrate(warehouseId: string | null | undefined): Observable<CashRegisterSessionDto | null> {
    if (!warehouseId) {
      this.session.set(null);
      this.register.set(null);
      return of(null);
    }

    this.loading.set(true);
    const params = new HttpParams().set('warehouseId', warehouseId);
    return forkJoin({
      register: this.http.get<ApiResponse<CashRegisterDto>>(`${this.apiUrl}/register`, { params }).pipe(
        catchError(() => of({ success: false, data: null as unknown as CashRegisterDto, message: null, errors: [] }))
      ),
      session: this.http
        .get<ApiResponse<CashRegisterSessionDto | null>>(`${this.apiUrl}/open-session`, { params })
        .pipe(catchError(() => of({
          success: false,
          data: null,
          message: null,
          errors: []
        } as ApiResponse<CashRegisterSessionDto | null>)))
    }).pipe(
      tap(({ register, session }) => {
        this.register.set(register.success ? register.data ?? null : null);
        this.session.set(session.success ? session.data ?? null : null);
        this.loading.set(false);
      }),
      map(({ session }) => (session.success ? session.data ?? null : null))
    );
  }

  open(warehouseId: string, openingFloat: number): Observable<CashRegisterSessionDto> {
    return this.http
      .post<ApiResponse<CashRegisterSessionDto>>(`${this.apiUrl}/session/open`, {
        warehouseId,
        openingFloat
      })
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
    const params = new HttpParams().set('warehouseId', warehouseId);
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
    const params = new HttpParams().set('warehouseId', warehouseId);
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
}
