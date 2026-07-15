import type { Texture } from 'three';
import type { ThreeModule } from './street-facade.builder';

export interface PbrTextureUrls {
  base: string;
  normal?: string;
  rough?: string;
  ao?: string;
}

export interface PbrTextureSet {
  map: Texture;
  normalMap?: Texture;
  roughnessMap?: Texture;
  aoMap?: Texture;
}

const setByKey = new Map<string, Promise<PbrTextureSet>>();

function loadOne(three: ThreeModule, url: string, isColor: boolean, anisotropy: number): Promise<Texture> {
  return new Promise((resolve, reject) => {
    const loader = new three.TextureLoader();
    loader.setCrossOrigin('anonymous');
    loader.load(
      url,
      tex => {
        tex.wrapS = three.RepeatWrapping;
        tex.wrapT = three.RepeatWrapping;
        if (isColor) {
          tex.colorSpace = three.SRGBColorSpace;
        } else if ('colorSpace' in tex) {
          (tex as Texture & { colorSpace: string }).colorSpace = three.NoColorSpace ?? three.LinearSRGBColorSpace;
        }
        if (anisotropy > 1) tex.anisotropy = anisotropy;
        tex.needsUpdate = true;
        resolve(tex);
      },
      undefined,
      err => reject(err)
    );
  });
}

/**
 * Loads (and caches) a PBR texture set tied to `key`. Concurrent callers for the same
 * key share a single in-flight `Promise` — no double network fetch, no double GPU upload.
 *
 * - `map` always uses sRGB color space.
 * - `normalMap` / `roughnessMap` / `aoMap` stay in linear color space.
 * - All maps use `RepeatWrapping`.
 */
export function loadPbrSet(
  three: ThreeModule,
  key: string,
  urls: PbrTextureUrls,
  anisotropy: number
): Promise<PbrTextureSet> {
  const cached = setByKey.get(key);
  if (cached) return cached;

  const p = (async (): Promise<PbrTextureSet> => {
    const map = await loadOne(three, urls.base, true, anisotropy);
    const normalMap = urls.normal ? await loadOne(three, urls.normal, false, anisotropy).catch(() => undefined) : undefined;
    const roughnessMap = urls.rough ? await loadOne(three, urls.rough, false, anisotropy).catch(() => undefined) : undefined;
    const aoMap = urls.ao ? await loadOne(three, urls.ao, false, anisotropy).catch(() => undefined) : undefined;
    return { map, normalMap, roughnessMap, aoMap };
  })();

  setByKey.set(key, p);
  p.catch(() => setByKey.delete(key));
  return p;
}

/**
 * Drains GPU textures held by the cache. Called from VirtualStreetScene.dispose()
 * after the per-instance DisposableRegistry has been emptied.
 */
export function disposePbrTextureCache(): void {
  for (const p of setByKey.values()) {
    p.then(set => {
      set.map.dispose();
      set.normalMap?.dispose();
      set.roughnessMap?.dispose();
      set.aoMap?.dispose();
    }).catch(() => {
      /* a rejected load has nothing to dispose */
    });
  }
  setByKey.clear();
}

/** Test-only / dev-only helper: peek into the cache size without exposing the map. */
export function pbrTextureCacheSize(): number {
  return setByKey.size;
}
