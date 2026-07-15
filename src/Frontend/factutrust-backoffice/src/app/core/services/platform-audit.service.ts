import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  AuditChainVerificationDto,
  AuditLogDetailDto,
  AuditLogListParams,
  AuditLogPageDto
} from '@core/models/platform.models';

/**
 * Lot B3 — Service consommant les endpoints `/api/platform/audit/tenants/{id}/...`.
 *
 * Toutes les méthodes exigent un `tenantId` car les audit logs sont stockés par tenant.
 * Le téléchargement des exports utilise `responseType: 'blob'` ; la conversion fichier
 * (download trigger via lien temporaire) est faite côté composant pour préserver l'UX.
 */
@Injectable({ providedIn: 'root' })
export class PlatformAuditService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/audit/tenants`;

  list(tenantId: string, params: AuditLogListParams = {}): Observable<ApiResponse<AuditLogPageDto>> {
    let hp = new HttpParams();
    if (params.from) hp = hp.set('from', params.from);
    if (params.to) hp = hp.set('to', params.to);
    if (params.action?.trim()) hp = hp.set('action', params.action.trim());
    if (params.userId) hp = hp.set('userId', params.userId);
    if (params.entityType?.trim()) hp = hp.set('entityType', params.entityType.trim());
    hp = hp.set('page', String(params.page ?? 1)).set('pageSize', String(params.pageSize ?? 25));
    return this.http.get<ApiResponse<AuditLogPageDto>>(`${this.base}/${tenantId}/logs`, { params: hp });
  }

  getById(tenantId: string, logId: string): Observable<ApiResponse<AuditLogDetailDto>> {
    return this.http.get<ApiResponse<AuditLogDetailDto>>(`${this.base}/${tenantId}/logs/${logId}`);
  }

  integrityReport(tenantId: string): Observable<ApiResponse<AuditChainVerificationDto>> {
    return this.http.get<ApiResponse<AuditChainVerificationDto>>(
      `${this.base}/${tenantId}/integrity-report`
    );
  }

  /** Renvoie un Blob (CSV ou PDF) — le composant gère le download. */
  export(
    tenantId: string,
    format: 'csv' | 'pdf',
    params: AuditLogListParams = {}
  ): Observable<Blob> {
    let hp = new HttpParams().set('format', format);
    if (params.from) hp = hp.set('from', params.from);
    if (params.to) hp = hp.set('to', params.to);
    if (params.action?.trim()) hp = hp.set('action', params.action.trim());
    if (params.userId) hp = hp.set('userId', params.userId);
    if (params.entityType?.trim()) hp = hp.set('entityType', params.entityType.trim());
    return this.http.get(`${this.base}/${tenantId}/export`, {
      params: hp,
      responseType: 'blob'
    });
  }
}
