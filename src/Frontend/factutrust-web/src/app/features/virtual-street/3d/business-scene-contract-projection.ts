/**
 * Pure allowlist projection, not a tenant resolver or an authorizer. Inputs are
 * parsed JSON; use the bounded parser for text. No input object is retained or
 * frozen. Successful records are detached and deeply frozen, not just readonly.
 */
import {
  BusinessAssetDescriptor, BusinessCatalogManifest, BusinessCatalogProfile,
  BusinessSceneDescriptor, BusinessSceneRequest, BusinessSceneVariant,
  BusinessStyleRecipe, ResolvedBusinessSceneRequest, SceneBounds, SceneVector3,
  SceneViewpoint, StreetExperienceConfig, VisualProfileRef, BUSINESS_QUALITIES, FACADE_THEMES
} from './business-scene-contracts';
import {
  CATALOG_VALIDATION_LIMITS, ContractViolation, isContractKey, isValidCatalogVersionToken,
  validateBusinessCatalogManifest, validateContractJsonEnvelope,
  validateStreetExperienceConfig, validateVisualProfileRef
} from './business-scene-contract-validation';

export type ContractProjection<T> =
  | { readonly ok: true; readonly value: T }
  | { readonly ok: false; readonly violations: readonly ContractViolation[] };

function rejected(code: string, path: string, message: string): ContractProjection<never> {
  return { ok: false, violations: [{ code, path, message }] };
}

/** Only traverses newly built allowlist objects, whose depth/size is already bounded. */
function freeze<T>(value: T): T {
  const stack: unknown[] = [value];
  while (stack.length) {
    const current = stack.pop();
    if (typeof current !== 'object' || current === null) continue;
    for (const child of Object.values(current)) stack.push(child);
    Object.freeze(current);
  }
  return value;
}

function project<T>(input: unknown, root: string,
  validate: (value: unknown) => ContractViolation[], copy: (value: T) => T): ContractProjection<T> {
  try {
    const bounds = validateContractJsonEnvelope(input, root, true);
    const violations = bounds.length ? bounds : validate(input);
    if (violations.length) return { ok: false, violations };
    return { ok: true, value: freeze(copy(input as T)) };
  } catch {
    // Not a sandbox for arbitrary in-process JS objects/proxies. Malformed input
    // must still never yield a partially projected value or implicit neutral choice.
    return rejected('projection.input', root, 'Unable to project parsed contract data');
  }
}

/** Limit UTF-8 byte length without allocating an encoded copy before JSON.parse. */
export function parseBusinessCatalogManifestJson(text: string): ContractProjection<BusinessCatalogManifest> {
  if (typeof text !== 'string' || text.length > CATALOG_VALIDATION_LIMITS.jsonBytes) {
    return rejected('manifest.limit-bytes', 'manifest', 'JSON exceeds the byte ceiling');
  }
  let bytes = 0;
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i);
    if (code < 0x80) bytes++;
    else if (code < 0x800) bytes += 2;
    else if (code >= 0xd800 && code <= 0xdbff && i + 1 < text.length &&
      text.charCodeAt(i + 1) >= 0xdc00 && text.charCodeAt(i + 1) <= 0xdfff) { bytes += 4; i++; }
    else bytes += 3;
    if (bytes > CATALOG_VALIDATION_LIMITS.jsonBytes) {
      return rejected('manifest.limit-bytes', 'manifest', 'JSON exceeds the byte ceiling');
    }
  }
  try { return projectBusinessCatalogManifest(JSON.parse(text)); }
  catch { return rejected('manifest.json', 'manifest', 'Invalid JSON'); }
}

const vector = (v: SceneVector3): SceneVector3 => [v[0], v[1], v[2]];
const bounds = (v: SceneBounds): SceneBounds => ({ min: vector(v.min), max: vector(v.max) });
const viewpoint = (v: SceneViewpoint): SceneViewpoint => ({ key: v.key, position: vector(v.position), target: vector(v.target) });
const reference = (v: VisualProfileRef): VisualProfileRef => ({
  schemaVersion: v.schemaVersion, profileKey: v.profileKey, editorialRevision: v.editorialRevision,
  catalogVersion: v.catalogVersion, representationKind: v.representationKind
});

