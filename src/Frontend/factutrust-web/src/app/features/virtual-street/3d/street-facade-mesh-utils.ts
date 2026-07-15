import type { Group, LOD, Mesh, Object3D } from 'three';

/** Wrapper root contains `__procedural`, flat `__gltf`, or `__gltfLod` (THREE.LOD). */
export function resolveFacadeVisualRoot(wrapper: Group): Group {
  const flat = wrapper.getObjectByName('__gltf') as Group | undefined;
  if (flat) return flat;
  const lod = wrapper.getObjectByName('__gltfLod') as LOD | undefined;
  if (lod?.levels?.length) {
    return lod.levels[0]!.object as Group;
  }
  const proc = wrapper.getObjectByName('__procedural') as Group | undefined;
  return proc ?? wrapper;
}

export function findMeshByNames(root: Object3D, names: readonly string[]): Mesh | undefined {
  let hit: Mesh | undefined;
  root.traverse(obj => {
    if (hit) return;
    if (names.includes(obj.name) && (obj as Mesh).isMesh) hit = obj as Mesh;
  });
  return hit;
}

export const SIGN_MESH_NAMES = ['facadeSign', 'Sign_Plane'] as const;
export const LOGO_MESH_NAMES = ['facadeLogo', 'Logo_Plane'] as const;
