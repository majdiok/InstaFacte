import { Injectable } from '@angular/core';
import { AppModule, APP_MODULE_OPTIONS } from '@core/models/app-module';

/**
 * Sector-aware registration catalog (Phase 1: declarative static data).
 *
 * Wire format is locked (plan §3, decision C1): segment/domain are lowercase-kebab
 * string codes and `enabledModules` is a plain `number[]` of `AppModule` ids — this
 * mirrors the backend `SectorConfigurationCatalog` exactly so Phase 1 requires no
 * network round-trip between wizard steps.
 *
 * Phase 2 seam (decision C8): this file/service is the injection point that will be
 * swapped for an HTTP-backed `RegistrationCatalogService` reading `GET
 * /api/public/sector-catalog` without any change to the components that consume it.
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
  code: CompanySegmentCode;
  label: string;
  subtitle: string;
  icon: string;
  tone: string;
}

export interface DomainOption {
  code: BusinessDomainCode;
  label: string;
  icon: string;
  tone: string;
}

export interface ModuleCatalogEntry {
  id: AppModule;
  label: string;
}

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

/** Injectable seam — Phase 2 will replace the body with an HTTP call to `/api/public/sector-catalog`. */
@Injectable({ providedIn: 'root' })
export class RegistrationCatalogService {
  readonly segments: readonly SegmentOption[] = SEGMENT_OPTIONS;
  readonly domains: readonly DomainOption[] = DOMAIN_OPTIONS;
  readonly coreModuleIds: readonly AppModule[] = CORE_MODULE_IDS;
  readonly modules: readonly ModuleCatalogEntry[] = APP_MODULE_OPTIONS.map(o => ({ id: o.value, label: o.label }));

  segmentLabel(code: string | null | undefined): string {
    return this.segments.find(s => s.code === code)?.label ?? '';
  }

  domainLabel(code: string | null | undefined): string {
    return this.domains.find(d => d.code === code)?.label ?? '';
  }

  moduleLabel(id: AppModule): string {
    return this.modules.find(m => m.id === id)?.label ?? String(id);
  }

  isCoreModule(id: AppModule): boolean {
    return this.coreModuleIds.includes(id);
  }

  recommendedModules(segment: string | null | undefined, domain: string | null | undefined): AppModule[] {
    return recommendedModulesFor(segment, domain);
  }

  optionalModules(segment: string | null | undefined, domain: string | null | undefined): AppModule[] {
    return optionalModulesFor(segment, domain);
  }

  defaultWarehouseName(segment: string | null | undefined): string | undefined {
    return defaultWarehouseNameFor(segment);
  }
}
