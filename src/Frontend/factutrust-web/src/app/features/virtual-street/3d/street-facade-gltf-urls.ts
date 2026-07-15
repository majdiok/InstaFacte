/** Filenames aligned with backend FacadeTheme enum order (0–4). */
export const FACADE_GLB_FILENAMES = [
  'classic.glb',
  'modern.glb',
  'vintage.glb',
  'minimal.glb',
  'artisan.glb'
] as const;

export function facadeThemeToFilename(theme: number): string {
  const t = Number.isFinite(theme) ? theme : 0;
  const i = Math.max(0, Math.min(FACADE_GLB_FILENAMES.length - 1, Math.floor(t)));
  return FACADE_GLB_FILENAMES[i]!;
}
