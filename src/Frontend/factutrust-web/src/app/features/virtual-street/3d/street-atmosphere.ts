import type { Group, InstancedMesh, Object3D, PointLight, Scene } from 'three';
import { vsEnv } from './street-env';
import type { DisposableRegistry } from './street-disposable-registry';
import type { ThreeModule } from './street-facade.builder';

export interface AtmosphereResult {
  lamps?: InstancedMesh;
  pointLights: PointLight[];
  awningMeshes: Object3D[];
}

const MAX_LAMPS = 24;
const MAX_SLABS = 60;

/**
 * Adds gently lit sidewalk slabs as one InstancedMesh along the storefront arc.
 * Layer 0 (visible). Receives shadows. Inserted only when the lamps flag is on.
 */
export function buildSidewalkSlabs(
  three: ThreeModule,
  streetRoot: Group,
  n: number,
  registry: DisposableRegistry,
  arcRadius: number
): void {
  if (!vsEnv.storefrontStreetLampsEnabled) return;
  const slabCount = Math.min(Math.max(n * 3, 6), MAX_SLABS);
  const slab = new three.BoxGeometry(2.0, 0.04, 1.6);
  const mat = new three.MeshStandardMaterial({
    color: 0x222b3a,
    roughness: 0.92,
    metalness: 0.04
  });
  const inst = new three.InstancedMesh(slab, mat, slabCount);
  inst.name = '__streetSidewalkSlabs';
  inst.receiveShadow = true;
  const dummy = new three.Object3D();
  for (let i = 0; i < slabCount; i++) {
    const t = -0.6 + (i / (slabCount - 1)) * 1.2;
    const x = arcRadius * Math.sin(t);
    const z = -arcRadius * Math.cos(t) + arcRadius * 0.88 + 1.8;
    dummy.position.set(x, 0.02, z);
    dummy.rotation.y = -t;
    dummy.updateMatrix();
    inst.setMatrixAt(i, dummy.matrix);
  }
  inst.instanceMatrix.needsUpdate = true;
  streetRoot.add(inst);
  registry.add({
    dispose() {
      slab.dispose();
      mat.dispose();
    }
  });
}

/**
 * One InstancedMesh for visible posts + per-instance warm PointLights for grounding.
 * Lights cast NO shadow (cost) and are capped at 14. Each light's distance/decay
 * keeps it affecting only ~3 storefronts so shader cost stays bounded.
 *
 * Async because it tries to merge the post geometry with arm + housing via
 * BufferGeometryUtils (dynamic import); falls back to the post alone if the
 * helper module fails to load.
 */
export async function buildStreetLamps(
  three: ThreeModule,
  streetRoot: Group,
  n: number,
  registry: DisposableRegistry,
  arcRadius: number
): Promise<{ instanced: InstancedMesh | null; pointLights: PointLight[] }> {
  if (!vsEnv.storefrontStreetLampsEnabled) return { instanced: null, pointLights: [] };
  const count = Math.min(Math.max(Math.floor(n / 2) + 1, 2), MAX_LAMPS);

  const post = new three.CylinderGeometry(0.06, 0.08, 3.2, 8);
  const arm = new three.BoxGeometry(0.05, 0.05, 0.6);
  arm.translate(0, 1.4, -0.3);
  const housing = new three.SphereGeometry(0.18, 8, 6);
  housing.translate(0, 1.55, -0.6);

  const lampMat = new three.MeshStandardMaterial({
    color: 0x111111,
    roughness: 0.45,
    metalness: 0.85,
    emissive: 0x553311,
    emissiveIntensity: 0.4
  });

  let merged: import('three').BufferGeometry = post;
  try {
    const utils = await import('three/examples/jsm/utils/BufferGeometryUtils.js');
    if (utils?.mergeGeometries) {
      const m = utils.mergeGeometries([post, arm, housing], false);
      if (m) merged = m;
    }
  } catch {
    /* fallback: lamp post alone is still readable */
  }
  const inst = new three.InstancedMesh(merged, lampMat, count);
  inst.name = '__streetLamps';
  inst.castShadow = false;

  const dummy = new three.Object3D();
  const lights: PointLight[] = [];
  for (let i = 0; i < count; i++) {
    const t = -0.6 + (i / Math.max(count - 1, 1)) * 1.2;
    const x = arcRadius * Math.sin(t);
    const z = -arcRadius * Math.cos(t) + arcRadius * 0.88 + 2.4;
    dummy.position.set(x, 1.6, z);
    dummy.rotation.y = -t;
    dummy.updateMatrix();
    inst.setMatrixAt(i, dummy.matrix);

    const light = new three.PointLight(0xffd9a8, 1.6, 11, 2);
    light.position.set(x, 3.1, z - 0.6);
    light.castShadow = false;
    light.userData['baseIntensity'] = 1.6;
    streetRoot.add(light);
    lights.push(light);
  }
  inst.instanceMatrix.needsUpdate = true;
  streetRoot.add(inst);

  registry.add({
    dispose() {
      merged.dispose();
      post.dispose();
      arm.dispose();
      housing.dispose();
      lampMat.dispose();
      for (const l of lights) {
        l.parent?.remove(l);
      }
    }
  });
  return { instanced: inst, pointLights: lights };
}

/**
 * Inverted SphereGeometry with a vertical canvas gradient for sky context. Layer 1
 * (existing backdrop layer — non-pickable). Replaces the flat dark scene.background
 * only after this is added; the original `Color` background remains the default.
 */
export function buildSkyGradient(three: ThreeModule, scene: Scene, registry: DisposableRegistry): void {
  if (!vsEnv.storefrontStreetLampsEnabled) return;
  const tile = 256;
  const canvas = document.createElement('canvas');
  canvas.width = 8;
  canvas.height = tile;
  const ctx = canvas.getContext('2d');
  if (ctx) {
    const g = ctx.createLinearGradient(0, 0, 0, tile);
    g.addColorStop(0, '#020617');
    g.addColorStop(0.55, '#0b1220');
    g.addColorStop(0.85, '#1e293b');
    g.addColorStop(1, '#3a2a14');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, 8, tile);
  }
  const tex = new three.CanvasTexture(canvas);
  tex.colorSpace = three.SRGBColorSpace;
  tex.wrapS = three.RepeatWrapping;
  tex.wrapT = three.ClampToEdgeWrapping;
  const geo = new three.SphereGeometry(60, 24, 12);
  const mat = new three.MeshBasicMaterial({
    map: tex,
    side: three.BackSide,
    fog: false,
    depthWrite: false
  });
  const sky = new three.Mesh(geo, mat);
  sky.name = '__streetSky';
  sky.layers.set(1);
  sky.renderOrder = -10;
  scene.add(sky);

  registry.add({
    dispose() {
      tex.dispose();
      mat.dispose();
      geo.dispose();
      sky.parent?.remove(sky);
    }
  });
}

/**
 * Slightly lifts the existing FogExp2 density to keep distant lampposts crisp
 * but still anchored. Idempotent — keeps current fog instance in place.
 */
export function tweakFog(three: ThreeModule, scene: Scene): void {
  if (!vsEnv.storefrontStreetLampsEnabled) return;
  const fog = scene.fog as { density?: number; isFogExp2?: boolean } | null;
  if (fog?.isFogExp2 && typeof fog.density === 'number') {
    fog.density = 0.018;
  }
}