export function projectVisualProfileRef(input: unknown): ContractProjection<VisualProfileRef> {
  return project(input, 'visualProfile', validateVisualProfileRef, reference);
}

export function projectStreetExperienceConfig(input: unknown): ContractProjection<StreetExperienceConfig> {
  return project<StreetExperienceConfig>(input, 'experienceConfig', validateStreetExperienceConfig, v => ({
    schemaVersion: v.schemaVersion, configRevision: v.configRevision, leaseSeconds: v.leaseSeconds,
    // Discriminated union: catalogVersion is only non-null in catalog-v2 mode; keep both branches.
    ...(v.mode === 'catalog-v2' ? { mode: v.mode, catalogVersion: v.catalogVersion }
      : { mode: v.mode, catalogVersion: v.catalogVersion })
  }));
}

function variant(v: BusinessSceneVariant): BusinessSceneVariant {
  return {
    modelAssetKey: v.modelAssetKey, rootNode: v.rootNode,
    lods: v.lods.map(l => ({ node: l.node, minDistanceMeters: l.minDistanceMeters, triangles: l.triangles })),
    requiredWebGlVersion: v.requiredWebGlVersion, transferBytes: v.transferBytes,
    estimatedTextureBytes: v.estimatedTextureBytes, triangles: v.triangles, drawCalls: v.drawCalls
  };
}

function scene(v: BusinessSceneDescriptor): BusinessSceneDescriptor {
  return {
    sceneKey: v.sceneKey, kind: v.kind, units: v.units, upAxis: v.upAxis, frontAxis: v.frontAxis, origin: v.origin,
    bounds: bounds(v.bounds), variants: { economy: variant(v.variants.economy), standard: variant(v.variants.standard) },
    navigation: {
      spawn: viewpoint(v.navigation.spawn), exit: viewpoint(v.navigation.exit),
      viewpoints: v.navigation.viewpoints.map(viewpoint), walkableBounds: v.navigation.walkableBounds.map(bounds),
      collisionBounds: v.navigation.collisionBounds.map(bounds)
    },
    interactions: v.interactions.map(i => ({
      key: i.key, label: i.label, position: vector(i.position),
      action: i.action.role === 'inspectScene' ? { role: i.action.role, viewpointKey: i.action.viewpointKey } : { role: i.action.role }
    })),
    branding: {
      signNode: v.branding.signNode, logoNode: v.branding.logoNode,
      colorMaterialNames: v.branding.colorMaterialNames.map(n => n), maxSignCharacters: v.branding.maxSignCharacters
    },
    lightmaps: v.lightmaps.map(l => ({ materialName: l.materialName, textureAssetKey: l.textureAssetKey, texCoord: l.texCoord, intensity: l.intensity }))
  };
}

const recipe = (v: BusinessStyleRecipe): BusinessStyleRecipe => ({
  recipeKey: v.recipeKey, materialNames: v.materialNames.map(n => n), assetKeys: v.assetKeys.map(k => k)
});

function profile(v: BusinessCatalogProfile): BusinessCatalogProfile {
  return {
    profileKey: v.profileKey, editorialRevision: v.editorialRevision, representationKind: v.representationKind,
    status: v.status, facadeSceneKey: v.facadeSceneKey, interiorSceneKey: v.interiorSceneKey, localExteriorSceneKey: v.localExteriorSceneKey,
    styleRecipes: { 0: recipe(v.styleRecipes[0]), 1: recipe(v.styleRecipes[1]), 2: recipe(v.styleRecipes[2]), 3: recipe(v.styleRecipes[3]), 4: recipe(v.styleRecipes[4]) },
    coverage: {
      exteriorRenderAssetKey: v.coverage.exteriorRenderAssetKey, interiorRenderAssetKey: v.coverage.interiorRenderAssetKey,
      attributionAssetKeys: v.coverage.attributionAssetKeys.map(k => k)
    }
  };
}

