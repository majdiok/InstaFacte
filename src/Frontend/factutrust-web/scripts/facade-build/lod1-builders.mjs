import * as THREE from 'three';
import { palette } from './palette.mjs';

/**
 * Far-distance silhouette. Hard-capped at <780 triangles by the validator.
 * Adds a simplified door volume, awning slab, and lantern post so the LOD1 shape
 * stays close to the high-detail mesh — no jarring transition when LOD switches.
 */
export function buildStorefrontLod1(t, style) {
  const { w, h, d } = t;
  const c = palette(style);
  const group = new THREE.Group();

  const shell = new THREE.Mesh(
    new THREE.BoxGeometry(w * 0.9, h * 0.86, d * 0.52),
    new THREE.MeshStandardMaterial({ color: c.wall, roughness: 0.88, metalness: 0.05 })
  );
  shell.position.set(0, h * 0.43, -d * 0.04);
  shell.castShadow = true;
  shell.receiveShadow = true;
  group.add(shell);

  const curb = new THREE.Mesh(
    new THREE.BoxGeometry(w * 1.02, 0.14, 0.22),
    new THREE.MeshStandardMaterial({ color: c.trim, roughness: 0.75, metalness: 0.12 })
  );
  curb.position.set(0, 0.07, d * 0.26);
  curb.castShadow = true;
  curb.receiveShadow = true;
  group.add(curb);

  const darkWin = new THREE.Mesh(
    new THREE.PlaneGeometry(w * 0.45, h * 0.32),
    new THREE.MeshStandardMaterial({ color: 0x020617, roughness: 1, metalness: 0 })
  );
  darkWin.position.set(0, h * 0.42, d * 0.22);
  group.add(darkWin);

  // Recessed door volume (named so anim ticks see something at LOD1)
  const door = new THREE.Mesh(
    new THREE.BoxGeometry(w * 0.18, h * 0.36, 0.06),
    new THREE.MeshStandardMaterial({ color: 0x020617, roughness: 0.95, metalness: 0 })
  );
  door.name = 'Door_Leaf';
  door.position.set(0, h * 0.2, d * 0.22);
  group.add(door);

  // Awning slab silhouette — keeps the named mesh so tickAwningSway has a target.
  // 1 tri × 2 = 12 tris — within budget.
  const awning = new THREE.Mesh(
    new THREE.BoxGeometry(w * 0.84, 0.05, 0.42),
    new THREE.MeshStandardMaterial({ color: c.awning ?? c.trim, roughness: 0.85, metalness: 0.05 })
  );
  awning.name = 'Awning_Canopy';
  awning.position.set(0, h * 0.66, d * 0.22);
  awning.rotation.x = -0.12;
  awning.userData.baseRotX = -0.12;
  awning.castShadow = true;
  group.add(awning);

  // Cornice cap to break the silhouette top
  const cornice = new THREE.Mesh(
    new THREE.BoxGeometry(w * 1.0, 0.1, d * 0.5),
    new THREE.MeshStandardMaterial({ color: c.trim, roughness: 0.6, metalness: 0.18 })
  );
  cornice.position.set(0, h * 0.86 + 0.05, -d * 0.04);
  cornice.castShadow = true;
  group.add(cornice);

  // Mini-lamp post next to the door — keeps `Lantern_Housing` named for runtime checks.
  // CylinderGeometry(8 segments) ≈ 32 tris + sphere(8,4) ≈ 64 tris.
  const post = new THREE.Mesh(
    new THREE.CylinderGeometry(0.05, 0.05, h * 0.5, 6),
    new THREE.MeshStandardMaterial({ color: 0x111111, roughness: 0.4, metalness: 0.85 })
  );
  post.position.set(w * 0.42, h * 0.25, d * 0.3);
  post.castShadow = true;
  group.add(post);

  const lantern = new THREE.Mesh(
    new THREE.SphereGeometry(0.12, 6, 4),
    new THREE.MeshStandardMaterial({
      color: 0x111111,
      roughness: 0.35,
      metalness: 0.78,
      emissive: 0x553311,
      emissiveIntensity: 0.5
    })
  );
  lantern.name = 'Lantern_Housing';
  lantern.position.set(w * 0.42, h * 0.5, d * 0.3);
  lantern.castShadow = false;
  group.add(lantern);

  return group;
}
