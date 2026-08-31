import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  CreateModuleDependencyRequest,
  CreateSectorDomainRequest,
  CreateSectorSegmentRequest,
  ModuleDependencyDto,
  SaveDomainModuleOverlayRequest,
  SaveSectorDataTemplateRequest,
  SaveSectorDefaultSettingRequest,
  SaveSegmentModuleRulesRequest,
  SectorDataTemplateDto,
  SectorDefaultSettingDto,
  SectorDomainDto,
  SectorModuleRuleDto,
  SectorRuleParityDto,
  SectorRuleSeedResultDto,
  SectorRuleSetAdminDto,
  SectorSegmentDto,
  SegmentDomainLinkDto,
  SetSegmentDomainsRequest,
  UpdateSectorDomainRequest,
  UpdateSectorSegmentRequest
} from '@core/models/sector-rules.models';

/**
 * Phase 2 (WP-F6) — Client HTTP pour `/api/platform/sector-rules/*`.
 *
 * Pattern copié de `platform-plans.service.ts` : service root-provided, une méthode par
 * ressource, chaque appel renvoie `Observable<ApiResponse<T>>`.
 */
@Injectable({ providedIn: 'root' })
export class PlatformSectorRulesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/sector-rules`;

  /** Dump complet (bootstrap de la page d'administration). */
  getAll(): Observable<ApiResponse<SectorRuleSetAdminDto>> {
    return this.http.get<ApiResponse<SectorRuleSetAdminDto>>(this.base);
  }

  // ----- Segments ------------------------------------------------------
  listSegments(): Observable<ApiResponse<SectorSegmentDto[]>> {
    return this.http.get<ApiResponse<SectorSegmentDto[]>>(`${this.base}/segments`);
  }

  getSegment(id: string): Observable<ApiResponse<SectorSegmentDto>> {
    return this.http.get<ApiResponse<SectorSegmentDto>>(`${this.base}/segments/${id}`);
  }

  createSegment(request: CreateSectorSegmentRequest): Observable<ApiResponse<SectorSegmentDto>> {
    return this.http.post<ApiResponse<SectorSegmentDto>>(`${this.base}/segments`, request);
  }

  updateSegment(id: string, request: UpdateSectorSegmentRequest): Observable<ApiResponse<SectorSegmentDto>> {
    return this.http.put<ApiResponse<SectorSegmentDto>>(`${this.base}/segments/${id}`, request);
  }

  deactivateSegment(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/segments/${id}`);
  }

  // ----- Domaines --------------------------------------------------------
  listDomains(): Observable<ApiResponse<SectorDomainDto[]>> {
    return this.http.get<ApiResponse<SectorDomainDto[]>>(`${this.base}/domains`);
  }

  getDomain(id: string): Observable<ApiResponse<SectorDomainDto>> {
    return this.http.get<ApiResponse<SectorDomainDto>>(`${this.base}/domains/${id}`);
  }

  createDomain(request: CreateSectorDomainRequest): Observable<ApiResponse<SectorDomainDto>> {
    return this.http.post<ApiResponse<SectorDomainDto>>(`${this.base}/domains`, request);
  }

  updateDomain(id: string, request: UpdateSectorDomainRequest): Observable<ApiResponse<SectorDomainDto>> {
    return this.http.put<ApiResponse<SectorDomainDto>>(`${this.base}/domains/${id}`, request);
  }

  deactivateDomain(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/domains/${id}`);
  }

  // ----- Associations segment ↔ domaine -----------------------------------
  listSegmentDomains(): Observable<ApiResponse<SegmentDomainLinkDto[]>> {
    return this.http.get<ApiResponse<SegmentDomainLinkDto[]>>(`${this.base}/segment-domains`);
  }

  setSegmentDomains(
    segmentCode: string,
    domainCodes: string[]
  ): Observable<ApiResponse<SegmentDomainLinkDto>> {
    const request: SetSegmentDomainsRequest = { domainCodes };
    return this.http.put<ApiResponse<SegmentDomainLinkDto>>(
      `${this.base}/segment-domains/${segmentCode}`,
      request
    );
  }

  // ----- Règles de modules -------------------------------------------------
  listModuleRules(): Observable<ApiResponse<SectorModuleRuleDto[]>> {
    return this.http.get<ApiResponse<SectorModuleRuleDto[]>>(`${this.base}/module-rules`);
  }

  saveSegmentRules(
    segmentCode: string,
    moduleIds: number[]
  ): Observable<ApiResponse<SectorModuleRuleDto[]>> {
    const request: SaveSegmentModuleRulesRequest = { moduleIds };
    return this.http.put<ApiResponse<SectorModuleRuleDto[]>>(
      `${this.base}/module-rules/segments/${segmentCode}`,
      request
    );
  }

  saveDomainOverlay(
    domainCode: string,
    moduleIds: number[]
  ): Observable<ApiResponse<SectorModuleRuleDto[]>> {
    const request: SaveDomainModuleOverlayRequest = { moduleIds };
    return this.http.put<ApiResponse<SectorModuleRuleDto[]>>(
      `${this.base}/module-rules/domains/${domainCode}`,
      request
    );
  }

  // ----- Dépendances entre modules -----------------------------------------
  listDependencies(): Observable<ApiResponse<ModuleDependencyDto[]>> {
    return this.http.get<ApiResponse<ModuleDependencyDto[]>>(`${this.base}/module-dependencies`);
  }

  createDependency(request: CreateModuleDependencyRequest): Observable<ApiResponse<ModuleDependencyDto>> {
    return this.http.post<ApiResponse<ModuleDependencyDto>>(`${this.base}/module-dependencies`, request);
  }

  deleteDependency(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/module-dependencies/${id}`);
  }

  // ----- Paramètres par défaut ----------------------------------------------
  listDefaultSettings(): Observable<ApiResponse<SectorDefaultSettingDto[]>> {
    return this.http.get<ApiResponse<SectorDefaultSettingDto[]>>(`${this.base}/settings`);
  }

  saveDefaultSetting(
    request: SaveSectorDefaultSettingRequest
  ): Observable<ApiResponse<SectorDefaultSettingDto>> {
    return this.http.post<ApiResponse<SectorDefaultSettingDto>>(`${this.base}/settings`, request);
  }

  // ----- Modèles de données --------------------------------------------------
  listDataTemplates(): Observable<ApiResponse<SectorDataTemplateDto[]>> {
    return this.http.get<ApiResponse<SectorDataTemplateDto[]>>(`${this.base}/templates`);
  }

  saveDataTemplate(request: SaveSectorDataTemplateRequest): Observable<ApiResponse<SectorDataTemplateDto>> {
    return this.http.post<ApiResponse<SectorDataTemplateDto>>(`${this.base}/templates`, request);
  }

  // ----- Seed / parity -------------------------------------------------------
  seedFromCatalog(force = false): Observable<ApiResponse<SectorRuleSeedResultDto>> {
    let hp = new HttpParams();
    if (force) hp = hp.set('force', 'true');
    return this.http.post<ApiResponse<SectorRuleSeedResultDto>>(
      `${this.base}/seed-from-catalog`,
      {},
      { params: hp }
    );
  }

  getParity(): Observable<ApiResponse<SectorRuleParityDto>> {
    return this.http.get<ApiResponse<SectorRuleParityDto>>(`${this.base}/parity`);
  }
}
