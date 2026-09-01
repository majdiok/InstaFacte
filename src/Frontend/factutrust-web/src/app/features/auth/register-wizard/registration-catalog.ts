import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, of, retry, timer } from 'rxjs';
import { environment } from '@environments/environment';
import { AppModule, APP_MODULE_OPTIONS } from '@core/models/app-module';

/**
 * Sector-aware registration catalog.
 *
 * Wire format is locked (plan §3, decision C1): segment/domain are lowercase-kebab
 * string codes and `enabledModules` is a plain `number[]` of `AppModule` ids — this
 * mirrors the backend `SectorConfigurationCatalog` exactly so registration requires
 * no network round-trip between wizard steps.
 *
 * Phase 2 (plan WP-F1): `RegistrationCatalogService` fetches `GET
 * /api/public/sector-catalog` once per SPA session and serves the response through
 * the *same* synchronous public API used since Phase 1 (`segments`, `domains`,
 * `recommendedModules()`, …) — step components need zero changes. A 404 (backend
 * kill-switch off) or any network/5xx error falls back silently to the bundled
 * static catalog below, which remains the permanent, byte-identical Phase 1 fallback.
 */

export type CompanySegmentCode =
  | 'entreprise'
  | 'commerce'
  | 'services'
  | 'btp-construction'
  | 'association'
  | 'etablissement-educatif';

export type BusinessDomainCode =
  | 'technologie-informatique'
  | 'alimentation-agroalimentaire'
  | 'sante-paramedical'
  | 'textile-habillement'
  | 'transport-logistique'
  | 'immobilier'
  | 'energie-environnement'
  | 'communication-marketing'
  | 'artisanat'
  | 'autre';

export interface SegmentOption {
  code: string;
  label: string;
  subtitle: string;
  icon: string;
  tone: string;
}

export interface DomainOption {
  code: string;
  label: string;
  icon: string;
  tone: string;
}

export interface ModuleCatalogEntry {
  id: AppModule;
  label: string;
}

/**
 * Generic `ApiResponse<T>` envelope used by every backend controller
 * (`FactuTrust.Application.DTOs.ApiResponse<T>`, camelCase JSON).
 */
export interface ApiResponse<T> {
  success: boolean;
  data?: T | null;
  message?: string | null;
  code?: string | null;
  errors?: string[];
}

/** Wire shape of `GET /api/public/sector-catalog` (locked contract, additive-only). */
export interface RemoteSectorSegmentDto {
  code: string;
  labelFr: string;
  descriptionFr: string;
  iconKey: string;
  sortOrder: number;
  coreModuleIds: number[];
  recommendedModuleIds: number[];
  defaultWarehouseName?: string | null;
  /** Ordered list of domain codes available for this segment (Phase 2 addition). */
  domainCodes: string[];
}

export interface RemoteSectorDomainDto {
  code: string;
  labelFr: string;
  sortOrder: number;
  additionalModuleIds: number[];
}

export interface RemoteSectorModuleDto {
  id: number;
  code: string;
  labelFr: string;
  isCore: boolean;
}

/**
 * Backend `SectorModuleDependencyDto`: `requiredModuleId` is the prerequisite — enabling
 * `moduleId` auto-pulls `requiredModuleId`. (The backend emits `requiredModuleId`, not
 * `requiresModuleId`.)
 */
export interface RemoteModuleDependencyDto {
  moduleId: number;
  requiredModuleId: number;
}

/**
 * Backend `SuggestedTaxRegimeDto` (plan §3.1) — admin-editable segment → tax regime
 * suggestion, surfaced with a French explanatory note. Additive/optional: absent on
 * catalogs served before this field ships server-side (fallback catalog never has it).
 */
export interface RemoteSuggestedTaxRegimeDto {
  segmentCode: string;
  regime: number;
  noteFr: string;
}

export interface SectorCatalogDto {
  segments: RemoteSectorSegmentDto[];
  domains: RemoteSectorDomainDto[];
  modules: RemoteSectorModuleDto[];
  moduleDependencies: RemoteModuleDependencyDto[];
  /** Optional (plan §3.1) — absent on older/static payloads. */
  suggestedTaxRegimes?: RemoteSuggestedTaxRegimeDto[];
}

export type CatalogLoadState = 'idle' | 'loading' | 'remote' | 'fallback';

