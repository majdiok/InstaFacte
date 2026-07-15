import { environment } from '../../../../environments/environment';

type EnvFlags = typeof environment & Record<string, unknown>;

function readBool(key: string, fallback: boolean): boolean {
  const raw = (environment as EnvFlags)[key];
  return typeof raw === 'boolean' ? raw : fallback;
}

function readString(key: string, fallback: string): string {
  const raw = (environment as EnvFlags)[key];
  return typeof raw === 'string' && raw.length > 0 ? raw : fallback;
}

export const vsEnv = {
  get production(): boolean {
    return readBool('production', false);
  },
  get storefrontEnabled(): boolean {
    return readBool('storefrontEnabled', false);
  },
  get storefrontGltfFacades(): boolean {
    return readBool('storefrontGltfFacades', false);
  },
  get storefrontGltfFacadesVersion(): string {
    return readString('storefrontGltfFacadesVersion', '1');
  },
  get storefrontGltfDraco(): boolean {
    return readBool('storefrontGltfDraco', false);
  },
  get storefrontGltfMeshopt(): boolean {
    return readBool('storefrontGltfMeshopt', false);
  },
  get storefrontGltfDebugLog(): boolean {
    return readBool('storefrontGltfDebugLog', false);
  },
  get storefrontPbrEnvironment(): boolean {
    return readBool('storefrontPbrEnvironment', false);
  },
  get storefrontPropsEnabled(): boolean {
    return readBool('storefrontPropsEnabled', false);
  },
  get storefrontPbrTexturesEnabled(): boolean {
    return readBool('storefrontPbrTexturesEnabled', false);
  },
  get storefrontStreetLampsEnabled(): boolean {
    return readBool('storefrontStreetLampsEnabled', false);
  },
  get storefrontPostFxEnabled(): boolean {
    return readBool('storefrontPostFxEnabled', false);
  },
  get storefrontMicroAnimationsEnabled(): boolean {
    return readBool('storefrontMicroAnimationsEnabled', false);
  },
  get storefrontCinematicIntro(): boolean {
    return readBool('storefrontCinematicIntro', false);
  },
  get storefrontProceduralRichEnabled(): boolean {
    return readBool('storefrontProceduralRichEnabled', false);
  },
  get storefrontDynamicFramingEnabled(): boolean {
    return readBool('storefrontDynamicFramingEnabled', false);
  }
};

export type VsEnv = typeof vsEnv;
