import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  SectorReconfigurationApplyResultDto,
  SectorReconfigurationPreviewDto,
  SectorReconfigurationRequestDto
} from '@core/models/sector-rules.models';

/**
 * Phase 2 (WP-F6/WP-F8) — Client HTTP pour la reconfiguration sectorielle d'un tenant existant.
 *
 * `preview()` n'a strictement aucun effet de bord côté backend (dryRun) ; `apply()` mute
 * classification + grants + modèles de données. Toujours faire précéder un `apply` d'un
 * `preview` réussi côté UI (cf. WP-F8, `ft-confirm-action`).
 */
@Injectable({ providedIn: 'root' })
export class PlatformTenantSectorService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/tenants`;

  preview(
    tenantId: string,
    request: SectorReconfigurationRequestDto
  ): Observable<ApiResponse<SectorReconfigurationPreviewDto>> {
    return this.http.post<ApiResponse<SectorReconfigurationPreviewDto>>(
      `${this.base}/${tenantId}/sector-configuration/preview`,
      request
    );
  }

  apply(
    tenantId: string,
    request: SectorReconfigurationRequestDto
  ): Observable<ApiResponse<SectorReconfigurationApplyResultDto>> {
    return this.http.post<ApiResponse<SectorReconfigurationApplyResultDto>>(
      `${this.base}/${tenantId}/sector-configuration/apply`,
      request
    );
  }
}
