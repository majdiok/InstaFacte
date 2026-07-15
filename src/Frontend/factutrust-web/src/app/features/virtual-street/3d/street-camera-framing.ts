/**
 * Pure math: turns (storefront count, arc radius) into camera + OrbitControls parameters.
 *
 * Why a dedicated module: the legacy `applyStreetCameraFraming(n)` was hard-coded with two
 * cases (n=1 / n>1). It worked at n≤5 then broke as the arc grew (outer storefronts fell
 * outside the frustum and crossed the LOD threshold while still in main view). This module
 * smoothly interpolates camera state across four anchors and stays continuous over n.
 */

export interface StreetFramingParams {
  /** Camera world position. */
  position: { x: number; y: number; z: number };
  /** OrbitControls target (also camera lookAt). */
  target: { x: number; y: number; z: number };
  /** OrbitControls min distance. */
  minDistance: number;
  /** OrbitControls max distance. */
  maxDistance: number;
  /** Vertical field of view in degrees. */
  fov: number;
  /** Symmetric azimuth lock in radians. The control is `±azimuth`. */
  azimuth: number;
  /** Distance at which a high-detail StorefrontRoot LOD switches to LOD1. */
  lodSwapDistance: number;
  /** Half-extent of the directional key shadow camera ortho box. */
  shadowOrthoHalfSize: number;
  /** FogExp2 density to apply (kept gentle so distant lamps stay visible). */
  fogDensity: number;
}

interface Anchor {
  /** Reference storefront count. */
  n: number;
  position: { x: number; y: number; z: number };
  target: { x: number; y: number; z: number };
  minDistance: number;
  maxDistance: number;
  fov: number;
  azimuth: number;
  lodSwapDistance: number;
  shadowOrthoHalfSize: number;
  fogDensity: number;
}

/** Anchor table — interpolation happens between these four points. */
const ANCHORS: readonly Anchor[] = [
  {
    n: 1,
    position: { x: 0, y: 5.05, z: 12.2 },
    target: { x: 0, y: 2.42, z: 0 },
    minDistance: 7.0,
    maxDistance: 19,
    fov: 44,
    azimuth: 0.55,
    lodSwapDistance: 24,
    shadowOrthoHalfSize: 20,
    fogDensity: 0.018
  },
  {
    n: 5,
    position: { x: 0, y: 5.4, z: 14.5 },
    target: { x: 0, y: 2.5, z: 0 },
    minDistance: 8.2,
    maxDistance: 24,
    fov: 46,
    azimuth: 0.7,
    lodSwapDistance: 26,
    shadowOrthoHalfSize: 22,
    fogDensity: 0.018
  },
  {
    n: 12,
    position: { x: 0, y: 6.6, z: 18.5 },
    target: { x: 0, y: 2.85, z: 0 },
    minDistance: 9.5,
    maxDistance: 32,
    fov: 50,
    azimuth: 0.85,
    lodSwapDistance: 38,
    shadowOrthoHalfSize: 28,
    fogDensity: 0.014
  },
  {
    n: 24,
    position: { x: 0, y: 8.2, z: 24 },
    target: { x: 0, y: 3.2, z: 0 },
    minDistance: 11,
    maxDistance: 44,
    fov: 54,
    azimuth: 0.95,
    lodSwapDistance: 52,
    shadowOrthoHalfSize: 36,
    fogDensity: 0.011
  }
] as const;

function smoothstep(edge0: number, edge1: number, x: number): number {
  if (edge0 === edge1) return x < edge0 ? 0 : 1;
  const t = Math.min(1, Math.max(0, (x - edge0) / (edge1 - edge0)));
  return t * t * (3 - 2 * t);
}

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

function lerp3(
  a: { x: number; y: number; z: number },
  b: { x: number; y: number; z: number },
  t: number
): { x: number; y: number; z: number } {
  return { x: lerp(a.x, b.x, t), y: lerp(a.y, b.y, t), z: lerp(a.z, b.z, t) };
}

/**
 * Returns smoothly-interpolated camera + control parameters for the given storefront count.
 * Outside the anchor range the function clamps to the nearest anchor (no extrapolation),
 * so n=0 reuses n=1 and n=100 reuses n=24.
 *
 * `arcRadius` is consumed only to push `lodSwapDistance` up if the arc happens to be wider
 * than the table value — guarantees outer storefronts never cross into LOD1 silhouette
 * while still in main view.
 */
export function computeStreetFraming(n: number, arcRadius: number): StreetFramingParams {
  const safeN = Math.max(1, Math.floor(n));

  let lo = ANCHORS[0]!;
  let hi = ANCHORS[ANCHORS.length - 1]!;
  for (let i = 0; i < ANCHORS.length - 1; i++) {
    const cur = ANCHORS[i]!;
    const nxt = ANCHORS[i + 1]!;
    if (safeN >= cur.n && safeN <= nxt.n) {
      lo = cur;
      hi = nxt;
      break;
    }
    if (safeN < ANCHORS[0]!.n) {
      lo = hi = ANCHORS[0]!;
      break;
    }
    if (safeN > ANCHORS[ANCHORS.length - 1]!.n) {
      lo = hi = ANCHORS[ANCHORS.length - 1]!;
      break;
    }
  }

  const t = lo === hi ? 0 : smoothstep(lo.n, hi.n, safeN);

  const lodSwapDistance = Math.max(lerp(lo.lodSwapDistance, hi.lodSwapDistance, t), arcRadius * 1.55);

  return {
    position: lerp3(lo.position, hi.position, t),
    target: lerp3(lo.target, hi.target, t),
    minDistance: lerp(lo.minDistance, hi.minDistance, t),
    maxDistance: Math.max(lerp(lo.maxDistance, hi.maxDistance, t), arcRadius * 1.4),
    fov: lerp(lo.fov, hi.fov, t),
    azimuth: lerp(lo.azimuth, hi.azimuth, t),
    lodSwapDistance,
    shadowOrthoHalfSize: Math.max(lerp(lo.shadowOrthoHalfSize, hi.shadowOrthoHalfSize, t), arcRadius * 0.95),
    fogDensity: lerp(lo.fogDensity, hi.fogDensity, t)
  };
}
