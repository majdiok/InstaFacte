import { computeStreetFraming } from './street-camera-framing';

describe('computeStreetFraming', () => {
  it('matches the n=1 anchor exactly', () => {
    const p = computeStreetFraming(1, 19);
    expect(p.position).toEqual({ x: 0, y: 5.05, z: 12.2 });
    expect(p.minDistance).toBe(7);
    expect(p.fov).toBe(44);
    expect(p.azimuth).toBe(0.55);
  });

  it('matches the n=24 anchor exactly when arcRadius does not push thresholds', () => {
    const p = computeStreetFraming(24, 22);
    expect(p.position.z).toBeCloseTo(24, 5);
    expect(p.fov).toBeCloseTo(54, 5);
    expect(p.azimuth).toBeCloseTo(0.95, 5);
  });

  it('clamps to nearest anchor for n outside the table', () => {
    const lo = computeStreetFraming(0, 19);
    const hi = computeStreetFraming(99, 22);
    expect(lo.position.z).toBe(12.2);
    expect(hi.position.z).toBe(24);
  });

  it('camera distance grows monotonically with n', () => {
    const z1 = computeStreetFraming(1, 19).position.z;
    const z5 = computeStreetFraming(5, 19).position.z;
    const z12 = computeStreetFraming(12, 22).position.z;
    const z24 = computeStreetFraming(24, 26).position.z;
    expect(z5).toBeGreaterThan(z1);
    expect(z12).toBeGreaterThan(z5);
    expect(z24).toBeGreaterThan(z12);
  });

  it('LOD swap distance is at least 1.55 × arcRadius', () => {
    const p = computeStreetFraming(10, 30);
    expect(p.lodSwapDistance).toBeGreaterThanOrEqual(30 * 1.55);
  });

  it('shadow ortho half-size is at least 0.95 × arcRadius', () => {
    const p = computeStreetFraming(10, 28);
    expect(p.shadowOrthoHalfSize).toBeGreaterThanOrEqual(28 * 0.95);
  });

  it('max distance is at least 1.4 × arcRadius', () => {
    const p = computeStreetFraming(10, 30);
    expect(p.maxDistance).toBeGreaterThanOrEqual(30 * 1.4);
  });

  it('FOV stays within 40-60 degrees', () => {
    for (let n = 1; n <= 30; n++) {
      const p = computeStreetFraming(n, 22);
      expect(p.fov).toBeGreaterThan(40);
      expect(p.fov).toBeLessThan(60);
    }
  });

  it('interpolates smoothly between anchors (no jumps)', () => {
    let prevZ = computeStreetFraming(1, 19).position.z;
    for (let n = 2; n <= 24; n++) {
      const z = computeStreetFraming(n, 22).position.z;
      expect(z - prevZ).toBeLessThan(2.5);
      prevZ = z;
    }
  });
});
