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
