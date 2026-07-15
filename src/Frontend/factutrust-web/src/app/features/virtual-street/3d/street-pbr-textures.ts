import type { Material, Mesh, PlaneGeometry } from 'three';
import { vsEnv } from './street-env';
import { loadPbrSet, type PbrTextureSet } from './street-texture-cache';
import type { ThreeModule } from './street-facade.builder';

export type PbrSetName = 'plaster-wall' | 'metal-panel' | 'wood-trim' | 'concrete-sidewalk';

interface PbrSetUrls {
  base: string;
  normal?: string;
  rough?: string;
  ao?: string;
}

function setUrls(name: PbrSetName): PbrSetUrls {
  const v = vsEnv.storefrontGltfFacadesVersion;
  const root = `/assets/virtual-street/textures/${name}`;
  const q = `?v=${encodeURIComponent(v)}`;
  return {
    base: `${root}/baseColor.png${q}`,
    normal: `${root}/normal.png${q}`,
    rough: `${root}/roughness.png${q}`,
    ao: `${root}/ao.png${q}`
  };
}

/**
 * Best-effort PBR set loader. Resolves with `null` on failure so callers can
 * keep the existing fallback material (e.g. canvas-tiled ground).
 */
export async function tryLoadPbrSet(
  three: ThreeModule,
  name: PbrSetName,
  anisotropy: number
): Promise<PbrTextureSet | null> {
  if (!vsEnv.storefrontPbrTexturesEnabled) return null;
  try {
    return await loadPbrSet(three, name, setUrls(name), anisotropy);
  } catch {
    return null;
  }
}

/**
 * Applies a PBR set to a MeshStandardMaterial in place. Disposes the previous
 * `map` to avoid GPU leaks and turns on AO via the existing `aoMap` slot.
 */
export function applyPbrSetToStandardMaterial(
  mat: Material & {
    map?: { dispose(): void } | null;
    normalMap?: unknown;
    roughnessMap?: unknown;
    aoMap?: unknown;
    needsUpdate?: boolean;
  },
  set: PbrTextureSet,
  repeatU: number,
  repeatV: number
): void {
  set.map.repeat.set(repeatU, repeatV);
  set.normalMap?.repeat.set(repeatU, repeatV);
  set.roughnessMap?.repeat.set(repeatU, repeatV);
  set.aoMap?.repeat.set(repeatU, repeatV);

  if (mat.map && 'dispose' in mat.map) mat.map.dispose();
  mat.map = set.map;
  if (set.normalMap) (mat as { normalMap?: unknown }).normalMap = set.normalMap;
  if (set.roughnessMap) (mat as { roughnessMap?: unknown }).roughnessMap = set.roughnessMap;
  if (set.aoMap) (mat as { aoMap?: unknown }).aoMap = set.aoMap;
  if ('needsUpdate' in mat) mat.needsUpdate = true;
}

/** Ensures the geometry has a `uv2` attribute (required for AO sampling). */
export function ensureUv2(mesh: Mesh): void {
  const geo = mesh.geometry as PlaneGeometry & { attributes?: Record<string, unknown> };
  const attrs = geo.attributes;
  if (!attrs?.['uv'] || attrs['uv2']) return;
  geo.setAttribute('uv2', (attrs['uv'] as { clone(): unknown }).clone() as never);
}
