/** Mirrors backend FacadeTheme (int). */
export const FacadeTheme = {
  Classic: 0,
  Modern: 1,
  Vintage: 2,
  Minimal: 3,
  Artisan: 4
} as const;

/** World scale vs legacy (~14% larger modules for readability in the viewport). */
const S = 1.14;

export interface FacadeVisualPreset {
  bodyHeight: number;
  bodyDepth: number;
  bodyWidth: number;
  signBandHeight: number;
  glassTransmission: number;
  glassRoughness: number;
  wallRoughness: number;
  metalness: number;
  corniceScale: number;
}

const defaultPreset: FacadeVisualPreset = {
  bodyHeight: 3.8 * S,
  bodyDepth: 2.15 * S,
  bodyWidth: 2.75 * S,
  signBandHeight: 0.42 * S,
  glassTransmission: 0.88,
  glassRoughness: 0.12,
  wallRoughness: 0.72,
  metalness: 0.08,
  corniceScale: 1
};

export function getFacadePreset(facadeTheme: number): FacadeVisualPreset {
  switch (facadeTheme) {
    case FacadeTheme.Classic:
      return {
        ...defaultPreset,
        bodyHeight: 4.1 * S,
        signBandHeight: 0.48 * S,
        glassTransmission: 0.82,
        wallRoughness: 0.78,
        corniceScale: 1.12
      };
    case FacadeTheme.Modern:
      return {
        ...defaultPreset,
        bodyHeight: 3.6 * S,
        glassTransmission: 0.93,
        glassRoughness: 0.06,
        wallRoughness: 0.55,
        metalness: 0.22,
        corniceScale: 0.92
      };
    case FacadeTheme.Vintage:
      return {
        ...defaultPreset,
        bodyHeight: 4.2 * S,
        signBandHeight: 0.52 * S,
        glassTransmission: 0.75,
        wallRoughness: 0.88,
        corniceScale: 1.18
      };
    case FacadeTheme.Minimal:
      return {
        ...defaultPreset,
        bodyHeight: 3.4 * S,
        signBandHeight: 0.32 * S,
        glassTransmission: 0.9,
        wallRoughness: 0.5,
        metalness: 0.15,
        corniceScale: 0.85
      };
    case FacadeTheme.Artisan:
      return {
        ...defaultPreset,
        bodyHeight: 3.95 * S,
        signBandHeight: 0.45 * S,
        glassTransmission: 0.8,
        wallRoughness: 0.82,
        corniceScale: 1.05
      };
    default:
      return { ...defaultPreset };
  }
}
