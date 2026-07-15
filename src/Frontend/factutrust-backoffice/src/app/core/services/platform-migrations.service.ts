import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  MigrationResultDto,
  MigrationStatsDto,
  MigrationStatusResultDto
} from '@core/models/platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformMigrationsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/migrations`;

  listStatus(): Observable<ApiResponse<MigrationStatusResultDto[]>> {
    return this.http.get<ApiResponse<MigrationStatusResultDto[]>>(`${this.base}/tenants/migrations-status`);
  }

  /** Lot A3 : KPIs agrégés. */
  stats(): Observable<ApiResponse<MigrationStatsDto>> {
    return this.http.get<ApiResponse<MigrationStatsDto>>(`${this.base}/stats`);
  }

  /** Statut migration pour un tenant unique (rafraîchissement après apply). */
  statusFor(tenantId: string): Observable<ApiResponse<MigrationStatusResultDto>> {
    return this.http.get<ApiResponse<MigrationStatusResultDto>>(
      `${this.base}/tenants/${tenantId}/migrations-status`
    );
  }

  applyAll(): Observable<ApiResponse<MigrationResultDto>> {
    return this.http.post<ApiResponse<MigrationResultDto>>(`${this.base}/tenants/apply-migrations`, {});
  }

  applyOne(tenantId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/tenants/${tenantId}/apply-migrations`, {});
  }
}
