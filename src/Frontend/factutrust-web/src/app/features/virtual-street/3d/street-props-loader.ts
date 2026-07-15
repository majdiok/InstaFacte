import type { Group } from 'three';
import type { GLTF } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { vsEnv } from './street-env';
import { getSharedGltfLoader } from './street-facade-gltf';

export type PropName =
  | 'planter'
  | 'lantern'
  | 'door-handle'
  | 'address-plate'
  | 'window-prop-bottle'
  | 'window-prop-box'
  | 'shop-sign-bracket';

const propByName = new Map<PropName, Promise<Group | null>>();

export function getPropUrl(name: PropName): string {
  const v = vsEnv.storefrontGltfFacadesVersion;
  return `/assets/virtual-street/props/${name}.glb?v=${encodeURIComponent(v)}`;
}

/**
 * Loads (and caches) a prop GLB template by short name. Returns `null` on network
 * failure so callers can skip the prop instead of crashing the scene mount.
 *
 * Caching mirrors `loadFacadeTemplate`: per-name `Map<name, Promise<Group|null>>`,
 * concurrent callers share the in-flight promise.
 */
export async function loadPropTemplate(name: PropName): Promise<Group | null> {
  if (!vsEnv.storefrontPropsEnabled) return null;
  const cached = propByName.get(name);
  if (cached) return cached;

  const p = (async (): Promise<Group | null> => {
    try {
      const loader = await getSharedGltfLoader();
      const gltf: GLTF = await loader.loadAsync(getPropUrl(name));
      return gltf.scene as Group;
    } catch {
      return null;
    }
  })();

  propByName.set(name, p);
  return p;
}

/**
 * Drops cached prop scenes. Templates share textures with cloned instances, so
 * mesh-level dispose still happens via DisposableRegistry on each scene tear-down.
 */
export function disposePropTemplateCache(): void {
  propByName.clear();
}

/** Test-only: introspection. */
export function propTemplateCacheSize(): number {
  return propByName.size;
}
