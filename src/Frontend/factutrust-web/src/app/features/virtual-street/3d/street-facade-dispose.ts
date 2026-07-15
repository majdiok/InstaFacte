import type { Light, Material, Mesh, Object3D, Texture } from 'three';

/** Recursively disposes geometries, materials, and common texture maps. */
export function deepDisposeObject3D(root: Object3D): void {
  root.traverse(node => {
    const mesh = node as Mesh & { isLight?: boolean };
    if (mesh.isLight) {
      const L = node as Light;
      L.dispose?.();
      return;
    }
    if (!mesh.isMesh) return;
    mesh.geometry?.dispose();
    const mat = mesh.material as Material | Material[] | undefined;
    if (!mat) return;
    const disposeMat = (m: Material) => {
      const anym = m as Material & {
        map?: Texture;
        normalMap?: Texture;
        roughnessMap?: Texture;
        metalnessMap?: Texture;
        emissiveMap?: Texture;
        aoMap?: Texture;
        envMap?: Texture;
        dispose: () => void;
      };
      anym.map?.dispose();
      anym.normalMap?.dispose();
      anym.roughnessMap?.dispose();
      anym.metalnessMap?.dispose();
      anym.emissiveMap?.dispose();
      anym.aoMap?.dispose();
      anym.envMap?.dispose();
      anym.dispose();
    };
    if (Array.isArray(mat)) mat.forEach(disposeMat);
    else disposeMat(mat);
  });
}