/** Segments — order matches the reference design (3×2 grid). */
export const SEGMENT_OPTIONS: readonly SegmentOption[] = [
  {
    code: 'entreprise',
    label: 'Entreprise',
    subtitle: 'Sociétés commerciales et de services aux entreprises',
    icon: 'pi-building',
    tone: 'tone-blue'
  },
  {
    code: 'commerce',
    label: 'Commerce',
    subtitle: 'Négoce et distribution',
    icon: 'pi-shopping-cart',
    tone: 'tone-green'
  },
  {
    code: 'services',
    label: 'Prestations de services',
    subtitle: 'Services et conseils',
    icon: 'pi-briefcase',
    tone: 'tone-orange'
  },
  {
    code: 'btp-construction',
    label: 'BTP & Construction',
    subtitle: 'Bâtiment et travaux publics',
    icon: 'pi-hammer',
    tone: 'tone-yellow'
  },
  {
    code: 'association',
    label: 'Association',
    subtitle: 'Organismes à but non lucratif',
    icon: 'pi-heart',
    tone: 'tone-pink'
  },
  {
    code: 'etablissement-educatif',
    label: 'Établissement éducatif',
    subtitle: 'Écoles, universités, centres de formation',
    icon: 'pi-graduation-cap',
    tone: 'tone-indigo'
  }
] as const;

/** Domains — order matches the reference design (5×2 grid). */
export const DOMAIN_OPTIONS: readonly DomainOption[] = [
  { code: 'technologie-informatique', label: 'Technologie & Informatique', icon: 'pi-desktop', tone: 'tone-blue' },
  { code: 'alimentation-agroalimentaire', label: 'Alimentation & Agroalimentaire', icon: 'pi-apple', tone: 'tone-green' },
  { code: 'sante-paramedical', label: 'Santé & Paramédical', icon: 'pi-heart-fill', tone: 'tone-purple' },
  { code: 'textile-habillement', label: 'Textile & Habillement', icon: 'pi-tag', tone: 'tone-blue' },
  { code: 'transport-logistique', label: 'Transport & Logistique', icon: 'pi-truck', tone: 'tone-orange' },
  { code: 'immobilier', label: 'Immobilier', icon: 'pi-home', tone: 'tone-indigo' },
  { code: 'energie-environnement', label: 'Énergie & Environnement', icon: 'pi-bolt', tone: 'tone-teal' },
  { code: 'communication-marketing', label: 'Communication & Marketing', icon: 'pi-megaphone', tone: 'tone-pink' },
  { code: 'artisanat', label: 'Artisanat', icon: 'pi-palette', tone: 'tone-yellow' },
  { code: 'autre', label: 'Autre domaine', icon: 'pi-ellipsis-h', tone: 'tone-gray' }
] as const;

/** Core modules — always on, never deselectable, never sent as "optional". */
export const CORE_MODULE_IDS: readonly AppModule[] = [
  AppModule.Administration,
  AppModule.Clients,
  AppModule.Products,
  AppModule.Sales,
  AppModule.Treasury,
  AppModule.Reports
];

const SEGMENT_RECOMMENDED_MODULES: Record<CompanySegmentCode, readonly AppModule[]> = {
  'entreprise': [AppModule.Purchases, AppModule.Stock, AppModule.Accounting, AppModule.CRM, AppModule.Fiscal],
  'commerce': [AppModule.Purchases, AppModule.Stock, AppModule.Fiscal],
  'services': [AppModule.CRM, AppModule.Projects, AppModule.RecurringContracts, AppModule.Fiscal],
  'btp-construction': [AppModule.Purchases, AppModule.Stock, AppModule.Projects, AppModule.Fiscal],
  'association': [AppModule.Accounting, AppModule.Fiscal],
  'etablissement-educatif': [AppModule.RecurringContracts, AppModule.Accounting, AppModule.Fiscal]
};

const DOMAIN_MODULE_OVERLAY: Record<BusinessDomainCode, readonly AppModule[]> = {
  'technologie-informatique': [AppModule.Projects, AppModule.RecurringContracts],
  'alimentation-agroalimentaire': [AppModule.Stock, AppModule.Purchases],
  'sante-paramedical': [AppModule.CRM],
  'textile-habillement': [AppModule.Stock],
  'transport-logistique': [AppModule.Stock],
  'immobilier': [],
  'energie-environnement': [],
  'communication-marketing': [AppModule.CRM],
  'artisanat': [],
  'autre': []
};

const SEGMENT_DEFAULT_WAREHOUSE_NAME: Record<CompanySegmentCode, string> = {
  'entreprise': 'Entrepôt Principal',
  'commerce': 'Magasin principal',
  'services': 'Entrepôt Principal',
  'btp-construction': 'Dépôt chantier',
  'association': 'Entrepôt Principal',
  'etablissement-educatif': 'Entrepôt Principal'
};

