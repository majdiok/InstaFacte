import {
  ContractProjection, parseBusinessCatalogManifestJson, projectBusinessCatalogManifest,
  projectResolvedBusinessSceneRequest, projectStreetExperienceConfig, projectVisualProfileRef
} from './business-scene-contract-projection';
import { CATALOG_VALIDATION_LIMITS, validateBusinessCatalogManifest } from './business-scene-contract-validation';
import { BusinessSceneRequest, ResolvedBusinessSceneRequest } from './business-scene-contracts';
import { CATALOG_VALIDATION_CASES } from '../testing/business-catalog-validation-corpus';
import { validManifest } from '../testing/business-catalog-validation-fixture';
import { acceptedRequest, neutralRequest, poisonUnknownFields, visualReference } from '../testing/business-catalog-projection-fixture';

function value<T>(result: ContractProjection<T>): T {
  if (!result.ok) throw new Error(JSON.stringify(result.violations));
  return result.value;
}

function objects(input: unknown): object[] {
  const found: object[] = [];
  const stack = [input];
  while (stack.length) {
    const v = stack.pop();
    if (typeof v !== 'object' || v === null) continue;
    found.push(v);
    for (const child of Object.values(v)) stack.push(child);
  }
  return found;
}

describe('allowlist contract projection', () => {
  for (const entry of CATALOG_VALIDATION_CASES) {
    it(`preserves canonical schema outcome: ${entry.name}`, () => {
      const input = JSON.parse(JSON.stringify(entry.create()));
      const result = projectBusinessCatalogManifest(input);
      expect(result.ok).toBe(entry.expectedCodes.length === 0);
      if (!result.ok) expect(result.violations).toEqual(validateBusinessCatalogManifest(input));
      else expect(validateBusinessCatalogManifest(result.value)).toEqual([]);
    });
  }

  it('strips poison at every open object level, retains all declared data, and freezes only detached output', () => {
    const expected = validManifest();
    const input = JSON.parse(JSON.stringify(expected));
    poisonUnknownFields(input);
    expect(validateBusinessCatalogManifest(input)).toEqual([]);
    const before = JSON.stringify(input);
    const output = value(projectBusinessCatalogManifest(input));
    expect<unknown>(output).toEqual(expected);
    expect(JSON.stringify(input)).toBe(before);
    const sourceObjects = new Set(objects(input));
    for (const object of objects(output)) {
      expect(sourceObjects.has(object)).toBeFalse();
      expect(Object.isFrozen(object)).toBeTrue();
    }
    for (const object of objects(input)) expect(Object.isFrozen(object)).toBeFalse();
    input.scenes[0].navigation.spawn.position[0] = 90;
    input.assets[0].gltf.requiredExtensions.push('private');
    expect<unknown>(output).toEqual(expected);
    expect(() => (output.scenes[0].navigation.spawn.position as unknown as number[])[0] = 10).toThrow();
  });

  for (const mode of ['list', 'legacy', 'catalog-v2'] as const) {
    it(`projects ${mode} config without private data or changed schema`, () => {
      const expected = { schemaVersion: 1, configRevision: 'r1', leaseSeconds: 0.5, mode, catalogVersion: mode === 'catalog-v2' ? 'v2.synthetic' : null };
      const input = JSON.parse(JSON.stringify(expected));
      poisonUnknownFields(input);
      expect<unknown>(value(projectStreetExperienceConfig(input))).toEqual(expected);
      expect(Object.isFrozen(value(projectStreetExperienceConfig(input)))).toBeTrue();
    });
  }

  it('projects visual references without fabricated consent evidence', () => {
    const expected = visualReference();
    const input = visualReference();
    poisonUnknownFields(input);
    expect<unknown>(value(projectVisualProfileRef(input))).toEqual(expected);
    for (const invalid of [null, {}, { ...expected, editorialRevision: 'r0' }, { ...expected, schemaVersion: 2 }]) {
      const result = projectVisualProfileRef(invalid);
      expect(result.ok).toBeFalse();
      expect('value' in result).toBeFalse();
    }
  });

  for (const project of [projectVisualProfileRef, projectStreetExperienceConfig, projectResolvedBusinessSceneRequest]) {
    it('fails closed on bounded unknown extras before any allowlist copy', () => {
      const deep = visualReference();
      let cursor = deep;
      for (let i = 0; i < 34; i++) { const next = {}; cursor['private'] = next; cursor = next; }
      expect(project(deep).ok).toBeFalse();
      const cycle = visualReference(); cycle['cycle'] = cycle;
      expect(project(cycle).ok).toBeFalse();
    });
  }

  it('rejects invalid config without retaining a favorable value', () => {
    for (const leaseSeconds of [0, 61, NaN]) {
      expect(projectStreetExperienceConfig({ schemaVersion: 1, mode: 'legacy', catalogVersion: null, configRevision: 'r1', leaseSeconds }).ok).toBeFalse();
    }
  });

  it('fails closed on non-JSON values and does not invoke enumerable getters', () => {
    for (const privateValue of [undefined, () => 1, NaN, new Date(), 1n]) {
      expect(projectVisualProfileRef({ ...visualReference(), privateValue }).ok).toBeFalse();
    }
    let reads = 0;
    const input = visualReference();
    Object.defineProperty(input, 'profileKey', { enumerable: true, get: () => { reads++; return 'synthetic'; } });
    expect(projectVisualProfileRef(input).ok).toBeFalse();
    expect(reads).toBe(0);
  });

  it('bounds text before parsing and rejects malformed/deep JSON without a partial value', () => {
    expect(parseBusinessCatalogManifestJson('{').ok).toBeFalse();
    for (const text of [' '.repeat(CATALOG_VALIDATION_LIMITS.jsonBytes + 1), 'é'.repeat(CATALOG_VALIDATION_LIMITS.jsonBytes / 2 + 1)]) {
      const result = parseBusinessCatalogManifestJson(text);
      expect(result.ok).toBeFalse();
      if (!result.ok) expect(result.violations[0].code).toBe('manifest.limit-bytes');
    }
    expect(parseBusinessCatalogManifestJson('['.repeat(40) + '0' + ']'.repeat(40)).ok).toBeFalse();
  });
});

