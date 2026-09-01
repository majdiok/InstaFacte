/**
 * Parity contract test (plan v1 §2.1 — "Catalogue sectoriel : backend = source de
 * vérité unique, versionnée").
 *
 * Goal: fail CI loudly if the frontend static sector catalog
 * (`registration-catalog.ts` — `SEGMENT_OPTIONS`, `DOMAIN_OPTIONS`, `CORE_MODULE_IDS`,
 * `SEGMENT_ALLOWED_DOMAINS`, `recommendedModulesFor`, `defaultWarehouseNameFor`) ever
 * drifts from the backend's canonical static catalog snapshot, checked in by the
 * backend agent at:
 *
 *   src/Backend/FactuTrust.API/Resources/sector-catalog.snapshot.json
 *
 * Cross-tree import constraint: Angular's TypeScript project for this app restricts
 * `rootDir` to `src/Frontend/factutrust-web/src`, and the Karma/webpack test bundle only
 * resolves modules reachable from that root — a file under `src/Backend/...` cannot be
 * `import`ed/`require`d from a spec here. So this spec cannot read the backend file live;
 * instead it compares the frontend static catalog against `./sector-catalog.snapshot.fixture`,
 * a checked-in copy of that backend snapshot's data (see the header comment on that file for
 * exactly how/when it was copied).
 *
 * Two-part drift-detection contract:
 *   1. This spec pins the frontend static catalog against the *fixture copy* below.
 *   2. A separate CI step (outside `ng test`, run once per build from the repo root) is
 *      expected to byte-diff the fixture's data against the real backend snapshot file on
 *      every build — that is what actually guarantees the copy itself never silently drifts
 *      from the backend source of truth. Until that CI step exists, keeping the fixture in
 *      sync with the backend file is a manual step whenever the backend snapshot changes.
 *
 * `SEGMENT_RECOMMENDED_MODULES` and `DOMAIN_MODULE_OVERLAY` in `registration-catalog.ts` are
 * module-private (not exported), so this spec derives their effective content through the
 * already-exported, already-pinned `recommendedModulesFor(segment, domain)` helper: calling it
 * with only one of {segment, domain} set isolates that axis's contribution (core ∪ that axis),
 * so subtracting `CORE_MODULE_IDS` recovers exactly the private map's value for that key
 * without needing to widen either map's visibility.
 */
import { AppModule, APP_MODULE_OPTIONS } from '@core/models/app-module';
import {
  SEGMENT_OPTIONS,
  DOMAIN_OPTIONS,
  CORE_MODULE_IDS,
  SEGMENT_ALLOWED_DOMAINS,
  recommendedModulesFor,
  defaultWarehouseNameFor,
  type CompanySegmentCode,
  type BusinessDomainCode
} from './registration-catalog';
import { SECTOR_CATALOG_SNAPSHOT_FIXTURE } from './sector-catalog.snapshot.fixture';

function sortedNumbers(ids: readonly number[]): number[] {
  return [...ids].sort((a, b) => a - b);
}

