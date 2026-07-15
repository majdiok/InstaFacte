import type { Group } from 'three';
import type { GLTF } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { vsEnv } from './street-env';
import { facadeThemeToFilename } from './street-facade-gltf-urls';
import type { ThreeModule } from './street-facade.builder';

const templateByTheme = new Map<number, GLTF>();
const inflight = new Map<number, Promise<GLTF | null>>();
let sharedLoaderPromise: Promise<GLTFLoader> | null = null;

export function getFacadeGlbUrl(theme: number): string | null {
  if (!vsEnv.storefrontGltfFacades) return null;
  const file = facadeThemeToFilename(theme);
  const v = vsEnv.storefrontGltfFacadesVersion;
  return `/assets/virtual-street/facades/${file}?v=${encodeURIComponent(v)}`;
}

/**
 * Returns a single GLTFLoader configured with optional Draco / Meshopt decoders.
 * Reused by `loadFacadeTemplate` and `loadPropTemplate` so the decoder setup
 * (and any decoder fetch) only runs once per page lifetime.
 */
export function getSharedGltfLoader(): Promise<GLTFLoader> {
  if (sharedLoaderPromise) return sharedLoaderPromise;
  sharedLoaderPromise = (async (): Promise<GLTFLoader> => {
    const loader = new GLTFLoader();
    if (vsEnv.storefrontGltfDraco) {
      try {
        const { DRACOLoader } = await import('three/examples/jsm/loaders/DRACOLoader.js');
        const draco = new DRACOLoader();
        draco.setDecoderPath('/assets/vendor/draco/gltf/');
        loader.setDRACOLoader(draco);
      } catch {
        /* décodeur Draco optionnel */
      }
    }
    if (vsEnv.storefrontGltfMeshopt) {
      try {
        const { MeshoptDecoder } = await import('three/examples/jsm/libs/meshopt_decoder.module.js');
        await MeshoptDecoder.ready;
        loader.setMeshoptDecoder(MeshoptDecoder);
      } catch {
        /* décodeur Meshopt optionnel */
      }
    }
    return loader;
  })();
  return sharedLoaderPromise;
}

export function cloneGltfSceneForInstance(three: ThreeModule, gltf: GLTF): Group {
  const root = gltf.scene.clone(true) as Group;
  root.traverse(node => {
    const m = node as import('three').Mesh;
    if (m.isMesh && m.material) {
      if (Array.isArray(m.material)) m.material = m.material.map(x => x.clone());
      else m.material = m.material.clone();
    }
  });
  return root;
}

/**
 * Loads and caches the GLTF template per theme (shared textures across instances).
 */
export async function loadFacadeTemplate(theme: number): Promise<GLTF | null> {
  const url = getFacadeGlbUrl(theme);
  if (!url) return null;
  const cached = templateByTheme.get(theme);
  if (cached) return cached;

  const pending = inflight.get(theme);
  if (pending) return pending;

  const p = (async (): Promise<GLTF | null> => {
    const loader = await getSharedGltfLoader();
    try {
      const gltf = await loader.loadAsync(url);
      templateByTheme.set(theme, gltf);
      return gltf;
    } catch {
      return null;
    } finally {
      inflight.delete(theme);
    }
  })();

  inflight.set(theme, p);
  return p;
}

/**
 * Drops cached GLTF parses (templates share GPU textures with live clones; do not dispose textures here).
 */
export function disposeGltfTemplateCache(): void {
  templateByTheme.clear();
  inflight.clear();
  sharedLoaderPromise = null;
}