/**
 * Fallback segment→domain matrix (plan §3.1) — exact copy of the backend
 * `SectorConfigurationCatalog` matrix (`SegmentDefinition.AllowedDomainCodes`). Used by
 * `domainsForSegment()`'s fallback path so the "N domaines adaptés à votre segment" chip is
 * honest even when the remote catalog is unavailable. `autre` is always included as the
 * universal safety-net fallback.
 */
export const SEGMENT_ALLOWED_DOMAINS: Record<CompanySegmentCode, readonly BusinessDomainCode[]> = {
  'entreprise': [
    'technologie-informatique',
    'alimentation-agroalimentaire',
    'sante-paramedical',
    'textile-habillement',
    'transport-logistique',
    'immobilier',
    'energie-environnement',
    'communication-marketing',
    'artisanat',
    'autre'
  ],
  'commerce': [
    'alimentation-agroalimentaire',
    'textile-habillement',
    'technologie-informatique',
    'sante-paramedical',
    'artisanat',
    'autre'
  ],
  'services': [
    'technologie-informatique',
    'communication-marketing',
    'sante-paramedical',
    'transport-logistique',
    'immobilier',
    'autre'
  ],
  'btp-construction': [
    'immobilier',
    'energie-environnement',
    'artisanat',
    'autre'
  ],
  'association': [
    'sante-paramedical',
    'energie-environnement',
    'communication-marketing',
    'artisanat',
    'autre'
  ],
  'etablissement-educatif': [
    'technologie-informatique',
    'sante-paramedical',
    'artisanat',
    'communication-marketing',
    'autre'
  ]
};

/** Fallback tone palette for segment/domain codes the static catalog doesn't know (backoffice-added). */
const TONE_PALETTE: readonly string[] = [
  'tone-blue', 'tone-green', 'tone-orange', 'tone-yellow', 'tone-pink',
  'tone-indigo', 'tone-purple', 'tone-teal', 'tone-gray'
];

/** All `AppModule` numeric ids known to the frontend catalog. */
function allModuleIds(): AppModule[] {
  return APP_MODULE_OPTIONS.map(o => o.value);
}

function isKnownSegment(code: string | null | undefined): code is CompanySegmentCode {
  return !!code && Object.prototype.hasOwnProperty.call(SEGMENT_RECOMMENDED_MODULES, code);
}

function isKnownDomain(code: string | null | undefined): code is BusinessDomainCode {
  return !!code && Object.prototype.hasOwnProperty.call(DOMAIN_MODULE_OVERLAY, code);
}

/**
 * Pure function mirroring the backend `SectorConfigurationCatalog.Resolve` merge
 * (segment base ∪ domain overlay ∪ core). Unknown/absent segment ⇒ core modules only.
 *
 * This function is pinned by `registration-catalog.spec.ts` and drives the static
 * fallback — it must stay byte-identical to Phase 1 behavior.
 */
export function recommendedModulesFor(
  segment: string | null | undefined,
  domain: string | null | undefined
): AppModule[] {
  const set = new Set<AppModule>(CORE_MODULE_IDS);

  if (isKnownSegment(segment)) {
    for (const id of SEGMENT_RECOMMENDED_MODULES[segment]) {
      set.add(id);
    }
  }

  if (isKnownDomain(domain)) {
    for (const id of DOMAIN_MODULE_OVERLAY[domain]) {
      set.add(id);
    }
  }

  return Array.from(set).sort((a, b) => a - b);
}

/** Every module that is neither core, recommended for the profile, nor Honoraires (firm-native, never offered). */
export function optionalModulesFor(
  segment: string | null | undefined,
  domain: string | null | undefined
): AppModule[] {
  const recommended = new Set(recommendedModulesFor(segment, domain));
  return allModuleIds()
    .filter(id => id !== AppModule.Honoraires && !recommended.has(id))
    .sort((a, b) => a - b);
}

export function defaultWarehouseNameFor(segment: string | null | undefined): string | undefined {
  return isKnownSegment(segment) ? SEGMENT_DEFAULT_WAREHOUSE_NAME[segment] : undefined;
}

function toneForIndex(index: number): string {
  const safe = ((index % TONE_PALETTE.length) + TONE_PALETTE.length) % TONE_PALETTE.length;
  return TONE_PALETTE[safe];
}

