/**
 * Phase 2 (WP-F6) — DTOs pour le moteur de règles sectorielles en base.
 *
 * Contrats ALIGNÉS sur le backend (WP-B5/B7/B9) :
 * - `/api/platform/sector-rules/*` : CRUD admin GUID + dump complet `SectorRuleSetAdminDto`.
 * - `/api/platform/tenants/{id}/sector-configuration/preview|apply` : reconfiguration tenant.
 *
 * Les champs sont exactement les noms PascalCase du backend sérialisés en camelCase par
 * System.Text.Json (ex: `LabelFr` → `labelFr`). Les ids sont des GUID (chaînes). Les
 * `ruleKind` sont des chaînes `"SegmentBase"` | `"DomainOverlay"` (pas d'entier). La
 * dépendance utilise `requiredModuleId` (contrat backend, cf. commit de fixation web).
 */

// ============================================================================
// Segments
// ============================================================================

export interface SectorSegmentDto {
  id: string;
  code: string;
  labelFr: string;
  descriptionFr: string;
  iconKey: string;
  sortOrder: number;
  /** Nom d'entrepôt par défaut suggéré pour ce segment (peut être vide). */
  defaultWarehouseName?: string | null;
  isActive: boolean;
}

export interface CreateSectorSegmentRequest {
  code: string;
  labelFr: string;
  descriptionFr: string;
  iconKey: string;
  sortOrder: number;
  defaultWarehouseName?: string | null;
}

export interface UpdateSectorSegmentRequest {
  labelFr: string;
  descriptionFr: string;
  iconKey: string;
  sortOrder: number;
  defaultWarehouseName?: string | null;
}

// ============================================================================
// Domaines
// ============================================================================

export interface SectorDomainDto {
  id: string;
  code: string;
  labelFr: string;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateSectorDomainRequest {
  code: string;
  labelFr: string;
  sortOrder: number;
}

export interface UpdateSectorDomainRequest {
  labelFr: string;
  sortOrder: number;
}

// ============================================================================
// Associations segment ↔ domaine (GUIDs)
// ============================================================================

export interface SectorSegmentDomainDto {
  id: string;
  segmentId: string;
  domainId: string;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateSectorSegmentDomainRequest {
  segmentId: string;
  domainId: string;
  sortOrder: number;
}

export interface UpdateSectorSegmentDomainRequest {
  sortOrder: number;
}

// ============================================================================
// Règles de modules (base segment + surcouche domaine)
// ============================================================================

/** `RuleKind` backend : chaîne `"SegmentBase"` (rattachée à un segment) ou `"DomainOverlay"` (surcouche d'un domaine). */
export type SectorModuleRuleKind = 'SegmentBase' | 'DomainOverlay';

export interface SectorModuleRuleDto {
  id: string;
  ruleKind: SectorModuleRuleKind;
  segmentId: string | null;
  domainId: string | null;
  moduleId: number;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateSectorModuleRuleRequest {
  ruleKind: SectorModuleRuleKind;
  segmentId?: string | null;
  domainId?: string | null;
  moduleId: number;
  sortOrder: number;
}

export interface UpdateSectorModuleRuleRequest {
  sortOrder: number;
}

// ============================================================================
// Dépendances entre modules
// ============================================================================

export interface ModuleDependencyDto {
  id: string;
  moduleId: number;
  requiredModuleId: number;
  isActive: boolean;
}

export interface CreateModuleDependencyRequest {
  moduleId: number;
  requiredModuleId: number;
}

// ============================================================================
// Paramètres par défaut
// ============================================================================

export type SectorSettingValueType = 'string' | 'int' | 'bool' | 'json';

export interface SectorDefaultSettingDto {
  id: string;
  segmentCode: string | null;
  domainCode: string | null;
  settingKey: string;
  settingValue: string;
  valueType: SectorSettingValueType;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateSectorDefaultSettingRequest {
  segmentCode?: string | null;
  domainCode?: string | null;
  settingKey: string;
  settingValue: string;
  valueType?: SectorSettingValueType;
  sortOrder: number;
}

export interface UpdateSectorDefaultSettingRequest {
  settingValue: string;
  valueType?: SectorSettingValueType;
  sortOrder: number;
}

// ============================================================================
// Modèles de données (types de documents, plan comptable, paramètres)
// ============================================================================

export type SectorDataTemplateItemKind = 'document-numbering-scheme' | 'chart-account' | 'setting';

export interface SectorDataTemplateItemDto {
  id: string;
  itemKind: string;
  /** JSON brut du payload — contrat exact dépend de `itemKind` (voir backend WP-B6). */
  payloadJson: string;
  sortOrder: number;
  isActive: boolean;
}

export interface SectorDataTemplateItemRequest {
  itemKind: SectorDataTemplateItemKind | string;
  payloadJson: string;
  sortOrder: number;
}

export interface SectorDataTemplateDto {
  id: string;
  code: string;
  segmentCode: string | null;
  domainCode: string | null;
  labelFr: string;
  descriptionFr: string | null;
  version: number;
  sortOrder: number;
  isActive: boolean;
  items: SectorDataTemplateItemDto[];
}

export interface CreateSectorDataTemplateRequest {
  code: string;
  segmentCode?: string | null;
  domainCode?: string | null;
  labelFr: string;
  descriptionFr?: string | null;
  version?: number;
  sortOrder: number;
  items: SectorDataTemplateItemRequest[];
}

export interface UpdateSectorDataTemplateRequest {
  labelFr: string;
  descriptionFr?: string | null;
  version?: number;
  sortOrder: number;
  items: SectorDataTemplateItemRequest[];
}

// ============================================================================
// Bootstrap / dump complet
// ============================================================================

export interface SectorRuleSetAdminDto {
  /** Stamp de version des règles en base. */
  version: number;
  segments: SectorSegmentDto[];
  domains: SectorDomainDto[];
  segmentDomains: SectorSegmentDomainDto[];
  moduleRules: SectorModuleRuleDto[];
  moduleDependencies: ModuleDependencyDto[];
  settings: SectorDefaultSettingDto[];
  templates: SectorDataTemplateDto[];
}

export interface SectorRuleSeedResultDto {
  inserted: number;
  updated: number;
  skippedExisting: number;
  newVersion: number;
  forced: boolean;
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

/** Issue de `TemplateItemOutcome` backend (WP-B6) : évaluation d'un item de modèle. */
export interface TemplateItemOutcomeDto {
  templateCode: string;
  itemKind: string;
  outcome: string;
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
