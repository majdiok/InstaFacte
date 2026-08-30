import { TestBed } from '@angular/core/testing';
import { AppModule } from '@core/models/app-module';
import {
  CORE_MODULE_IDS,
  DOMAIN_OPTIONS,
  RegistrationCatalogService,
  SEGMENT_OPTIONS,
  defaultWarehouseNameFor,
  optionalModulesFor,
  recommendedModulesFor
} from './registration-catalog';

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
      'immobilier': [],
      'energie-environnement': [],
      'communication-marketing': [AppModule.CRM],
      'artisanat': [],
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

    beforeEach(() => {
      TestBed.configureTestingModule({});
      service = TestBed.inject(RegistrationCatalogService);
    });

    it('resolves French labels for known codes', () => {
      expect(service.segmentLabel('entreprise')).toBe('Entreprise');
      expect(service.domainLabel('technologie-informatique')).toBe('Technologie & Informatique');
    });

    it('flags core modules correctly', () => {
      expect(service.isCoreModule(AppModule.Administration)).toBe(true);
      expect(service.isCoreModule(AppModule.Stock)).toBe(false);
    });

    it('delegates recommendedModules/optionalModules to the pure functions', () => {
      expect(service.recommendedModules('commerce', 'autre')).toEqual(recommendedModulesFor('commerce', 'autre'));
      expect(service.optionalModules('commerce', 'autre')).toEqual(optionalModulesFor('commerce', 'autre'));
    });
  });
});