describe('sector catalog parity contract (frontend static catalog vs backend snapshot)', () => {
  const snapshot = SECTOR_CATALOG_SNAPSHOT_FIXTURE;
  const backendSegments = snapshot.segments.slice().sort((a, b) => a.sortOrder - b.sortOrder);
  const backendDomains = snapshot.domains.slice().sort((a, b) => a.sortOrder - b.sortOrder);

  it('has one SEGMENT_OPTIONS entry per backend segment, same order, matching codes', () => {
    expect(SEGMENT_OPTIONS.map(s => s.code)).toEqual(backendSegments.map(s => s.code));
  });

  it('SEGMENT_OPTIONS labels match the backend segments\' labelFr, 1:1', () => {
    expect(SEGMENT_OPTIONS.map(s => s.label)).toEqual(backendSegments.map(s => s.labelFr));
  });

  it('has one DOMAIN_OPTIONS entry per backend domain, same order, matching codes', () => {
    expect(DOMAIN_OPTIONS.map(d => d.code)).toEqual(backendDomains.map(d => d.code));
  });

  it('DOMAIN_OPTIONS labels match the backend domains\' labelFr, 1:1', () => {
    expect(DOMAIN_OPTIONS.map(d => d.label)).toEqual(backendDomains.map(d => d.labelFr));
  });

  it('CORE_MODULE_IDS (as a set) matches every backend segment\'s coreModuleIds', () => {
    expect(backendSegments.length).toBeGreaterThan(0);
    for (const segment of backendSegments) {
      expect(sortedNumbers(segment.coreModuleIds))
        .withContext(`segment "${segment.code}"`)
        .toEqual(sortedNumbers(CORE_MODULE_IDS));
    }
  });

  backendSegments.forEach(segment => {
    it(`recommended module ids match the backend snapshot for segment "${segment.code}"`, () => {
      const backendRecommended = sortedNumbers(segment.recommendedModuleIds);
      // Passing a null domain isolates the segment axis: recommendedModulesFor merges
      // CORE_MODULE_IDS ∪ SEGMENT_RECOMMENDED_MODULES[segment] ∪ (nothing, no known domain).
      const frontendCombined = recommendedModulesFor(segment.code, null);
      const frontendRecommendedOnly = sortedNumbers(
        frontendCombined.filter(id => !(CORE_MODULE_IDS as readonly AppModule[]).includes(id))
      );
      expect(frontendRecommendedOnly).toEqual(backendRecommended);
    });

    it(`default warehouse name matches the backend snapshot for segment "${segment.code}"`, () => {
      expect(defaultWarehouseNameFor(segment.code)).toEqual(segment.defaultWarehouseName ?? undefined);
    });

    it(`SEGMENT_ALLOWED_DOMAINS matches the backend snapshot's domainCodes, in order, for segment "${segment.code}"`, () => {
      const code = segment.code as CompanySegmentCode;
      expect(SEGMENT_ALLOWED_DOMAINS[code]).toEqual(segment.domainCodes as BusinessDomainCode[]);
    });
  });

  backendDomains.forEach(domain => {
    it(`overlay module ids match the backend snapshot for domain "${domain.code}"`, () => {
      const backendOverlay = sortedNumbers(domain.additionalModuleIds);
      // Passing a null segment isolates the domain axis: recommendedModulesFor merges
      // CORE_MODULE_IDS ∪ (nothing, no known segment) ∪ DOMAIN_MODULE_OVERLAY[domain].
      const frontendCombined = recommendedModulesFor(null, domain.code);
      const frontendOverlayOnly = sortedNumbers(
        frontendCombined.filter(id => !(CORE_MODULE_IDS as readonly AppModule[]).includes(id))
      );
      expect(frontendOverlayOnly).toEqual(backendOverlay);
    });
  });

  it(
    'backend module catalog matches APP_MODULE_OPTIONS ids/labels, excluding Honoraires ' +
      '(firm-native module, intentionally never offered during registration)',
    () => {
      expect(snapshot.modules.some(m => m.id === AppModule.Honoraires)).toBe(false);

      const backendModules = snapshot.modules.slice().sort((a, b) => a.id - b.id);
      const frontendModulesWithoutHonoraires = APP_MODULE_OPTIONS
        .filter(o => o.value !== AppModule.Honoraires)
        .slice()
        .sort((a, b) => a.value - b.value);

      expect(backendModules.map(m => m.id)).toEqual(frontendModulesWithoutHonoraires.map(m => m.value));
      expect(backendModules.map(m => m.labelFr)).toEqual(frontendModulesWithoutHonoraires.map(m => m.label));
    }
  );

  it('backend snapshot has no module dependencies (frontend static fallback assumes none)', () => {
    // `RegistrationCatalogService`'s static fallback path never populates `moduleDependencies`
    // (that field only exists on the remote DTO). If the backend snapshot ever starts shipping
    // dependency edges, the static fallback would silently diverge from the backend's real
    // "requires" rules — this assertion turns that into a loud, actionable test failure instead.
    expect(snapshot.moduleDependencies).toEqual([]);
  });
});
