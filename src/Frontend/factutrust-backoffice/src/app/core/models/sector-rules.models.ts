/**
 * Phase 2 (WP-F6) — DTOs pour le moteur de règles sectorielles en base.
 *
 * Tous les contrats `/api/platform/sector-rules/*` et
 * `/api/platform/tenants/{id}/sector-configuration/*` sont regroupés dans CE SEUL fichier afin
 * de localiser tout écart avec les DTOs backend (WP-B5/B7/B9) à un unique endroit lors de
 * l'intégration.
 *
 * Codes segment/domaine : kebab-case (ex: "commerce", "sante-paramedical"). Ids de module :
 * entiers `AppModule` (voir `core/models/module-catalog.ts`).
 */

// ============================================================================
// Segments
// ============================================================================

export interface SectorSegmentDto {
  id: string;
  code: string;
  label: string;
  subtitle?: string | null;
  icon?: string | null;
  tone?: string | null;
  isActive: boolean;
  sortOrder: number;
  /** Nom d'entrepôt par défaut suggéré pour ce segment (peut être vide). */
  defaultWarehouseName?: string | null;
  /** Additif D4 — codes des domaines associés, dans l'ordre d'affichage. */
  domainCodes?: string[];
}

export interface CreateSectorSegmentRequest {
  code: string;
  label: string;
  subtitle?: string | null;
  icon?: string | null;
  tone?: string | null;
  sortOrder: number;
  defaultWarehouseName?: string | null;
}

export interface UpdateSectorSegmentRequest {
  label: string;
  subtitle?: string | null;
  icon?: string | null;
  tone?: string | null;
  sortOrder: number;
  defaultWarehouseName?: string | null;
  isActive: boolean;
}

// ============================================================================
// Domaines
// ============================================================================

export interface SectorDomainDto {
  id: string;
  code: string;
  label: string;
  icon?: string | null;
  tone?: string | null;
  isActive: boolean;
  sortOrder: number;
}

export interface CreateSectorDomainRequest {
  code: string;
  label: string;
  icon?: string | null;
  tone?: string | null;
  sortOrder: number;
}

export interface UpdateSectorDomainRequest {
  label: string;
  icon?: string | null;
  tone?: string | null;
  sortOrder: number;
  isActive: boolean;
}

// ============================================================================
// Associations segment ↔ domaine
// ============================================================================

/** Vue « ordered list » pour un segment donné — utilisée par la matrice d'associations. */
export interface SegmentDomainLinkDto {
  segmentCode: string;
  /** Codes de domaines associés, dans l'ordre d'affichage (autre pas nécessairement inclus). */
  domainCodes: string[];
}

export interface SetSegmentDomainsRequest {
  domainCodes: string[];
}

// ============================================================================
// Règles de modules (base segment + surcouche domaine)
// ============================================================================

/** `RuleKind` backend : 0 = SegmentBase, 1 = DomainOverlay. */
export type SectorModuleRuleKind = 0 | 1;

export interface SectorModuleRuleDto {
  id: string;
  ruleKind: SectorModuleRuleKind;
  segmentCode: string | null;
  domainCode: string | null;
  moduleId: number;
  isActive: boolean;
}

export interface SaveSegmentModuleRulesRequest {
  moduleIds: number[];
}

export interface SaveDomainModuleOverlayRequest {
  moduleIds: number[];
}

// ============================================================================
// Dépendances entre modules
// ============================================================================

export interface ModuleDependencyDto {
  id: string;
  moduleId: number;
  requiresModuleId: number;
  isActive: boolean;
}

export interface CreateModuleDependencyRequest {
  moduleId: number;
  requiresModuleId: number;
}

// ============================================================================
// Paramètres par défaut
// ============================================================================

export type SectorSettingValueType = 'string' | 'int' | 'bool' | 'json';

export interface SectorDefaultSettingDto {
  id: string;
  segmentCode: string | null;
  domainCode?: string | null;
  settingKey: string;
  valueType: SectorSettingValueType;
  value: string;
  isActive: boolean;
}