function asset(v: BusinessAssetDescriptor): BusinessAssetDescriptor {
  return {
    assetKey: v.assetKey, kind: v.kind, path: v.path, mimeType: v.mimeType, sha256: v.sha256,
    encodedBytes: v.encodedBytes, estimatedDecodedBytes: v.estimatedDecodedBytes,
    dependencyAssetKeys: v.dependencyAssetKeys.map(k => k),
    gltf: v.gltf === null ? null : {
      requiredExtensions: v.gltf.requiredExtensions.map(e => e), allowedExtensions: v.gltf.allowedExtensions.map(e => e),
      embeddedImageMimeTypes: v.gltf.embeddedImageMimeTypes.map(m => m)
    },
    textures: v.textures.map(t => ({
      key: t.key, width: t.width, height: t.height, mipLevels: t.mipLevels, layers: t.layers,
      colorSpace: t.colorSpace, estimatedDecodedBytes: t.estimatedDecodedBytes
    }))
  };
}

export function projectBusinessCatalogManifest(input: unknown): ContractProjection<BusinessCatalogManifest> {
  return project<BusinessCatalogManifest>(input, 'manifest', validateBusinessCatalogManifest, v => ({
    schemaVersion: v.schemaVersion, catalogVersion: v.catalogVersion,
    renderer: { engine: v.renderer.engine, revision: v.renderer.revision, minContractVersion: v.renderer.minContractVersion },
    profiles: v.profiles.map(profile), scenes: v.scenes.map(scene), assets: v.assets.map(asset)
  }));
}

function validateResolvedRequest(input: unknown): ContractViolation[] {
  const out: ContractViolation[] = [];
  const add = (code: string) => out.push({ code, path: 'sceneRequest', message: 'Invalid resolved request structure' });
  const record = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);
  if (!record(input)) { add('request.type'); return out; }
  const kind = input['kind'];
  if (kind !== 'accepted' && kind !== 'neutral-global') { add('request.kind'); return out; }
  const value = kind === 'accepted' ? input['request'] : input;
  if (!record(value)) { add('request.type'); return out; }
  if (typeof value['scopeId'] !== 'string' || !value['scopeId'].trim()) add('request.scope');
  if (!Number.isSafeInteger(value['generation']) || (value['generation'] as number) < 0) add('request.generation');
  if (!(BUSINESS_QUALITIES as readonly unknown[]).includes(value['quality'])) add('request.quality');
  if (!isContractKey(value['sceneKey'])) add('request.scene-key');
  if (kind === 'accepted') {
    out.push(...validateVisualProfileRef(value['visualProfile']));
    if (typeof value['slug'] !== 'string' || !value['slug'].trim()) add('request.slug');
    if (!(FACADE_THEMES as readonly unknown[]).includes(value['facadeTheme'])) add('request.theme');
  } else if (!isValidCatalogVersionToken(value['catalogVersion'])) add('request.catalog-version');
  return out;
}

/** Does not select a fallback, attest acceptance, approve a scene, or grant G1. */
export function projectResolvedBusinessSceneRequest(input: unknown): ContractProjection<ResolvedBusinessSceneRequest> {
  return project<ResolvedBusinessSceneRequest>(input, 'sceneRequest', validateResolvedRequest, v => {
    if (v.kind === 'neutral-global') return {
      kind: v.kind, sceneKey: v.sceneKey, catalogVersion: v.catalogVersion,
      quality: v.quality, scopeId: v.scopeId, generation: v.generation
    };
    const r: BusinessSceneRequest = v.request;
    return { kind: v.kind, request: {
      scopeId: r.scopeId, generation: r.generation, slug: r.slug, visualProfile: reference(r.visualProfile),
      facadeTheme: r.facadeTheme, quality: r.quality, sceneKey: r.sceneKey
    } };
  });
}