/**
 * Best-effort mapping from the backend `iconKey` (e.g. `"shopping-cart"`, `"hard-hat"`)
 * to a PrimeIcons class. Used only for segment codes unknown to the bundled static
 * catalog (backoffice-added segments) — known codes keep their curated Phase 1
 * icon/tone below for exact visual continuity with the design mockups, since the
 * locked wire contract carries no `tone` field and the backend's `iconKey` values
 * don't 1:1 match the frontend's curated icon choices for the original 6 segments.
 */
function iconKeyToPiClass(iconKey: string | null | undefined): string {
  if (!iconKey) return 'pi-sitemap';
  return iconKey.startsWith('pi-') ? iconKey : `pi-${iconKey}`;
}

/** Injectable seam — HTTP-backed catalog with static fallback (plan WP-F1). */
@Injectable({ providedIn: 'root' })
export class RegistrationCatalogService {
  private readonly http = inject(HttpClient);

  /** Non-null once a valid remote payload has been received; null in idle/loading/fallback. */
  private readonly remoteCatalog = signal<SectorCatalogDto | null>(null);

  /** Cached set of every `AppModule` numeric id known to the frontend catalog — used to filter
   *  unknown ids out of the remote payload (defensive; the backend only sends known ids). */
  private readonly knownModuleIds = new Set<AppModule>(allModuleIds());

  private isValidModuleId(id: number): id is AppModule {
    return this.knownModuleIds.has(id as AppModule);
  }

  /** Observable load lifecycle — see `load()`. */
  readonly loadState = signal<CatalogLoadState>('idle');

  /** True once a remote catalog has replaced the static one (used to gate domain filtering UI). */
  get isRemote(): boolean {
    return this.remoteCatalog() !== null;
  }

  get segments(): readonly SegmentOption[] {
    const remote = this.remoteCatalog();
    if (!remote) return SEGMENT_OPTIONS;
    return remote.segments
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(s => this.toSegmentOption(s));
  }

  get domains(): readonly DomainOption[] {
    const remote = this.remoteCatalog();
    if (!remote) return DOMAIN_OPTIONS;
    return remote.domains
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(d => this.toDomainOption(d));
  }

  get coreModuleIds(): readonly AppModule[] {
    const remote = this.remoteCatalog();
    if (!remote || remote.segments.length === 0) return CORE_MODULE_IDS;
    const ids = new Set<AppModule>();
    for (const s of remote.segments) {
      for (const id of s.coreModuleIds) {
        if (this.isValidModuleId(id)) ids.add(id);
      }
    }
    return ids.size > 0 ? Array.from(ids).sort((a, b) => a - b) : CORE_MODULE_IDS;
  }

  get modules(): readonly ModuleCatalogEntry[] {
    const remote = this.remoteCatalog();
    if (!remote) return APP_MODULE_OPTIONS.map(o => ({ id: o.value, label: o.label }));
    return remote.modules
      .filter(m => this.isValidModuleId(m.id))
      .map(m => ({ id: m.id, label: m.labelFr }));
  }

  segmentLabel(code: string | null | undefined): string {
    return this.segments.find(s => s.code === code)?.label ?? '';
  }

  domainLabel(code: string | null | undefined): string {
    return this.domains.find(d => d.code === code)?.label ?? '';
  }

  /**
   * Suggested tax regime for a segment (plan §3.1). Returns `undefined` whenever the
   * remote catalog hasn't loaded yet, doesn't carry `suggestedTaxRegimes` (older
   * backend, or the static fallback catalog which never has this field), or has no
   * entry for `segment` — the wizard then behaves exactly as before (no suggestion).
   */
  suggestedTaxRegimeFor(segment: string | null | undefined): RemoteSuggestedTaxRegimeDto | undefined {
    if (!segment) return undefined;
    const remote = this.remoteCatalog();
    if (!remote || !Array.isArray(remote.suggestedTaxRegimes)) return undefined;
    return remote.suggestedTaxRegimes.find(r => r.segmentCode === segment);
  }

  moduleLabel(id: AppModule): string {
    return this.modules.find(m => m.id === id)?.label ?? String(id);
  }

  isCoreModule(id: AppModule): boolean {
    return this.coreModuleIds.includes(id);
  }