describe('resolved request representation, never authorization', () => {
  it('preserves BusinessSceneRequest and its accepted identity without substitution', () => {
    const input = acceptedRequest();
    const expected = acceptedRequest();
    poisonUnknownFields(input);
    const output = value(projectResolvedBusinessSceneRequest(input));
    expect<unknown>(output).toEqual(expected);
    if (output.kind !== 'accepted') throw new Error('Expected accepted branch');
    const preserved: BusinessSceneRequest = output.request;
    expect(preserved.visualProfile.editorialRevision).toBe('r7');
    expect(Object.isFrozen(preserved.visualProfile)).toBeTrue();
  });

  it('only carries explicit global selection/version/quality/scope, with no fake reference or private inference', () => {
    const input = neutralRequest();
    input['visualProfile'] = visualReference();
    input['request'] = acceptedRequest();
    input['slug'] = 'must-not-carry';
    input['facadeTheme'] = 4;
    poisonUnknownFields(input);
    const output = value(projectResolvedBusinessSceneRequest(input));
    expect<unknown>(output).toEqual(neutralRequest());
    expect('visualProfile' in output).toBeFalse();
    expect('request' in output).toBeFalse();
    // No type-valid selection is promoted into G1 authorization here.
    expect('authorized' in output).toBeFalse();
  });

  it('does not convert absent/invalid acceptance into a neutral decision', () => {
    for (const visualProfile of [null, undefined, { ...visualReference(), editorialRevision: 'r0' }]) {
      const input = acceptedRequest();
      (input['request'] as Record<string, unknown>)['visualProfile'] = visualProfile;
      const result = projectResolvedBusinessSceneRequest(input);
      expect(result.ok).toBeFalse();
      expect('value' in result).toBeFalse();
    }
    for (const input of [null, {}, { ...neutralRequest(), kind: 'neutral-family' }, { ...neutralRequest(), sceneKey: '../private' }, { ...neutralRequest(), quality: 'enhanced' }, { ...neutralRequest(), generation: -1 }]) {
      expect(projectResolvedBusinessSceneRequest(input).ok).toBeFalse();
    }
  });
});

// Compile-time contract checks, not executable authorization or consent proofs.
function typeChecks(request: BusinessSceneRequest, neutral: Extract<ResolvedBusinessSceneRequest, { kind: 'neutral-global' }>): void {
  const accepted: ResolvedBusinessSceneRequest = { kind: 'accepted', request };
  void accepted;
  // @ts-expect-error Global neutral deliberately has no consent-bearing reference.
  void neutral.visualProfile;
  // @ts-expect-error Accepted request must retain its VisualProfileRef.
  const missing: BusinessSceneRequest = { scopeId: 's', generation: 0, slug: 's', facadeTheme: 0, quality: 'economy', sceneKey: 's' };
  void missing;
}
void typeChecks;
