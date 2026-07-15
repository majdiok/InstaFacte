import * as THREE from 'three';
import { detachStorefrontRootsFromGltfClone } from './street-facade-pack-utils';

describe('street-facade-pack-utils', () => {
  it('legacy single StorefrontRoot: high is entire clone, no LOD', () => {
    const root = new THREE.Group();
    root.name = 'StorefrontRoot';
    const sign = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), new THREE.MeshBasicMaterial());
    sign.name = 'Sign_Plane';
    root.add(sign);

    const { high, lod1, emptyPack } = detachStorefrontRootsFromGltfClone(root);
    expect(high).toBe(root);
    expect(lod1).toBeNull();
    expect(emptyPack).toBeNull();
  });

  it('FaçadePack: detaches StorefrontRoot and StorefrontRoot_LOD1, returns empty pack', () => {
    const pack = new THREE.Group();
    pack.name = 'FaçadePack';
    const high = new THREE.Group();
    high.name = 'StorefrontRoot';
    high.add(new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshBasicMaterial()));
    const lod = new THREE.Group();
    lod.name = 'StorefrontRoot_LOD1';
    lod.add(new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshBasicMaterial()));
    pack.add(high);
    pack.add(lod);

    const out = detachStorefrontRootsFromGltfClone(pack);
    expect(out.high).toBe(high);
    expect(out.lod1).toBe(lod);
    expect(out.emptyPack).toBe(pack);
    expect(pack.children.length).toBe(0);
  });
});