  /**
   * Segment-filtered, ordered domain list (plan WP-F2, §3.3). No/unknown segment ⇒ `[]`
   * in both remote and fallback modes. Remote mode: known segment ⇒ its ordered
   * `domainCodes`. Fallback mode (remote catalog unavailable): known segment ⇒ the
   * static `SEGMENT_ALLOWED_DOMAINS` matrix (plan §3.1), mirroring the backend
   * `SectorConfigurationCatalog`. In both modes, `autre` is appended when absent (never
   * invalidates a user who already picked "Autre domaine").
   */
  domainsForSegment(segment: string | null | undefined): readonly DomainOption[] {
    const remote = this.remoteCatalog();
    const byCode = new Map(this.domains.map(d => [d.code, d] as const));

    let codes: readonly string[] | undefined;
    if (remote) {
      if (!segment) return [];
      const seg = remote.segments.find(s => s.code === segment);
      codes = seg?.domainCodes;
    } else {
      if (!isKnownSegment(segment)) return [];
      codes = SEGMENT_ALLOWED_DOMAINS[segment];
    }
    if (!codes) {
      return [];
    }

    const ordered: DomainOption[] = [];
    for (const code of codes) {
      const opt = byCode.get(code);
      if (opt) ordered.push(opt);
    }
    if (!ordered.some(d => d.code === 'autre')) {
      const autre = byCode.get('autre');
      if (autre) ordered.push(autre);
    }
    return ordered;
  }

  recommendedModules(segment: string | null | undefined, domain: string | null | undefined): AppModule[] {
    const remote = this.remoteCatalog();
    if (!remote) {
      return recommendedModulesFor(segment, domain);
    }

    const coreSet = new Set(this.coreModuleIds);
    const set = new Set<AppModule>();

    const seg = remote.segments.find(s => s.code === segment);
    if (seg) {
      for (const id of seg.recommendedModuleIds) {
        if (this.isValidModuleId(id) && !coreSet.has(id)) set.add(id);
      }
    }

    const dom = remote.domains.find(d => d.code === domain);
    if (dom) {
      for (const id of dom.additionalModuleIds) {
        if (this.isValidModuleId(id) && !coreSet.has(id)) set.add(id);
      }
    }

    // Defensive closure (plan WP-F3): a recommended module never silently omits one of
    // its hard dependencies. No-op when `moduleDependencies` is empty.
    this.closeDependencies(set);

    return Array.from(set)
      .filter(m => m !== AppModule.Honoraires)
      .sort((a, b) => a - b);
  }

  optionalModules(segment: string | null | undefined, domain: string | null | undefined): AppModule[] {
    const remote = this.remoteCatalog();
    if (!remote) {
      return optionalModulesFor(segment, domain);
    }
    const recommended = new Set(this.recommendedModules(segment, domain));
    const coreSet = new Set(this.coreModuleIds);
    return this.modules
      .map(m => m.id)
      .filter(id => id !== AppModule.Honoraires && !recommended.has(id) && !coreSet.has(id))
      .sort((a, b) => a - b);
  }

  defaultWarehouseName(segment: string | null | undefined): string | undefined {
    const remote = this.remoteCatalog();
    if (!remote) {
      return defaultWarehouseNameFor(segment);
    }
    const seg = remote.segments.find(s => s.code === segment);
    return seg?.defaultWarehouseName ?? undefined;
  }

  /**
   * Transitive closure of "requires" edges for `moduleId` — every module that must be
   * ON for `moduleId` to work (plan WP-F3). Empty in static/fallback mode. Guards
   * against cycles with a visited set even though the backend rejects them.
   */
  requiredBy(moduleId: AppModule): AppModule[] {
    const edges = this.dependencyEdges;
    if (edges.length === 0) return [];

    const result: AppModule[] = [];
    const visited = new Set<AppModule>([moduleId]);
    const queue: AppModule[] = [moduleId];

    while (queue.length > 0) {
      const current = queue.shift() as AppModule;
      for (const edge of edges) {
        if (edge.moduleId === current && !visited.has(edge.requiresModuleId)) {
          visited.add(edge.requiresModuleId);
          result.push(edge.requiresModuleId);
          queue.push(edge.requiresModuleId);
        }
      }
    }

    return result;
  }

  /** Among `enabled`, the modules that (transitively) require `moduleId` (plan WP-F3). */
  dependentsOf(moduleId: AppModule, enabled: readonly AppModule[]): AppModule[] {
    if (this.dependencyEdges.length === 0) return [];
    return enabled.filter(id => id !== moduleId && this.requiredBy(id).includes(moduleId));
  }

