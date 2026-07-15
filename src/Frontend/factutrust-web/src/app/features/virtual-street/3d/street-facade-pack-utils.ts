import type { Group } from 'three';

/**
 * GLB may be legacy (single root `StorefrontRoot`) or packed (`FaçadePack` with
 * `StorefrontRoot` + optional `StorefrontRoot_LOD1` for distant LOD).
 */
export function detachStorefrontRootsFromGltfClone(clonedRoot: Group): {
  high: Group;
  lod1: Group | null;
  /** Empty pack left after detaching children — caller should dispose. */
  emptyPack: Group | null;
} {
  const namedLod = clonedRoot.getObjectByName('StorefrontRoot_LOD1') as Group | undefined;
  const namedHigh = clonedRoot.getObjectByName('StorefrontRoot') as Group | undefined;

  let high: Group;
  if (namedHigh && namedHigh !== clonedRoot) {
    high = namedHigh;
    high.removeFromParent();
  } else {
    high = clonedRoot;
  }

  let lod1: Group | null = null;
  if (namedLod && namedLod !== high) {
    lod1 = namedLod;
    lod1.removeFromParent();
  }

  let emptyPack: Group | null = null;
  if (clonedRoot !== high && clonedRoot.children.length === 0) {
    emptyPack = clonedRoot;
  }

  return { high, lod1, emptyPack };
}
