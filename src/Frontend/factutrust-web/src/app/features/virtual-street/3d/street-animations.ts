import type { Group, Object3D, OrthographicCamera, PerspectiveCamera, PointLight } from 'three';
import type { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

const HOVER_AMPLITUDE = 0.06;
const HOVER_FREQUENCY = 4.0;
const FLICKER_AMPLITUDE = 0.08;
const FLICKER_FREQ_A = 7.3;
const FLICKER_FREQ_B = 2.1;
const SWAY_AMPLITUDE = 0.012;
const SWAY_FREQUENCY = 1.6;

/**
 * Hovers a storefront group with a smooth sine bob. Uses `userData.baseY` as the
 * resting Y so calls remain idempotent. When `group` is null, no-op.
 */
export function tickHoverBounce(group: Group | null, t: number): void {
  if (!group) return;
  const ud = group.userData as { baseY?: number };
  if (typeof ud.baseY !== 'number') ud.baseY = group.position.y;
  group.position.y = ud.baseY + Math.sin(t * HOVER_FREQUENCY) * HOVER_AMPLITUDE;
}

/** Releases a previously hovered group back to its resting position. */
export function releaseHoverBounce(group: Group | null): void {
  if (!group) return;
  const ud = group.userData as { baseY?: number };
  if (typeof ud.baseY === 'number') group.position.y = ud.baseY;
}

/**
 * Pseudo-Perlin via two summed sines per lamp index. Reads `userData.baseIntensity`
 * and writes `light.intensity`. Caps amplitude so the scene never gets dark.
 */
export function tickLanternFlicker(lights: readonly PointLight[], t: number): void {
  for (let i = 0; i < lights.length; i++) {
    const l = lights[i]!;
    const base = (l.userData['baseIntensity'] as number | undefined) ?? l.intensity;
    if (typeof l.userData['baseIntensity'] !== 'number') l.userData['baseIntensity'] = base;
    const a = Math.sin(t * FLICKER_FREQ_A + i);
    const b = Math.sin(t * FLICKER_FREQ_B + i * 0.7);
    const factor = 1 - FLICKER_AMPLITUDE + FLICKER_AMPLITUDE * ((a + b) / 2 + 1) * 0.5;
    l.intensity = base * factor;
  }
}

/**
 * Subtle awning sway around the X axis. Stores the rest rotation in
 * `userData.baseRotX`. Skips disposed/orphan meshes (no parent) so the array can
 * outlive a façade swap without manual pruning.
 */
export function tickAwningSway(meshes: readonly Object3D[], t: number): void {
  for (const m of meshes) {
    if (!m.parent) continue;
    const ud = m.userData as { baseRotX?: number };
    if (typeof ud.baseRotX !== 'number') ud.baseRotX = m.rotation.x;
    m.rotation.x = ud.baseRotX + Math.sin(t * SWAY_FREQUENCY) * SWAY_AMPLITUDE;
  }
}

/**
 * Cinematic ease-out cubic camera dolly. Resolves cleanly if `isDisposed()` returns
 * true mid-flight (early-return on the next animation frame). Honors visibility
 * indirectly via the host render loop's `requestAnimationFrame` cadence.
 */
export function runCinematicIntro(
  camera: PerspectiveCamera | OrthographicCamera,
  controls: OrbitControls,
  durationMs: number,
  isDisposed: () => boolean,
  startPosition: { x: number; y: number; z: number } = { x: 0, y: 6.4, z: 19 }
): Promise<void> {
  return new Promise(resolve => {
    if (isDisposed()) {
      resolve();
      return;
    }
    const endX = camera.position.x;
    const endY = camera.position.y;
    const endZ = camera.position.z;
    const startMs = performance.now();
    const easeOutCubic = (u: number): number => 1 - Math.pow(1 - u, 3);

    const step = (): void => {
      if (isDisposed()) {
        resolve();
        return;
      }
      const u = Math.min(1, (performance.now() - startMs) / durationMs);
      const e = easeOutCubic(u);
      camera.position.x = startPosition.x + (endX - startPosition.x) * e;
      camera.position.y = startPosition.y + (endY - startPosition.y) * e;
      camera.position.z = startPosition.z + (endZ - startPosition.z) * e;
      controls.update();
      if (u < 1) {
        requestAnimationFrame(step);
      } else {
        resolve();
      }
    };
    camera.position.set(startPosition.x, startPosition.y, startPosition.z);
    controls.update();
    requestAnimationFrame(step);
  });
}
