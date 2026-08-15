import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from '@core/services/auth.service';
import {
  CashFlowForecast,
  CashFlowThresholds,
  RecurringCashCommitment,
  SaveCashFlowThresholdsRequest,
  SaveRecurringCashCommitmentRequest
} from '../models/cash-forecast.models';

/**
 * Accès à l'API de trésorerie prévisionnelle.
 *
 * Les énumérations sont déjà sérialisées en snake_case par le backend
 * (`CashFlowForecastMappings`) : aucune normalisation n'est nécessaire ici, contrairement au
 * module Prévisions IA dont le service doit convertir des enums PascalCase.
 */
@Injectable({ providedIn: 'root' })
export class CashForecastService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/treasury/cash-forecast`;

  getForecast(horizonMonths?: number): Observable<CashFlowForecast> {
    let params = new HttpParams();
    if (horizonMonths) params = params.set('horizonMonths', String(horizonMonths));

    return this.http
      .get<ApiResponse<CashFlowForecast>>(this.base, { params })
      .pipe(map(r => r.data!));
  }

  recompute(horizonMonths?: number): Observable<CashFlowForecast> {
    let params = new HttpParams();
    if (horizonMonths) params = params.set('horizonMonths', String(horizonMonths));

    return this.http
      .post<ApiResponse<CashFlowForecast>>(`${this.base}/recompute`, {}, { params })
      .pipe(map(r => r.data!));
  }

  /** URL d'export CSV — ouverte via le service de téléchargement authentifié. */
  exportUrl(runId: string): string {
    return `${this.base}/${runId}/export`;
  }

  exportCsv(runId: string): Observable<Blob> {
    return this.http.get(`${this.base}/${runId}/export`, { responseType: 'blob' });
  }

  getCommitments(includeInactive = false): Observable<RecurringCashCommitment[]> {
    const params = new HttpParams().set('includeInactive', String(includeInactive));

    return this.http
      .get<ApiResponse<RecurringCashCommitment[]>>(`${this.base}/commitments`, { params })
      .pipe(map(r => r.data ?? []));
  }

  createCommitment(
    request: SaveRecurringCashCommitmentRequest
  ): Observable<RecurringCashCommitment> {
    return this.http
      .post<ApiResponse<RecurringCashCommitment>>(`${this.base}/commitments`, request)
      .pipe(map(r => r.data!));
  }

  updateCommitment(
    id: string,
    request: SaveRecurringCashCommitmentRequest
  ): Observable<RecurringCashCommitment> {
    return this.http
      .put<ApiResponse<RecurringCashCommitment>>(`${this.base}/commitments/${id}`, request)
      .pipe(map(r => r.data!));
  }

  deactivateCommitment(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/commitments/${id}`);
  }

  getThresholds(): Observable<CashFlowThresholds> {
    return this.http
      .get<ApiResponse<CashFlowThresholds>>(`${this.base}/settings`)
      .pipe(map(r => r.data!));
  }

  saveThresholds(request: SaveCashFlowThresholdsRequest): Observable<CashFlowThresholds> {
    return this.http
      .put<ApiResponse<CashFlowThresholds>>(`${this.base}/settings`, request)
      .pipe(map(r => r.data!));
  }
}
