import * as THREE from 'three';
import {
  releaseHoverBounce,
  tickAwningSway,
  tickHoverBounce,
  tickLanternFlicker
} from './street-animations';

describe('street-animations', () => {
  it('tickHoverBounce records baseY on first call and bounces around it', () => {
    const g = new THREE.Group();
    g.position.y = 1.5;
    tickHoverBounce(g, 0);
    expect(g.userData['baseY']).toBe(1.5);
    expect(g.position.y).toBeCloseTo(1.5, 5);

    tickHoverBounce(g, Math.PI / (2 * 4));
    expect(g.position.y).toBeGreaterThan(1.5);
    expect(g.position.y).toBeLessThanOrEqual(1.5 + 0.06 + 1e-6);
  });

  it('tickHoverBounce on null group is a no-op', () => {
    expect(() => tickHoverBounce(null, 1)).not.toThrow();
  });

  it('releaseHoverBounce restores baseY exactly', () => {
    const g = new THREE.Group();
    g.position.y = 2;
    tickHoverBounce(g, 1.234);
    expect(g.position.y).not.toBe(2);
    releaseHoverBounce(g);
    expect(g.position.y).toBe(2);
  });

  it('tickLanternFlicker stays within ±FLICKER_AMPLITUDE around base intensity', () => {
    const lights = [new THREE.PointLight(0xffd9a8, 1.6, 11, 2)];
    for (let t = 0; t < 6; t += 0.05) {
      tickLanternFlicker(lights, t);
      expect(lights[0]!.intensity).toBeGreaterThan(1.6 * 0.85);
      expect(lights[0]!.intensity).toBeLessThan(1.6 * 1.05);
    }
  });

  it('tickAwningSway skips orphan meshes (no parent)', () => {
    const orphan = new THREE.Mesh();
    orphan.rotation.x = 0.5;
    expect(() => tickAwningSway([orphan], 1.0)).not.toThrow();
    expect(orphan.rotation.x).toBe(0.5);
  });

  it('tickAwningSway oscillates parented meshes around baseRotX', () => {
    const parent = new THREE.Group();
    const m = new THREE.Mesh();
    m.rotation.x = -0.18;
    parent.add(m);
    tickAwningSway([m], 0);
    expect(m.userData['baseRotX']).toBe(-0.18);
    tickAwningSway([m], Math.PI / (2 * 1.6));
    expect(m.rotation.x).toBeGreaterThan(-0.18);
    expect(m.rotation.x).toBeLessThan(-0.18 + 0.013);
  });
});
