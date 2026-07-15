import { vsEnv } from './street-env';

describe('vsEnv (feature-flag helper)', () => {
  it('exposes every storefront* flag as a boolean getter', () => {
    expect(typeof vsEnv.storefrontEnabled).toBe('boolean');
    expect(typeof vsEnv.storefrontGltfFacades).toBe('boolean');
    expect(typeof vsEnv.storefrontGltfDraco).toBe('boolean');
    expect(typeof vsEnv.storefrontGltfMeshopt).toBe('boolean');
    expect(typeof vsEnv.storefrontGltfDebugLog).toBe('boolean');
    expect(typeof vsEnv.storefrontPbrEnvironment).toBe('boolean');
    expect(typeof vsEnv.storefrontPropsEnabled).toBe('boolean');
    expect(typeof vsEnv.storefrontPbrTexturesEnabled).toBe('boolean');
    expect(typeof vsEnv.storefrontStreetLampsEnabled).toBe('boolean');
    expect(typeof vsEnv.storefrontPostFxEnabled).toBe('boolean');
    expect(typeof vsEnv.storefrontMicroAnimationsEnabled).toBe('boolean');
    expect(typeof vsEnv.storefrontCinematicIntro).toBe('boolean');
    expect(typeof vsEnv.storefrontProceduralRichEnabled).toBe('boolean');
    expect(typeof vsEnv.production).toBe('boolean');
  });

  it('GLB version is a non-empty string', () => {
    expect(typeof vsEnv.storefrontGltfFacadesVersion).toBe('string');
    expect(vsEnv.storefrontGltfFacadesVersion.length).toBeGreaterThan(0);
  });
});