  /** Number of retries attempted after the initial request before falling back (plan 2.1 — 3 attempts total). */
  private static readonly RETRY_COUNT = 2;
  /** Backoff base delay (ms); attempt N waits `RETRY_BACKOFF_MS_BASE * N` before retrying. */
  private static readonly RETRY_BACKOFF_MS_BASE = 1500;

  /**
   * True once the catalog has fallen back to the bundled static table after exhausting
   * its retries (plan 2.1). Drives the discreet "recommandations par défaut" wizard
   * indicator — kept separate from `loadState() === 'fallback'` so a future caller
   * (telemetry dashboard, support tooling) can query it independently of the load
   * lifecycle enum.
   */
  readonly usedStaticFallback = signal(false);

  /**
   * Fetches the live sector catalog once per SPA session and caches it in-memory
   * (plan WP-F1). No-op when already loading/loaded, or when the frontend kill-switch
   * `featureFlags.sectorCatalogHttp` is off. Never surfaces a blocking error to the
   * caller — registration must never be blocked by this endpoint — but a failure after
   * retries is now "loud": logged to the console/telemetry sink and reflected through
   * `usedStaticFallback()` so the wizard can show a discreet indicator (plan 2.1).
   * Root-provided means an admin editing rules mid-session won't be reflected without a
   * hard reload; acceptable for the registration flow.
   */
  load(): void {
    if (this.loadState() !== 'idle') return;
    if (!environment.featureFlags.sectorCatalogHttp) return;

    this.loadState.set('loading');

    this.http
      .get<ApiResponse<SectorCatalogDto>>(`${environment.apiUrl}/public/sector-catalog`)
      .pipe(
        // Plan 2.1: 3 attempts total (initial + 2 retries) with linear backoff before
        // falling back to the static catalog — was a single retry (2 attempts) in Phase 1.
        retry({
          count: RegistrationCatalogService.RETRY_COUNT,
          delay: (_error, retryCount) => timer(RegistrationCatalogService.RETRY_BACKOFF_MS_BASE * retryCount)
        }),
        catchError(() => of(null))
      )
      .subscribe(res => {
        const data = res?.data;
        if (!res || !res.success || !data || !Array.isArray(data.segments) || data.segments.length === 0) {
          this.reportStaticFallback(res);
          return;
        }
        this.remoteCatalog.set(data);
        this.loadState.set('remote');
      });
  }

  /**
   * Loud fallback reporting (plan 2.1 — "bruyant" instead of silent): console telemetry
   * with enough context to diagnose an endpoint outage, plus the `usedStaticFallback`
   * signal the wizard reads to show its discreet indicator.
   */
  private reportStaticFallback(res: ApiResponse<SectorCatalogDto> | null): void {
    console.warn(
      '[RegistrationCatalogService] Sector catalog unavailable or empty after ' +
        `${RegistrationCatalogService.RETRY_COUNT + 1} attempt(s) — using static fallback catalog.`,
      { lastResponseSuccess: res?.success ?? null, hasData: !!res?.data }
    );
    this.usedStaticFallback.set(true);
    this.loadState.set('fallback');
  }

  private get dependencyEdges(): readonly { moduleId: AppModule; requiresModuleId: AppModule }[] {
    const remote = this.remoteCatalog();
    if (!remote || !Array.isArray(remote.moduleDependencies)) return [];
    return remote.moduleDependencies
      .filter(d => this.isValidModuleId(d.moduleId) && this.isValidModuleId(d.requiredModuleId))
      .map(d => ({ moduleId: d.moduleId, requiresModuleId: d.requiredModuleId }));
  }

  private closeDependencies(set: Set<AppModule>): void {
    for (const id of Array.from(set)) {
      for (const dep of this.requiredBy(id)) {
        set.add(dep);
      }
    }
  }

  private toSegmentOption(seg: RemoteSectorSegmentDto): SegmentOption {
    const staticMatch = SEGMENT_OPTIONS.find(s => s.code === seg.code);
    return {
      code: seg.code,
      label: seg.labelFr,
      subtitle: seg.descriptionFr,
      icon: staticMatch?.icon ?? iconKeyToPiClass(seg.iconKey),
      tone: staticMatch?.tone ?? toneForIndex(seg.sortOrder)
    };
  }

  private toDomainOption(dom: RemoteSectorDomainDto): DomainOption {
    const staticMatch = DOMAIN_OPTIONS.find(d => d.code === dom.code);
    return {
      code: dom.code,
      label: dom.labelFr,
      icon: staticMatch?.icon ?? 'pi-sitemap',
      tone: staticMatch?.tone ?? toneForIndex(dom.sortOrder)
    };
  }
}
