import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  CreateDunningCampaignRequest,
  DunningCampaignDto,
  DunningCampaignsListDto,
  DunningStatesPageDto,
  ExtendGracePeriodRequest,
  UpdateDunningCampaignRequest
} from '@core/models/platform.models';

/**
 * Lot C6 — Service HTTP CRUD campagnes dunning + audit + actions abonnement.
 *
 * Routes :
 * - `GET    /api/platform/dunning/campaigns`
 * - `POST   /api/platform/dunning/campaigns`
 * - `PUT    /api/platform/dunning/campaigns/{id}`
 * - `POST   /api/platform/dunning/campaigns/{id}/activate`
 * - `POST   /api/platform/dunning/campaigns/{id}/deactivate`
 * - `GET    /api/platform/dunning/states?outcome&tenantId`
 * - `POST   /api/platform/dunning/subscriptions/{id}/renew-now`
 * - `POST   /api/platform/dunning/subscriptions/{id}/extend-grace` (body: { days })
 */
@Injectable({ providedIn: 'root' })
export class PlatformDunningService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/dunning`;

  listCampaigns(): Observable<ApiResponse<DunningCampaignsListDto>> {
    return this.http.get<ApiResponse<DunningCampaignsListDto>>(`${this.base}/campaigns`);
  }

  getCampaign(id: string): Observable<ApiResponse<DunningCampaignDto>> {
    return this.http.get<ApiResponse<DunningCampaignDto>>(`${this.base}/campaigns/${id}`);
  }

  createCampaign(request: CreateDunningCampaignRequest): Observable<ApiResponse<DunningCampaignDto>> {
    return this.http.post<ApiResponse<DunningCampaignDto>>(`${this.base}/campaigns`, request);
  }

  updateCampaign(id: string, request: UpdateDunningCampaignRequest): Observable<ApiResponse<DunningCampaignDto>> {
    return this.http.put<ApiResponse<DunningCampaignDto>>(`${this.base}/campaigns/${id}`, request);
  }

  activate(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/campaigns/${id}/activate`, {});
  }

  deactivate(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/campaigns/${id}/deactivate`, {});
  }

  listStates(
    outcome?: string | null,
    tenantId?: string | null,
    page = 1,
    pageSize = 50
  ): Observable<ApiResponse<DunningStatesPageDto>> {
    let hp = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    if (outcome) hp = hp.set('outcome', outcome);
    if (tenantId) hp = hp.set('tenantId', tenantId);
    return this.http.get<ApiResponse<DunningStatesPageDto>>(`${this.base}/states`, { params: hp });
  }

  renewNow(subscriptionId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/subscriptions/${subscriptionId}/renew-now`, {});
  }

  extendGrace(subscriptionId: string, request: ExtendGracePeriodRequest): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/subscriptions/${subscriptionId}/extend-grace`, request);
  }
}