export interface SaveSectorDefaultSettingRequest {
  segmentCode: string | null;
  domainCode?: string | null;
  settingKey: string;
  valueType: SectorSettingValueType;
  value: string;
}

// ============================================================================
// Modèles de données (types de documents, plan comptable, paramètres)
// ============================================================================

export type SectorDataTemplateItemKind = 'document-numbering-scheme' | 'chart-account' | 'setting';

export interface SectorDataTemplateItemDto {
  id?: string;
  itemKind: SectorDataTemplateItemKind;
  /** JSON brut du payload — contrat exact dépend de `itemKind` (voir backend WP-B6). */
  payloadJson: string;
  sortOrder: number;
}

export interface SectorDataTemplateDto {
  id: string;
  code: string;
  label: string;
  segmentCode: string | null;
  domainCode: string | null;
  isActive: boolean;
  sortOrder: number;
  items: SectorDataTemplateItemDto[];
}

export interface SaveSectorDataTemplateRequest {
  code: string;
  label: string;
  segmentCode: string | null;
  domainCode: string | null;
  sortOrder: number;
  items: SectorDataTemplateItemDto[];
}

// ============================================================================
// Bootstrap / dump complet
// ============================================================================

export interface SectorRuleSetAdminDto {
  version: number;
  updatedAtUtc: string | null;
  updatedBy: string | null;
  /** Reflet de `Features:RegistrationSector:UseDbRules` côté backend (bannière de source). */
  useDbRules: boolean;
  segments: SectorSegmentDto[];
  domains: SectorDomainDto[];
  segmentDomains: SegmentDomainLinkDto[];
  moduleRules: SectorModuleRuleDto[];
  dependencies: ModuleDependencyDto[];
  defaultSettings: SectorDefaultSettingDto[];
  dataTemplates: SectorDataTemplateDto[];
}

export interface SectorRuleSeedResultDto {
  seededSegments: number;
  seededDomains: number;
  seededLinks: number;
  seededModuleRules: number;
  seededDependencies: number;
  seededSettings: number;
  seededTemplates: number;
  version: number;
}

export interface SectorRuleParityDto {
  isMatch: boolean;
  dbVersion: number;
  differences: string[];
}

// ============================================================================
// Reconfiguration sectorielle tenant (preview / apply) — WP-F8
// ============================================================================

export interface TenantSectorConfigurationDto {
  companySegment: string | null;
  businessDomain: string | null;
}

export interface SectorReconfigurationRequestDto {
  /** null = conserver la valeur actuelle. */
  companySegment?: string | null;
  /** null = conserver la valeur actuelle ; "" = effacer explicitement. */
  businessDomain?: string | null;
  recomputeModuleGrants?: boolean;
  applyDataTemplates?: boolean;
  /** null = tous les utilisateurs actifs du tenant. */
  userIds?: string[] | null;
}

export interface UserModuleDiffDto {
  userId: string;
  currentEnabledModuleIds: number[];
  targetEnabledModuleIds: number[];
  modulesToEnable: number[];
  modulesToDisable: number[];
}

export type TemplateItemOutcomeStatus = 'created' | 'existing' | 'skipped';

export interface TemplateItemOutcomeDto {
  kind: SectorDataTemplateItemKind | string;
  outcome: TemplateItemOutcomeStatus;
  detail?: string | null;
}

export interface TemplatePreviewDto {
  code: string;
  version: number;
  alreadyApplied: boolean;
  itemOutcomes: TemplateItemOutcomeDto[];
}

export interface SettingPreviewDto {
  key: string;
  value: string;
}

export interface SectorReconfigurationPreviewDto {
  currentSegment: string | null;
  currentDomain: string | null;
  targetSegment: string | null;
  targetDomain: string | null;
  users: UserModuleDiffDto[];
  templates: TemplatePreviewDto[];
  settings: SettingPreviewDto[];
  warnings: string[];
}

export interface StepResultDto {
  step: string;
  success: boolean;
  error?: string | null;
}

export interface SectorReconfigurationApplyResultDto {
  steps: StepResultDto[];
  effectiveChange: SectorReconfigurationPreviewDto;
}
