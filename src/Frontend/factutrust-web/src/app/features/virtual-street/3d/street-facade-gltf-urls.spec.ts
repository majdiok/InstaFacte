import { FACADE_GLB_FILENAMES, facadeThemeToFilename } from './street-facade-gltf-urls';

describe('street-facade-gltf-urls', () => {
  it('maps FacadeTheme indices 0–4 to stable filenames', () => {
    expect(FACADE_GLB_FILENAMES.length).toBe(5);
    expect(facadeThemeToFilename(0)).toBe('classic.glb');
    expect(facadeThemeToFilename(1)).toBe('modern.glb');
    expect(facadeThemeToFilename(2)).toBe('vintage.glb');
    expect(facadeThemeToFilename(3)).toBe('minimal.glb');
    expect(facadeThemeToFilename(4)).toBe('artisan.glb');
  });

  it('clamps out-of-range theme values', () => {
    expect(facadeThemeToFilename(-5)).toBe('classic.glb');
    expect(facadeThemeToFilename(99)).toBe('artisan.glb');
    expect(facadeThemeToFilename(Number.NaN)).toBe('classic.glb');
  });
});
