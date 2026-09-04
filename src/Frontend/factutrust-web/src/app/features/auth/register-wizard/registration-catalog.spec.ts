import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AppModule } from '@core/models/app-module';
import { environment } from '@environments/environment';
import {
  ApiResponse,
  CORE_MODULE_IDS,
  DOMAIN_OPTIONS,
  PREMIUM_MODULE_IDS,
  RegistrationCatalogService,
  SectorCatalogDto,
  SEGMENT_OPTIONS,
  defaultWarehouseNameFor,
  optionalModulesFor,
  recommendedModulesFor,
  applyProfileOverlay,
  EMPTY_PROFILE_ANSWERS
} from './registration-catalog';

// Review S5: the exact domain-code list for the 'commerce' segment (SEGMENT_ALLOWED_DOMAINS
// fallback matrix) is asserted identically in three separate specs below (static-matrix filter,
// per-segment matrix pin, 404-fallback). Hoisted once here so those three assertions can't drift
// out of sync with each other while still each independently pinning the exact contract.
const COMMERCE_FALLBACK_DOMAINS = [
  'alimentation-agroalimentaire',
  'textile-habillement',
  'technologie-informatique',
  'sante-paramedical',
  'artisanat',
  'autre'
];

describe('registration-catalog', () => {
  it('exposes exactly 6 segments and 10 domains (plan §5/§3 C1)', () => {
    expect(SEGMENT_OPTIONS.length).toBe(6);
    expect(DOMAIN_OPTIONS.length).toBe(10);
  });

  it('uses lowercase-kebab string codes (plan C1)', () => {
    for (const s of SEGMENT_OPTIONS) {
      expect(s.code).toMatch(/^[a-z]+(-[a-z]+)*$/);
    }
    for (const d of DOMAIN_OPTIONS) {
      expect(d.code).toMatch(/^[a-z]+(-[a-z]+)*$/);
    }
  });

  describe('recommendedModulesFor', () => {
    it('returns only core modules for an unknown/absent segment', () => {
      expect(recommendedModulesFor(null, null)).toEqual([...CORE_MODULE_IDS].sort((a, b) => a - b));
      expect(recommendedModulesFor('not-a-segment', null)).toEqual([...CORE_MODULE_IDS].sort((a, b) => a - b));
    });

    it('always includes every core module for every known segment', () => {
      for (const segment of SEGMENT_OPTIONS) {
        const result = recommendedModulesFor(segment.code, null);
        for (const core of CORE_MODULE_IDS) {
          expect(result).toContain(core);
        }
      }
    });

    it('never recommends Honoraires (firm-native module)', () => {
      for (const segment of SEGMENT_OPTIONS) {
        for (const domain of DOMAIN_OPTIONS) {
          expect(recommendedModulesFor(segment.code, domain.code)).not.toContain(AppModule.Honoraires);
        }
      }
    });

    it('merges segment base with domain overlay (entreprise + technologie-informatique)', () => {
      const result = recommendedModulesFor('entreprise', 'technologie-informatique');
      expect(result).toEqual(
        jasmine.arrayContaining([
          AppModule.Administration,
          AppModule.Clients,
          AppModule.Products,
          AppModule.Sales,
          AppModule.Treasury,
          AppModule.Reports,
          AppModule.Purchases,
          AppModule.Stock,
          AppModule.Accounting,
          AppModule.CRM,
          AppModule.Fiscal,
          AppModule.Projects,
          AppModule.RecurringContracts
        ])
      );
    });

    it('applies the commerce base matrix (Purchases, Stock, Fiscal)', () => {
      const result = recommendedModulesFor('commerce', 'autre');
      expect(result).toEqual(
        jasmine.arrayContaining([AppModule.Purchases, AppModule.Stock, AppModule.Fiscal])
      );
    });

    it('returns a sorted array with no duplicates', () => {
      const result = recommendedModulesFor('services', 'sante-paramedical');
      const sorted = [...result].sort((a, b) => a - b);
      expect(result).toEqual(sorted);
      expect(new Set(result).size).toBe(result.length);
    });
  });

  describe('optionalModulesFor', () => {
    it('is disjoint from the recommended set and never contains Honoraires', () => {
      for (const segment of SEGMENT_OPTIONS) {
        for (const domain of DOMAIN_OPTIONS) {
          const recommended = new Set(recommendedModulesFor(segment.code, domain.code));
          const optional = optionalModulesFor(segment.code, domain.code);
          expect(optional).not.toContain(AppModule.Honoraires);
          for (const id of optional) {
            expect(recommended.has(id)).toBe(false);
          }
        }
      }
    });
  });

  describe('PREMIUM_MODULE_IDS / premium gating (Free plan — static-fallback path)', () => {
    it('PREMIUM_MODULE_IDS is exactly AI, Forecasting, Studio, Payroll', () => {
      expect([...PREMIUM_MODULE_IDS].sort((a, b) => a - b)).toEqual(
        [AppModule.AI, AppModule.Forecasting, AppModule.Studio, AppModule.Payroll].sort((a, b) => a - b)
      );
    });

    it('premium modules are never core', () => {
      for (const premium of PREMIUM_MODULE_IDS) {
        expect((CORE_MODULE_IDS as readonly AppModule[]).includes(premium)).toBe(false);
      }
    });

    it('optionalModulesFor never includes a premium module, for any segment/domain', () => {
      for (const segment of SEGMENT_OPTIONS) {
        for (const domain of DOMAIN_OPTIONS) {
          const optional = optionalModulesFor(segment.code, domain.code);
          for (const premium of PREMIUM_MODULE_IDS) {
            expect(optional).withContext(`${segment.code}/${domain.code}`).not.toContain(premium);
          }
        }
      }
    });

    it('optionalModulesFor still surfaces non-premium optional modules where not recommended (commerce + autre)', () => {
      const optional = optionalModulesFor('commerce', 'autre');
      // commerce base = {Purchases, Stock, Fiscal}; no domain overlay for autre.
      expect(optional).toContain(AppModule.Accounting);
      expect(optional).toContain(AppModule.CRM);
      expect(optional).toContain(AppModule.Projects);
      expect(optional).toContain(AppModule.RecurringContracts);
      for (const premium of PREMIUM_MODULE_IDS) {
        expect(optional).not.toContain(premium);
      }
    });

    it('recommendedModulesFor never includes a premium module (premium never recommended in the static catalog)', () => {
      for (const segment of SEGMENT_OPTIONS) {
        for (const domain of DOMAIN_OPTIONS) {
          const recommended = recommendedModulesFor(segment.code, domain.code);
          for (const premium of PREMIUM_MODULE_IDS) {
            expect(recommended).withContext(`${segment.code}/${domain.code}`).not.toContain(premium);
          }
        }
      }
    });
  });

  describe('defaultWarehouseNameFor', () => {
    it('returns the legacy default for entreprise', () => {
      expect(defaultWarehouseNameFor('entreprise')).toBe('Entrepôt Principal');
    });

    it('returns the commerce-specific default', () => {
      expect(defaultWarehouseNameFor('commerce')).toBe('Magasin principal');
    });

    it('returns undefined for unknown/absent segment', () => {
      expect(defaultWarehouseNameFor(null)).toBeUndefined();
      expect(defaultWarehouseNameFor('bogus')).toBeUndefined();
    });
  });

  /**
   * Parity contract with the backend `SectorConfigurationCatalog` and the design
   * mockups (/code/.plans/designs/type-societe-reference.html): exact codes, exact
   * canonical French labels, and the plan §5 recommended-module id sets. This spec
   * exists specifically to catch drift between the frontend static catalog and the
   * backend catalog — both are declarative Phase 1 data and must stay in lockstep.
   */
  describe('sector catalog parity contract (plan §5 + design mockup labels)', () => {
    it('pins the exact 6 segment codes, in order', () => {
      expect(SEGMENT_OPTIONS.map(s => s.code)).toEqual([
        'entreprise',
        'commerce',
        'services',
        'btp-construction',
        'association',
        'etablissement-educatif'
      ]);
    });

    it('pins the exact canonical French segment labels', () => {
      const labels: Record<string, string> = {};
      for (const s of SEGMENT_OPTIONS) labels[s.code] = s.label;
      expect(labels).toEqual({
        'entreprise': 'Entreprise',
        'commerce': 'Commerce',
        'services': 'Prestations de services',
        'btp-construction': 'BTP & Construction',
        'association': 'Association',
        'etablissement-educatif': 'Établissement éducatif'
      });
    });

    it('pins the exact 10 domain codes, in order', () => {
      expect(DOMAIN_OPTIONS.map(d => d.code)).toEqual([
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
      ]);
    });

    it('pins the exact canonical French domain labels (with "&", "Autre domaine" fallback)', () => {
      const labels: Record<string, string> = {};
      for (const d of DOMAIN_OPTIONS) labels[d.code] = d.label;
      expect(labels).toEqual({
        'technologie-informatique': 'Technologie & Informatique',
        'alimentation-agroalimentaire': 'Alimentation & Agroalimentaire',
        'sante-paramedical': 'Santé & Paramédical',
        'textile-habillement': 'Textile & Habillement',
        'transport-logistique': 'Transport & Logistique',
        'immobilier': 'Immobilier',
        'energie-environnement': 'Énergie & Environnement',
        'communication-marketing': 'Communication & Marketing',
        'artisanat': 'Artisanat',
        'autre': 'Autre domaine'
      });
    });

    it('pins the exact core module id set (plan §5)', () => {
      expect([...CORE_MODULE_IDS].sort((a, b) => a - b)).toEqual([
        AppModule.Clients,
        AppModule.Products,
        AppModule.Sales,
        AppModule.Treasury,
        AppModule.Reports,
        AppModule.Administration
      ].sort((a, b) => a - b));
    });

    const SEGMENT_ONLY_EXPECTED_MODULES: Record<string, AppModule[]> = {
      'entreprise': [
        AppModule.Administration, AppModule.Clients, AppModule.Products, AppModule.Sales,
        AppModule.Treasury, AppModule.Reports,
        AppModule.Purchases, AppModule.Stock, AppModule.Accounting, AppModule.CRM, AppModule.Fiscal
      ],
      'commerce': [
        AppModule.Administration, AppModule.Clients, AppModule.Products, AppModule.Sales,
        AppModule.Treasury, AppModule.Reports,
        AppModule.Purchases, AppModule.Stock, AppModule.Fiscal
      ],
      'services': [
        AppModule.Administration, AppModule.Clients, AppModule.Products, AppModule.Sales,
        AppModule.Treasury, AppModule.Reports,
        AppModule.CRM, AppModule.Projects, AppModule.RecurringContracts, AppModule.Fiscal
      ],
      'btp-construction': [
        AppModule.Administration, AppModule.Clients, AppModule.Products, AppModule.Sales,
        AppModule.Treasury, AppModule.Reports,
        AppModule.Purchases, AppModule.Stock, AppModule.Projects, AppModule.Fiscal
      ],
      'association': [
        AppModule.Administration, AppModule.Clients, AppModule.Products, AppModule.Sales,
        AppModule.Treasury, AppModule.Reports,
        AppModule.Accounting, AppModule.Fiscal
      ],
      'etablissement-educatif': [
        AppModule.Administration, AppModule.Clients, AppModule.Products, AppModule.Sales,
        AppModule.Treasury, AppModule.Reports,
        AppModule.RecurringContracts, AppModule.Accounting, AppModule.Fiscal
      ]
    };

    it('pins the exact per-segment recommended-module id set (base, no domain overlay — plan §5 table)', () => {
      for (const [segment, expected] of Object.entries(SEGMENT_ONLY_EXPECTED_MODULES)) {
        const result = recommendedModulesFor(segment, null);
        const expectedSorted = [...new Set(expected)].sort((a, b) => a - b);
        expect(result).toEqual(expectedSorted);
      }
    });

    const DOMAIN_OVERLAY_EXPECTED_MODULES: Record<string, AppModule[]> = {
      'technologie-informatique': [AppModule.Projects, AppModule.RecurringContracts],
      'alimentation-agroalimentaire': [AppModule.Stock, AppModule.Purchases],
      'sante-paramedical': [AppModule.CRM],
      'textile-habillement': [AppModule.Stock],
      'transport-logistique': [AppModule.Stock],
      'immobilier': [AppModule.Projects, AppModule.RecurringContracts],
      'energie-environnement': [AppModule.Projects, AppModule.Purchases],
      'communication-marketing': [AppModule.CRM],
      'artisanat': [AppModule.Stock, AppModule.Purchases],
      // « Autre domaine » est le seul overlay volontairement vide : filet de sécurité quand
      // l'utilisateur ne se reconnaît dans aucun domaine, il ne doit rien présumer.
      'autre': []
    };

    it('pins the exact domain overlay additions on top of the "entreprise" base (plan §5 table)', () => {
      const base = new Set(recommendedModulesFor('entreprise', null));
      for (const [domain, overlay] of Object.entries(DOMAIN_OVERLAY_EXPECTED_MODULES)) {
        const result = new Set(recommendedModulesFor('entreprise', domain));
        const expected = new Set([...base, ...overlay]);
        expect(result).toEqual(expected);
      }
    });
  });

  describe('RegistrationCatalogService', () => {
    let service: RegistrationCatalogService;
    let httpMock: HttpTestingController;
    const CATALOG_URL = `${environment.apiUrl}/public/sector-catalog`;

    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [provideHttpClient(), provideHttpClientTesting()]
      });
      service = TestBed.inject(RegistrationCatalogService);
      httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
      httpMock.verify();
    });

    it('resolves French labels for known codes', () => {
      expect(service.segmentLabel('entreprise')).toBe('Entreprise');
      expect(service.domainLabel('technologie-informatique')).toBe('Technologie & Informatique');
    });

    it('flags core modules correctly', () => {
      expect(service.isCoreModule(AppModule.Administration)).toBe(true);
      expect(service.isCoreModule(AppModule.Stock)).toBe(false);
    });

    it('delegates recommendedModules/optionalModules to the pure functions in fallback (idle) mode', () => {
      expect(service.recommendedModules('commerce', 'autre')).toEqual(recommendedModulesFor('commerce', 'autre'));
      expect(service.optionalModules('commerce', 'autre')).toEqual(optionalModulesFor('commerce', 'autre'));
    });

    it('domainsForSegment filters by the static SEGMENT_ALLOWED_DOMAINS matrix before load() (plan §3.1/§3.3)', () => {
      expect(service.domainsForSegment('commerce').map(d => d.code)).toEqual(COMMERCE_FALLBACK_DOMAINS);
      expect(service.domainsForSegment(null).length).toBe(0);
      expect(service.domainsForSegment('bogus-segment').length).toBe(0);
    });

    it('domainsForSegment pins the exact matrix for every known segment before load() (plan §3.1)', () => {
      const expected: Record<string, string[]> = {
        'entreprise': [
          'technologie-informatique', 'alimentation-agroalimentaire', 'sante-paramedical',
          'textile-habillement', 'transport-logistique', 'immobilier', 'energie-environnement',
          'communication-marketing', 'artisanat', 'autre'
        ],
        'commerce': COMMERCE_FALLBACK_DOMAINS,
        'services': [
          'technologie-informatique', 'communication-marketing', 'sante-paramedical',
          'transport-logistique', 'immobilier', 'autre'
        ],
        'btp-construction': ['immobilier', 'energie-environnement', 'artisanat', 'autre'],
        'association': [
          'sante-paramedical', 'energie-environnement', 'communication-marketing', 'artisanat', 'autre'
        ],
        'etablissement-educatif': [
          'technologie-informatique', 'sante-paramedical', 'artisanat', 'communication-marketing', 'autre'
        ]
      };
      for (const [segment, codes] of Object.entries(expected)) {
        expect(service.domainsForSegment(segment).map(d => d.code)).toEqual(codes);
      }
    });

    function fakeCatalog(): SectorCatalogDto {
      return {
        segments: [
          {
            code: 'commerce',
            labelFr: 'Commerce',
            descriptionFr: 'Négoce et distribution.',
            iconKey: 'shopping-cart',
            sortOrder: 0,
            coreModuleIds: [...CORE_MODULE_IDS],
            recommendedModuleIds: [AppModule.Purchases, AppModule.Stock],
            defaultWarehouseName: 'Magasin principal',
            domainCodes: ['alimentation-agroalimentaire', 'artisanat']
          }
        ],
        domains: [
          { code: 'alimentation-agroalimentaire', labelFr: 'Alimentation & Agroalimentaire', sortOrder: 0, additionalModuleIds: [AppModule.Stock] },
          { code: 'artisanat', labelFr: 'Artisanat', sortOrder: 1, additionalModuleIds: [] },
          { code: 'autre', labelFr: 'Autre domaine', sortOrder: 2, additionalModuleIds: [] }
        ],
        modules: [
          { id: AppModule.Purchases, code: 'purchases', labelFr: 'Achats', isCore: false },
          { id: AppModule.Stock, code: 'stock', labelFr: 'Stock', isCore: false },
          { id: AppModule.Forecasting, code: 'forecasting', labelFr: 'Prévisions IA', isCore: false }
        ],
        moduleDependencies: [
          { moduleId: AppModule.Forecasting, requiredModuleId: AppModule.Stock }
        ]
      };
    }

    it('load() success sets loadState to "remote" and serves segments/domains/modules from the response', () => {
      service.load();
      const req = httpMock.expectOne(CATALOG_URL);
      req.flush({ success: true, data: fakeCatalog() } as ApiResponse<SectorCatalogDto>);

      expect(service.loadState()).toBe('remote');
      expect(service.segments.map(s => s.code)).toEqual(['commerce']);
      expect(service.domains.map(d => d.code)).toEqual(['alimentation-agroalimentaire', 'artisanat', 'autre']);
    });

    it('domainsForSegment(remote) returns the segment ordered domainCodes with "autre" appended', () => {
      service.load();
      httpMock.expectOne(CATALOG_URL).flush({ success: true, data: fakeCatalog() } as ApiResponse<SectorCatalogDto>);

      expect(service.domainsForSegment('commerce').map(d => d.code)).toEqual([
        'alimentation-agroalimentaire', 'artisanat', 'autre'
      ]);
      expect(service.domainsForSegment(null)).toEqual([]);
      expect(service.domainsForSegment('unknown-segment')).toEqual([]);
    });

    it('404 fails loudly after retries with backoff: loadState becomes "fallback", static catalog still served, usedStaticFallback flips true', fakeAsync(() => {
      const warnSpy = spyOn(console, 'warn');
      expect(service.usedStaticFallback()).toBe(false);

      service.load();
      httpMock.expectOne(CATALOG_URL).flush('not found', { status: 404, statusText: 'Not Found' });
      tick(1500); // 1st retry backoff (1 * 1500ms)
      httpMock.expectOne(CATALOG_URL).flush('not found', { status: 404, statusText: 'Not Found' });
      tick(3000); // 2nd retry backoff (2 * 1500ms)
      httpMock.expectOne(CATALOG_URL).flush('not found', { status: 404, statusText: 'Not Found' });

      expect(service.loadState()).toBe('fallback');
      expect(service.usedStaticFallback()).toBe(true);
      expect(service.domainsForSegment('commerce').map(d => d.code)).toEqual(COMMERCE_FALLBACK_DOMAINS);
      expect(service.segments).toEqual(SEGMENT_OPTIONS);
      expect(warnSpy).toHaveBeenCalled();
    }));

    it('retries twice (3 attempts total) with linear backoff on 500 then falls back to static on repeated failure (plan 2.1)', fakeAsync(() => {
      service.load();
      httpMock.expectOne(CATALOG_URL).flush('boom', { status: 500, statusText: 'Server Error' });
      tick(1500);
      httpMock.expectOne(CATALOG_URL).flush('boom again', { status: 500, statusText: 'Server Error' });
      tick(3000);
      httpMock.expectOne(CATALOG_URL).flush('boom once more', { status: 500, statusText: 'Server Error' });

      expect(service.loadState()).toBe('fallback');
      expect(service.usedStaticFallback()).toBe(true);
    }));

    it('recovers on the 2nd retry (3rd attempt) without falling back', fakeAsync(() => {
      service.load();
      httpMock.expectOne(CATALOG_URL).flush('boom', { status: 500, statusText: 'Server Error' });
      tick(1500);
      httpMock.expectOne(CATALOG_URL).flush('boom again', { status: 500, statusText: 'Server Error' });
      tick(3000);
      httpMock.expectOne(CATALOG_URL).flush({ success: true, data: fakeCatalog() } as ApiResponse<SectorCatalogDto>);

      expect(service.loadState()).toBe('remote');
      expect(service.usedStaticFallback()).toBe(false);
    }));

    it('does not fetch at all when the frontend kill-switch is off', () => {
      environment.featureFlags.sectorCatalogHttp = false;
      try {
        service.load();
        httpMock.expectNone(CATALOG_URL);
        expect(service.loadState()).toBe('idle');
      } finally {
        environment.featureFlags.sectorCatalogHttp = true;
      }
    });

    it('filters unknown module ids out of the remote payload', () => {
      const catalog = fakeCatalog();
      catalog.modules.push({ id: 9999, code: 'ghost', labelFr: 'Fantôme', isCore: false });
      catalog.segments[0].recommendedModuleIds.push(9999);

      service.load();
      httpMock.expectOne(CATALOG_URL).flush({ success: true, data: catalog } as ApiResponse<SectorCatalogDto>);

      expect(service.modules.some(m => m.id === (9999 as unknown as AppModule))).toBe(false);
      expect(service.recommendedModules('commerce', null)).not.toContain(9999 as unknown as AppModule);
    });

    it('requiredBy / dependentsOf compute the transitive dependency closure and guard cycles', () => {
      service.load();
      httpMock.expectOne(CATALOG_URL).flush({ success: true, data: fakeCatalog() } as ApiResponse<SectorCatalogDto>);

      expect(service.requiredBy(AppModule.Forecasting)).toEqual([AppModule.Stock]);
      expect(service.requiredBy(AppModule.Stock)).toEqual([]);
      expect(service.dependentsOf(AppModule.Stock, [AppModule.Forecasting, AppModule.Purchases])).toEqual([AppModule.Forecasting]);
      expect(service.dependentsOf(AppModule.Stock, [AppModule.Purchases])).toEqual([]);
    });

    it('recommendedModules() closes over hard dependencies (a recommended module pulls its hard requirement)', () => {
      // Forecasting is now a premium (Free-plan-locked) module and is filtered out of
      // recommendedModules(), so this closure test uses a non-premium recommended module
      // (Purchases) with a fabricated hard dependency on Stock to exercise the same
      // transitive-closure logic (plan WP-F3).
      const catalog = fakeCatalog();
      catalog.segments[0].recommendedModuleIds = [AppModule.Purchases];
      catalog.moduleDependencies = [{ moduleId: AppModule.Purchases, requiredModuleId: AppModule.Stock }];
      service.load();
      httpMock.expectOne(CATALOG_URL).flush({ success: true, data: catalog } as ApiResponse<SectorCatalogDto>);

      const result = service.recommendedModules('commerce', null);
      expect(result).toContain(AppModule.Purchases);
      expect(result).toContain(AppModule.Stock);
    });

    // Premium (paid-plan) module gating — Free plan must not let users freely select
    // AI/Forecasting/Studio/Payroll, and they must never be submitted in enabledModules.
    describe('premium module gating (Free plan)', () => {
      it('isLockedOnFreePlan is true for every PREMIUM_MODULE_IDS id and false for core/standard modules (idle/static path)', () => {
        for (const premium of PREMIUM_MODULE_IDS) {
          expect(service.isLockedOnFreePlan(premium)).toBe(true);
        }
        expect(service.isLockedOnFreePlan(AppModule.Administration)).toBe(false);
        expect(service.isLockedOnFreePlan(AppModule.Stock)).toBe(false);
        expect(service.isLockedOnFreePlan(AppModule.Honoraires)).toBe(false);
      });

      it('optionalModules excludes premium modules in the static-fallback (idle) path', () => {
        const optional = service.optionalModules('commerce', 'autre');
        for (const premium of PREMIUM_MODULE_IDS) {
          expect(optional).not.toContain(premium);
        }
      });

      it('premiumModules returns exactly the premium ids present in the static catalog, sorted, never core (idle path)', () => {
        const premium = service.premiumModules();
        expect(premium).toEqual([...PREMIUM_MODULE_IDS].sort((a, b) => a - b));
        for (const id of premium) {
          expect(service.isCoreModule(id)).toBe(false);
        }
      });

      it('recommended/optional/premium are mutually disjoint and partition every non-core, non-Honoraires module (idle path)', () => {
        const segment = 'entreprise';
        const domain = 'technologie-informatique';
        const recommended = new Set(service.recommendedModules(segment, domain));
        const optional = new Set(service.optionalModules(segment, domain));
        const premium = new Set(service.premiumModules());
        for (const id of optional) {
          expect(recommended.has(id)).toBe(false);
          expect(premium.has(id)).toBe(false);
        }
        for (const id of premium) {
          expect(recommended.has(id)).toBe(false);
          expect(optional.has(id)).toBe(false);
        }
        const all = service.modules
          .filter(m => !service.isCoreModule(m.id) && m.id !== AppModule.Honoraires)
          .map(m => m.id);
        for (const id of all) {
          const count = (recommended.has(id) ? 1 : 0) + (optional.has(id) ? 1 : 0) + (premium.has(id) ? 1 : 0);
          expect(count).withContext(`module id ${id}`).toBe(1);
        }
      });

      describe('remote path', () => {
        function fakePremiumCatalog(): SectorCatalogDto {
          return {
            segments: [
              {
                code: 'commerce', labelFr: 'Commerce', descriptionFr: '', iconKey: 'shopping-cart', sortOrder: 0,
                coreModuleIds: [...CORE_MODULE_IDS],
                recommendedModuleIds: [AppModule.Purchases, AppModule.Stock],
                defaultWarehouseName: 'Magasin principal', domainCodes: ['artisanat']
              }
            ],
            domains: [{ code: 'artisanat', labelFr: 'Artisanat', sortOrder: 0, additionalModuleIds: [] }],
            modules: [
              { id: AppModule.Purchases, code: 'purchases', labelFr: 'Achats', isCore: false, availableOnFreePlan: true },
              { id: AppModule.Stock, code: 'stock', labelFr: 'Stock', isCore: false, availableOnFreePlan: true },
              { id: AppModule.Accounting, code: 'accounting', labelFr: 'Comptabilité', isCore: false, availableOnFreePlan: true },
              { id: AppModule.AI, code: 'ai', labelFr: 'Assistant IA', isCore: false, availableOnFreePlan: false },
              { id: AppModule.Forecasting, code: 'forecasting', labelFr: 'Prévisions IA', isCore: false, availableOnFreePlan: false }
            ],
            moduleDependencies: []
          };
        }

        function loadRemote(catalog: SectorCatalogDto = fakePremiumCatalog()): void {
          service.load();
          httpMock.expectOne(CATALOG_URL).flush({ success: true, data: catalog } as ApiResponse<SectorCatalogDto>);
        }

        it('isLockedOnFreePlan honors availableOnFreePlan:false from the remote payload', () => {
          loadRemote();
          expect(service.isLockedOnFreePlan(AppModule.AI)).toBe(true);
          expect(service.isLockedOnFreePlan(AppModule.Forecasting)).toBe(true);
          expect(service.isLockedOnFreePlan(AppModule.Purchases)).toBe(false);
          expect(service.isLockedOnFreePlan(AppModule.Stock)).toBe(false);
        });

        it('optionalModules excludes premium modules but keeps non-premium optional ones (remote path)', () => {
          loadRemote();
          const optional = service.optionalModules('commerce', 'artisanat');
          expect(optional).not.toContain(AppModule.AI);
          expect(optional).not.toContain(AppModule.Forecasting);
          expect(optional).toContain(AppModule.Accounting);
        });

        it('premiumModules returns only the premium modules present in the remote catalog (remote path)', () => {
          loadRemote();
          expect(service.premiumModules()).toEqual([AppModule.AI, AppModule.Forecasting]);
        });

        it('a non-canonical module flagged availableOnFreePlan:false is also locked (defensive remote field)', () => {
          const catalog = fakePremiumCatalog();
          // Flag a non-premium module as unavailable on Free — the remote field must gate it too.
          const accounting = catalog.modules.find(m => m.id === AppModule.Accounting)!;
          accounting.availableOnFreePlan = false;
          loadRemote(catalog);

          expect(service.isLockedOnFreePlan(AppModule.Accounting)).toBe(true);
          expect(service.optionalModules('commerce', 'artisanat')).not.toContain(AppModule.Accounting);
          expect(service.premiumModules()).toContain(AppModule.Accounting);
        });

        it('a premium module with an absent availableOnFreePlan flag is still locked via PREMIUM_MODULE_IDS (older payload)', () => {
          // Simulate an older payload that omits the flag entirely on AI.
          const catalog = fakePremiumCatalog();
          catalog.modules = catalog.modules.map(m =>
            m.id === AppModule.AI ? { id: m.id, code: m.code, labelFr: m.labelFr, isCore: m.isCore } : m
          );
          loadRemote(catalog);

          expect(service.isLockedOnFreePlan(AppModule.AI)).toBe(true);
          expect(service.optionalModules('commerce', 'artisanat')).not.toContain(AppModule.AI);
          expect(service.premiumModules()).toContain(AppModule.AI);
        });
      });
    });

    // Plan §3.1 — suggestedTaxRegimeFor
    describe('suggestedTaxRegimeFor (plan §3.1)', () => {
      it('returns the suggestion when the remote catalog carries suggestedTaxRegimes for the segment', () => {
        const catalog = fakeCatalog();
        catalog.suggestedTaxRegimes = [
          { segmentCode: 'commerce', regime: 1, noteFr: 'Le régime forfaitaire est usuel en commerce.' }
        ];
        service.load();
        httpMock.expectOne(CATALOG_URL).flush({ success: true, data: catalog } as ApiResponse<SectorCatalogDto>);

        const suggestion = service.suggestedTaxRegimeFor('commerce');
        expect(suggestion).not.toBeUndefined();
        expect(suggestion!.regime).toBe(1);
        expect(suggestion!.noteFr).toContain('forfaitaire');
      });

      it('returns undefined when the remote catalog has no suggestedTaxRegimes field (graceful absence)', () => {
        service.load();
        httpMock.expectOne(CATALOG_URL).flush({ success: true, data: fakeCatalog() } as ApiResponse<SectorCatalogDto>);

        expect(service.suggestedTaxRegimeFor('commerce')).toBeUndefined();
      });

      it('returns undefined when the remote catalog has no entry for the given segment', () => {
        const catalog = fakeCatalog();
        catalog.suggestedTaxRegimes = [
          { segmentCode: 'association', regime: 2, noteFr: 'Exonéré pour les associations.' }
        ];
        service.load();
        httpMock.expectOne(CATALOG_URL).flush({ success: true, data: catalog } as ApiResponse<SectorCatalogDto>);

        expect(service.suggestedTaxRegimeFor('commerce')).toBeUndefined();
      });

      it('returns undefined when no remote catalog has loaded yet (static/fallback mode)', () => {
        expect(service.suggestedTaxRegimeFor('commerce')).toBeUndefined();
      });

      it('returns undefined for an empty/null segment', () => {
        expect(service.suggestedTaxRegimeFor('')).toBeUndefined();
        expect(service.suggestedTaxRegimeFor(null)).toBeUndefined();
        expect(service.suggestedTaxRegimeFor(undefined)).toBeUndefined();
      });

      it('returns undefined when suggestedTaxRegimes is not an array (defensive)', () => {
        const catalog = fakeCatalog();
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        (catalog as any).suggestedTaxRegimes = 'not-an-array';
        service.load();
        httpMock.expectOne(CATALOG_URL).flush({ success: true, data: catalog } as ApiResponse<SectorCatalogDto>);

        expect(service.suggestedTaxRegimeFor('commerce')).toBeUndefined();
      });
    });
  });

  describe('applyProfileOverlay (profilage, lot 3)', () => {
    const CORE = CORE_MODULE_IDS;
    const OPTS = {
      coreModuleIds: CORE,
      isLocked: (id: AppModule) => PREMIUM_MODULE_IDS.includes(id)
    };
    const base = () => [...CORE, AppModule.Accounting, AppModule.Fiscal];

    it('est l’identité quand aucune question n’a de réponse (additivité stricte)', () => {
      const before = base().sort((a, b) => a - b);
      expect(applyProfileOverlay(before, EMPTY_PROFILE_ANSWERS, OPTS)).toEqual(before);
    });

    it('ajoute Stock et Achats quand l’entreprise gère du stock physique', () => {
      const result = applyProfileOverlay(base(), { ...EMPTY_PROFILE_ANSWERS, hasPhysicalStock: true }, OPTS);
      expect(result).toContain(AppModule.Stock);
      expect(result).toContain(AppModule.Purchases);
    });

    it('ajoute Stock quand l’activité est B2C (c’est ce qui débloque le Point de Vente)', () => {
      const result = applyProfileOverlay(base(), { ...EMPTY_PROFILE_ANSWERS, sellsToConsumers: true }, OPTS);
      expect(result).toContain(AppModule.Stock);
    });

    it('retire Comptabilité quand la compta est déléguée à un cabinet', () => {
      const result = applyProfileOverlay(base(), { ...EMPTY_PROFILE_ANSWERS, accountingDelegatedToFirm: true }, OPTS);
      expect(result).not.toContain(AppModule.Accounting);
    });

    it('fait gagner le retrait sur l’ajout en cas de réponses contradictoires', () => {
      const result = applyProfileOverlay(
        base(),
        { ...EMPTY_PROFILE_ANSWERS, sellsToConsumers: true, hasPhysicalStock: false },
        OPTS
      );
      expect(result).not.toContain(AppModule.Stock);
    });

    it('ne retire JAMAIS un module cœur, quelles que soient les réponses', () => {
      const answers = {
        hasPhysicalStock: false,
        sellsToConsumers: false,
        headcountBand: '1' as const,
        accountingDelegatedToFirm: true
      };
      const result = applyProfileOverlay(base(), answers, OPTS);
      for (const core of CORE) {
        expect(result).toContain(core);
      }
    });

    it('n’ajoute JAMAIS un module verrouillé sur le plan Free', () => {
      const lockAll = { coreModuleIds: CORE, isLocked: () => true };
      const result = applyProfileOverlay(base(), { ...EMPTY_PROFILE_ANSWERS, hasPhysicalStock: true }, lockAll);
      expect(result).not.toContain(AppModule.Stock);
      expect(result).not.toContain(AppModule.Purchases);
    });

    it('la tranche d’effectif ne modifie pas la sélection (RH & Paie est un module payant)', () => {
      const before = base().sort((a, b) => a - b);
      for (const band of ['1', '2-9', '10-49', '50+'] as const) {
        expect(applyProfileOverlay(before, { ...EMPTY_PROFILE_ANSWERS, headcountBand: band }, OPTS)).toEqual(before);
      }
    });
  });

});
