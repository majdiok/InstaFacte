import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

export interface AuditLogEntryDto {
  id: string;
  createdAt: string;
  userId?: string | null;
  userEmail: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  entityLabel?: string | null;
}

export interface AuditLogDetailDto extends AuditLogEntryDto {
  oldValues?: string | null;
  newValues?: string | null;
  ipAddress: string;
  userAgent?: string | null;
  previousHash: string;
  hash: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface AuditChainVerificationDto {
  isValid: boolean;
  entryCount: number;
  firstBrokenEntryId?: string | null;
  /** IntegrityMismatch | ChainMismatch */
  firstFailureReason?: string | null;
  duplicatePreviousHashGroupCount?: number;
}

interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
}

export interface GetLogsParams {
  from?: Date;
  to?: Date;
  action?: string;
  userId?: string;
  entityType?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly http = inject(HttpClient);
  /** apiUrl already ends with /api — do not use /api/audit-logs here. */
  private readonly base = `${environment.apiUrl}/audit-logs`;

  getLogs(params?: GetLogsParams): Observable<ApiResponse<PagedResult<AuditLogEntryDto>>> {
    let p = new HttpParams();
    if (params?.from) p = p.set('from', params.from.toISOString());
    if (params?.to) p = p.set('to', params.to.toISOString());
    if (params?.action) p = p.set('action', params.action);
    if (params?.userId) p = p.set('userId', params.userId);
    if (params?.entityType) p = p.set('entityType', params.entityType);
    if (params?.page != null) p = p.set('page', String(params.page));
    if (params?.pageSize != null) p = p.set('pageSize', String(params.pageSize));
    return this.http.get<ApiResponse<PagedResult<AuditLogEntryDto>>>(this.base, { params: p });
  }

  getById(id: string): Observable<ApiResponse<AuditLogDetailDto>> {
    return this.http.get<ApiResponse<AuditLogDetailDto>>(`${this.base}/${id}`);
  }

  verifyIntegrity(): Observable<ApiResponse<AuditChainVerificationDto>> {
    return this.http.get<ApiResponse<AuditChainVerificationDto>>(`${this.base}/verify-integrity`);
  }
}
