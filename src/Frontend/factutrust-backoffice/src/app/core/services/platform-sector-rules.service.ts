import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  CreateModuleDependencyRequest,
  CreateSectorDataTemplateRequest,
  CreateSectorDefaultSettingRequest,
  CreateSectorDomainRequest,
  CreateSectorModuleRuleRequest,
  CreateSectorSegmentDomainRequest,
  CreateSectorSegmentRequest,
  ModuleDependencyDto,
  SectorDataTemplateDto,
  SectorDefaultSettingDto,
  SectorDomainDto,
  SectorModuleRuleDto,
  SectorRuleParityDto,
  SectorRuleSeedResultDto,
  SectorRuleSetAdminDto,
  SectorSegmentDomainDto,
  SectorSegmentDto,
  UpdateSectorDataTemplateRequest,
  UpdateSectorDefaultSettingRequest,
  UpdateSectorDomainRequest,
  UpdateSectorModuleRuleRequest,
  UpdateSectorSegmentDomainRequest,
  UpdateSectorSegmentRequest
} from '@core/models/sector-rules.models';

/**
 * Phase 2 (WP-F6) — Client HTTP pour `/api/platform/sector-rules/*`.
 *
 * Une méthode par ressource backend, chaque appel renvoie `Observable<ApiResponse<T>>`. Les ids
 * sont des GUID (chaînes) ; les `DELETE` désactivent en douceur côté backend (soft-delete). Les
 * dépendances de modules n'exposent que POST + DELETE (pas de PUT côté backend).
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

  // ----- Associations segment ↔ domaine (GUIDs) --------------------------
  listSegmentDomains(): Observable<ApiResponse<SectorSegmentDomainDto[]>> {
    return this.http.get<ApiResponse<SectorSegmentDomainDto[]>>(`${this.base}/segment-domains`);
  }

  getSegmentDomain(id: string): Observable<ApiResponse<SectorSegmentDomainDto>> {
    return this.http.get<ApiResponse<SectorSegmentDomainDto>>(`${this.base}/segment-domains/${id}`);
  }

  createSegmentDomain(request: CreateSectorSegmentDomainRequest): Observable<ApiResponse<SectorSegmentDomainDto>> {
    return this.http.post<ApiResponse<SectorSegmentDomainDto>>(`${this.base}/segment-domains`, request);
  }

  updateSegmentDomain(id: string, request: UpdateSectorSegmentDomainRequest): Observable<ApiResponse<SectorSegmentDomainDto>> {
    return this.http.put<ApiResponse<SectorSegmentDomainDto>>(`${this.base}/segment-domains/${id}`, request);
  }

  deactivateSegmentDomain(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/segment-domains/${id}`);
  }

  // ----- Règles de modules -------------------------------------------------
  listModuleRules(): Observable<ApiResponse<SectorModuleRuleDto[]>> {
    return this.http.get<ApiResponse<SectorModuleRuleDto[]>>(`${this.base}/module-rules`);
  }

  getModuleRule(id: string): Observable<ApiResponse<SectorModuleRuleDto>> {
    return this.http.get<ApiResponse<SectorModuleRuleDto>>(`${this.base}/module-rules/${id}`);
  }

  createModuleRule(request: CreateSectorModuleRuleRequest): Observable<ApiResponse<SectorModuleRuleDto>> {
    return this.http.post<ApiResponse<SectorModuleRuleDto>>(`${this.base}/module-rules`, request);
  }

  updateModuleRule(id: string, request: UpdateSectorModuleRuleRequest): Observable<ApiResponse<SectorModuleRuleDto>> {
    return this.http.put<ApiResponse<SectorModuleRuleDto>>(`${this.base}/module-rules/${id}`, request);
  }

  deactivateModuleRule(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/module-rules/${id}`);
  }

  // ----- Dépendances entre modules (POST + DELETE only) -------------------
  listModuleDependencies(): Observable<ApiResponse<ModuleDependencyDto[]>> {
    return this.http.get<ApiResponse<ModuleDependencyDto[]>>(`${this.base}/module-dependencies`);
  }

  getModuleDependency(id: string): Observable<ApiResponse<ModuleDependencyDto>> {
    return this.http.get<ApiResponse<ModuleDependencyDto>>(`${this.base}/module-dependencies/${id}`);
  }

  createModuleDependency(request: CreateModuleDependencyRequest): Observable<ApiResponse<ModuleDependencyDto>> {
    return this.http.post<ApiResponse<ModuleDependencyDto>>(`${this.base}/module-dependencies`, request);
  }

  deactivateModuleDependency(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/module-dependencies/${id}`);
  }

  // ----- Paramètres par défaut ----------------------------------------------
  listSettings(): Observable<ApiResponse<SectorDefaultSettingDto[]>> {
    return this.http.get<ApiResponse<SectorDefaultSettingDto[]>>(`${this.base}/settings`);
  }

  getSetting(id: string): Observable<ApiResponse<SectorDefaultSettingDto>> {
    return this.http.get<ApiResponse<SectorDefaultSettingDto>>(`${this.base}/settings/${id}`);
  }

  createSetting(request: CreateSectorDefaultSettingRequest): Observable<ApiResponse<SectorDefaultSettingDto>> {
    return this.http.post<ApiResponse<SectorDefaultSettingDto>>(`${this.base}/settings`, request);
  }

  updateSetting(id: string, request: UpdateSectorDefaultSettingRequest): Observable<ApiResponse<SectorDefaultSettingDto>> {
    return this.http.put<ApiResponse<SectorDefaultSettingDto>>(`${this.base}/settings/${id}`, request);
  }

  deactivateSetting(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/settings/${id}`);
  }

  // ----- Modèles de données --------------------------------------------------
  listTemplates(): Observable<ApiResponse<SectorDataTemplateDto[]>> {
    return this.http.get<ApiResponse<SectorDataTemplateDto[]>>(`${this.base}/templates`);
  }

  getTemplate(id: string): Observable<ApiResponse<SectorDataTemplateDto>> {
    return this.http.get<ApiResponse<SectorDataTemplateDto>>(`${this.base}/templates/${id}`);
  }

  createTemplate(request: CreateSectorDataTemplateRequest): Observable<ApiResponse<SectorDataTemplateDto>> {
    return this.http.post<ApiResponse<SectorDataTemplateDto>>(`${this.base}/templates`, request);
  }

  updateTemplate(id: string, request: UpdateSectorDataTemplateRequest): Observable<ApiResponse<SectorDataTemplateDto>> {
    return this.http.put<ApiResponse<SectorDataTemplateDto>>(`${this.base}/templates/${id}`, request);
  }

  deactivateTemplate(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/templates/${id}`);
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
